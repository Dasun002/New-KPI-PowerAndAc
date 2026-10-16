// Presents one month's KPI definitions beside the persisted per-area calculation results.
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import {
  AfterViewInit,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnDestroy,
  OnInit,
  QueryList,
  ViewChildren,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as ExcelJS from 'exceljs';
import { saveAs } from 'file-saver';
import { Subscription } from 'rxjs';
import { environment } from '../../../../../environments/environment';
import { Region as RegionApi, RegionService } from '../../../../services/region.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import * as XLSX from 'xlsx';
import { ToastrService } from 'ngx-toastr';
import { FilterUtils } from '../../../../utils/filter.utils';
import { formatEngineerDisplay, getAreaLookupAliases } from '../../../../utils/region-display.utils';

type Region = {
  id: number;
  region: string;
  province: string;
  networkEngineer: string;
  lea: string;
  engName?: string;
};
//kpi rows from backend with metrics initialized to 0 (until we fetch results)
interface KpiMetric {
  achieved: number | null; // %; null means no calculated denominator/data
  maximumPoints: number; // Points Per KPI
  pointsAchieved: number; // Points achieved (achieved % * max points / 100)
}

interface KpiRow {
  id: number;
  number: number;
  perspectives: string;
  category: string;
  strategicObjectives: string;
  kpi: string;

  // ✅ LEFT TABLE new structure
  target: string; // target = descriptionOfKPI (same)
  weightage: number;
  pointsApplicable: number;

  metrics: KpiMetric[];
}

/** final table API response */
type KpiDefinition = {
  id: number;
  perspectives: string;
  category?: string;
  strategicObjectives: string;
  keyPerformanceIndicators: string;

  // backend still returns these
  unit: string;
  descriptionOfKPI: string;
  weightage: number;

  // ✅ NEW FIELD from backend
  pointsApplicable: number;

  month?: number;
  year?: number;
};

type OverallKpiResultApi = {
  id: number;
  kpiDefinitionId: number;
  areaCode: string;
  achievedKpi: number;
  maximumPointsPerKpi: number;
  pointsAchieved: number;
  overallKpiValuePercent: number;
  month: number;
  year: number;
};

@Component({
  selector: 'app-current-month',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule],
  templateUrl: './current-month.component.html',
  styleUrls: ['./current-month.component.scss'],
})
export class CurrentMonthComponent implements OnInit, AfterViewInit, OnDestroy {
  // The selected period drives definition loading, result retrieval, calculation, and export labels.
  currentMonth: string;
  currentYear: number;
  hoveredRowIndex: number | null = null;

  setHoveredRowIndex(index: number | null): void {
    this.hoveredRowIndex = index;
    this.cdr.detectChanges();
  }

  selectedMonth: number;
  selectedYear: number;
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = [];

  loading = false;
  error: string | null = null;
  noDefinitions = false;
  noOverallResults = false;

  // Optional Excel-rendering state retained for the legacy Q1/HTML export path.
  isExcelView = false;
  excelHtmlContent: SafeHtml | string = '';

  // KPI definition endpoint used to build the left-hand table structure.
  private readonly apiBase = `${environment.apiUrl}/kpi-definitions`;


  private readonly overallResultsApiBase = `${environment.apiUrl}/overall-kpi-results`;


  @ViewChildren('leftRowRef', { read: ElementRef })
  private leftRowElements!: QueryList<ElementRef<HTMLTableRowElement>>;

  @ViewChildren('rightRowRef', { read: ElementRef })
  private rightRowElements!: QueryList<ElementRef<HTMLTableRowElement>>;

  regions: Region[] = [];
  regionGroups: {
    region: string;
    provinces: { province: string; engineers: Region[] }[];
    totalEngineers: number;
  }[] = [];
  engineersFlat: Region[] = [];

  // Current period data and the derived per-area totals shown in the right-hand table.
  kpiRows: KpiRow[] = [];
  weightageSum = 0;
  totalPointsApplicable = 0;
  totalPointsAchievedByRegion: number[] = [];
  totalPointsNormalized: Array<number | null> = [];
  totalMaximumPointsByRegion: number[] = [];

  private readonly rowChangesSub = new Subscription();
  private pendingFrame: number | null = null;

  // Provides API access, region metadata, layout synchronization, sanitization, and notifications.
  constructor(
    private http: HttpClient,
    private regionService: RegionService,
    private cdr: ChangeDetectorRef,
    private sanitizer: DomSanitizer,
    private toastr: ToastrService
  ) {
    const now = new Date();
    this.currentYear = now.getFullYear();
    this.currentMonth = now.toLocaleString('en-US', { month: 'long' });

    this.selectedMonth = now.getMonth() + 1;
    this.selectedYear = now.getFullYear();

    // Generate valid year options before synchronizing the initial display period.
    this.yearOptions = FilterUtils.generateYearOptions();

    this.syncDisplayedPeriod();
  }

  ngOnInit(): void {
    // Regions are required before KPI rows can allocate one metric cell per engineer.
    this.loadRegions();
  }

  onMonthChange(month: number): void {
    // Update the visible period and reload definitions/results for the selected month.
    this.selectedMonth = Number(month);
    this.syncDisplayedPeriod();
    this.loadLeftTableFromApi();
  }

  onYearChange(year: number): void {
    // Update the visible period and reload definitions/results for the selected year.
    this.selectedYear = Number(year);
    this.syncDisplayedPeriod();
    this.loadLeftTableFromApi();
  }

  private getMonthLabel(month: number): string {
    // Resolve the numeric month through the shared filter labels.
    return this.monthOptions.find((m) => m.value === month)?.label ?? '';
  }

  private syncDisplayedPeriod(): void {
    // Keep the subtitle and export context synchronized with the selected filters.
    this.currentMonth = this.getMonthLabel(this.selectedMonth);
    this.currentYear = this.selectedYear;
  }

  formatHeaderLabel(value: string | null | undefined): string {
    // Turn compact region/province identifiers into readable table headings.
    if (!value) return '';
    const withSpaces = value.replace(/([a-zA-Z])([0-9])/g, '$1 $2');
    return withSpaces
      .split(/\s+/)
      .map((part) => {
        const isAllCaps = part === part.toUpperCase();
        if (isAllCaps && part.length <= 4) {
          return part;
        }
        return part.charAt(0).toUpperCase() + part.slice(1).toLowerCase();
      })
      .join(' ');
  }

  @HostListener('window:focus')
  onWindowFocus(): void {
    // Refresh data when the user returns to the tab after editing source values elsewhere.
    this.loadRegions();
  }

  ngAfterViewInit(): void {
    // Observe row collection changes so the paired tables remain aligned after rendering.
    this.scheduleRowSync();

    this.rowChangesSub.add(
      this.leftRowElements.changes.subscribe(() => this.scheduleRowSync())
    );

    this.rowChangesSub.add(
      this.rightRowElements.changes.subscribe(() => this.scheduleRowSync())
    );
  }

  ngOnDestroy(): void {
    // Release row-change subscriptions and cancel a pending layout frame.
    this.rowChangesSub.unsubscribe();
    if (this.pendingFrame !== null) cancelAnimationFrame(this.pendingFrame);
  }

  /** Manually trigger calculation/refresh of KPI results */
  calculate(): void {
    // Explicitly recalculate and persist the selected month's overall KPI results.
    this.recalculateOverallResults();
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    // Re-align paired table rows after the viewport changes.
    this.scheduleRowSync();
  }

  private loadRegions(): void {
    // Load and group region metadata before creating KPI metric columns.
    this.loading = true;
    this.error = null;

    this.regionService.getAll().subscribe({
      next: (res) => {
        this.regions = (res ?? []).map((r: RegionApi) => {
          const networkEngineer =
            (r as any).networkEngineer ??
            (r as any).networkengineer ??
            (r as any)['network_engineer'] ??
            '';
          const lea =
            (r as any).lea ??
            (r as any).leaCode ??
            (r as any).leacode ??
            (r as any)['lea_code'] ??
            '';

          return {
            id: r.id,
            region: r.region,
            province: r.province,
            networkEngineer: networkEngineer || '—',
            lea: lea || networkEngineer || '—',
            engName: (r as any).engName ?? (r as any).EngName ?? (r as any)['EngName'] ?? (r as any)['engName'] ?? undefined,
          };
        });

        this.buildRegionGrouping();
        this.loadLeftTableFromApi();
      },
      error: (err) => {
        console.error('Failed loading regions:', err);
        this.error = 'Unable to load regions from backend.';
        this.regions = [];
        this.regionGroups = [];
        this.engineersFlat = [];
        this.loading = false;
        this.kpiRows = [];
        this.computeTotals();
        this.scheduleRowSync();
      },
    });
  }

  private buildRegionGrouping(): void {
    // Build the region/province hierarchy and flatten it into the table's engineer column order.
    const regionMap = new Map<string, Map<string, Region[]>>();

    this.regions.forEach((item) => {
      const provinceMap =
        regionMap.get(item.region) ?? new Map<string, Region[]>();
      const engineers = provinceMap.get(item.province) ?? [];
      engineers.push(item);
      provinceMap.set(item.province, engineers);
      regionMap.set(item.region, provinceMap);
    });

    this.regionGroups = Array.from(regionMap.entries()).map(
      ([region, provinceMap]) => {
        const provinces = Array.from(provinceMap.entries()).map(
          ([province, engineers]) => ({
            province,
            engineers,
          })
        );

        const totalEngineers = provinces.reduce(
          (sum, p) => sum + p.engineers.length,
          0
        );

        return { region, provinces, totalEngineers };
      }
    );

    this.engineersFlat = this.regionGroups.flatMap((g) =>
      g.provinces.flatMap((p) => p.engineers)
    );
  }

  /** ✅ Fetch KPI definitions from backend (LEFT table data) */
  private loadLeftTableFromApi(): void {
    // Load KPI definitions and initialize one zeroed metric array per engineer.
    this.loading = true;
    this.error = null;
    this.noDefinitions = false;
    this.noOverallResults = false;

    const month = this.selectedMonth;
    const year = this.selectedYear;
    const url = `${this.apiBase}?month=${month}&year=${year}`;

    this.http
      .get<KpiDefinition[]>(url)
      .subscribe({
        next: (res) => {
          const list = (res ?? []).sort((a, b) => a.id - b.id);

          this.noDefinitions = list.length === 0;
          if (this.noDefinitions) {
            this.kpiRows = [];
            this.computeTotals();
            this.scheduleRowSync();
            this.noOverallResults = true;
            this.loading = false;
            this.cdr.detectChanges();
            return;
          }

          this.kpiRows = list.map((row, rowIndex) => {
            const metrics: KpiMetric[] = this.engineersFlat.map(
              () => {
                return { achieved: null, maximumPoints: 0, pointsAchieved: 0 };
              }
            );

            return {
              id: row.id,
              number: rowIndex + 1,
              perspectives: row.perspectives,
              category: row.category ?? '',
              strategicObjectives: (row.strategicObjectives ?? '').replace(
                /service assurance/gi,
                'SA'
              ),
              kpi: row.keyPerformanceIndicators,

              // ✅ Target = DescriptionOfKPI (same)
              target: row.descriptionOfKPI,

              weightage: row.weightage,

              // ✅ new field from backend
              pointsApplicable: row.pointsApplicable ?? 0,

              metrics,
            };
          });

          this.computeTotals();
          this.scheduleRowSync();
          console.log('KPI definitions');
          this.kpiRows.forEach(x => console.log(x.id, x.kpi));
          this.loadOverallResultsFromApi();
        },
        error: (err) => {
          console.error('Failed loading final table KPI rows:', err);
          this.error = 'Unable to load KPI rows from backend.';
          this.noDefinitions = true;
          this.noOverallResults = true;
          this.kpiRows = [];
          this.computeTotals();
          this.scheduleRowSync();
          this.loading = false;
          this.cdr.detectChanges();
        },
      });
  }

  public shouldShowQ1Redirect(): boolean {
    // January-March 2026 is displayed through the dedicated Q1 page instead of this table.
    return this.selectedYear === 2026 && (this.selectedMonth === 1 || this.selectedMonth === 2 || this.selectedMonth === 3);
  }

  private loadOverallResultsFromApi(): void {
    // Read stored results for the selected month and map them into KPI/engineer cells.
    this.noOverallResults = false;
    const month = this.selectedMonth;
    const year = this.selectedYear;

    if (this.shouldShowQ1Redirect()) {
      this.isExcelView = false;
      this.excelHtmlContent = '';
      this.loading = false;
      this.cdr.detectChanges();
      return;
    }

    this.isExcelView = false;
    this.excelHtmlContent = '';

    const url = `${this.overallResultsApiBase}?month=${month}&year=${year}`;

    this.http.get<OverallKpiResultApi[]>(url).subscribe({
      next: (rows) => {
        const raw = Array.isArray(rows) ? rows : [];

        // Normalize incoming JSON property casing (handle both camelCase and PascalCase)
        const list: OverallKpiResultApi[] = raw.map((r: any) => ({
          id: Number(r.id ?? r.Id ?? 0),
          kpiDefinitionId: Number(r.kpiDefinitionId ?? r.KpiDefinitionId ?? 0),
          areaCode: String(r.areaCode ?? r.AreaCode ?? ''),
          achievedKpi: Number(r.achievedKpi ?? r.AchievedKpi ?? 0),
          maximumPointsPerKpi: Number(r.maximumPointsPerKpi ?? r.MaximumPointsPerKpi ?? 0),
          pointsAchieved: Number(r.pointsAchieved ?? r.PointsAchieved ?? 0),
          overallKpiValuePercent: Number(r.overallKpiValuePercent ?? r.OverallKpiValuePercent ?? 0),
          month: Number(r.month ?? r.Month ?? 0),
          year: Number(r.year ?? r.Year ?? 0),
        }));

        this.noOverallResults = list.length === 0;
        console.log('Overall results');
        list.forEach(x => console.log(x.kpiDefinitionId, x.areaCode, x.achievedKpi));
        const grouped = new Map<number, OverallKpiResultApi[]>();

        list.forEach((row) => {
          const bucket = grouped.get(row.kpiDefinitionId) ?? [];
          bucket.push(row);
          grouped.set(row.kpiDefinitionId, bucket);
        });

        this.kpiRows = this.kpiRows.map((kpiRow) => {
          const byKpi = grouped.get(kpiRow.id) ?? [];
          console.log('Engineers');
          this.engineersFlat.forEach(x => console.log(x.networkEngineer, x.lea));
          const metrics = this.engineersFlat.map((engineer) => {
            const match = this.findOverallResultForEngineer(byKpi, engineer);
            return {
              achieved: match ? Number(match.achievedKpi) : null,
              maximumPoints: Number(match?.maximumPointsPerKpi ?? 0),
              pointsAchieved: Number(match?.pointsAchieved ?? 0),
            };
          });

          return { ...kpiRow, metrics };
        });

        this.computeTotals();
        const percentByArea = new Map<string, number>();
        list.forEach((row) => {
          if (row.overallKpiValuePercent !== undefined && row.overallKpiValuePercent !== null) {
            percentByArea.set(this.normalizeArea(row.areaCode), Number(row.overallKpiValuePercent));
          }
        });
        if (percentByArea.size > 0) {
          this.totalPointsNormalized = this.engineersFlat.map((engineer, index) => {
            const value = percentByArea.get(this.normalizeArea(engineer.lea));
            return value !== undefined ? Number(value.toFixed(2)) : this.totalPointsNormalized[index];
          });
        }
        this.scheduleRowSync();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed loading overall KPI results:', err);
        this.noOverallResults = true;
        this.computeTotals();
        this.scheduleRowSync();
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  private recalculateOverallResults(): void {
    // Call the backend monthly calculation endpoint, then reload all dashboard dependencies.
    this.loading = true;
    this.error = null;

    const month = this.selectedMonth;
    const year = this.selectedYear;
    const url = `${this.overallResultsApiBase}/calculate?month=${month}&year=${year}`;

    this.http.post<OverallKpiResultApi[]>(url, {}).subscribe({
      next: () => {
        this.loadRegions();
      },
      error: (err) => {
        console.error('Failed recalculating overall KPI results:', err);
        this.loading = false;
        this.error = 'Unable to recalculate overall KPI results.';
        this.cdr.detectChanges();
      },
    });
  }

  private findOverallResultForEngineer(rows: OverallKpiResultApi[], engineer: Region): OverallKpiResultApi | undefined {
    // Match a result using either the engineer's LEA code or network-engineer identifier.
    const candidates = new Set([
      ...getAreaLookupAliases(engineer.lea),
      ...getAreaLookupAliases(engineer.networkEngineer),
    ]);

    const exact = rows.find((x) =>
      getAreaLookupAliases(x.areaCode).some(alias => candidates.has(alias))
    );
    return exact;
  }

  private normalizeArea(value: string): string {
    // Normalize area identifiers so punctuation and casing differences do not break joins.
    return (value ?? '').replace(/[^A-Za-z0-9]/g, '').toLowerCase();
  }

  getEngineerHeaderLabel(eng: Region): string {
    // Format engineer code/name consistently with the Dashboard and Analytics views.
    return formatEngineerDisplay(eng.networkEngineer, eng.engName);
  }

  private computeTotals(): void {
    // Recompute weightage, point, and normalized percentage totals after loading results.
    this.weightageSum = this.kpiRows.reduce(
      (sum, row) => sum + (row.weightage ?? 0),
      0
    );

    this.totalPointsApplicable = this.kpiRows.reduce(
      (sum, row) => sum + (row.pointsApplicable ?? 0),
      0
    );

    this.totalMaximumPointsByRegion = this.engineersFlat.map((_, colIndex) =>
      this.kpiRows.reduce(
        (sum, row) => sum + (row.metrics[colIndex]?.maximumPoints ?? 0),
        0
      )
    );

    this.totalPointsAchievedByRegion = this.engineersFlat.map((_, colIndex) =>
      this.kpiRows.reduce(
        (sum, row) => sum + (row.metrics[colIndex]?.pointsAchieved ?? 0),
        0
      )
    );

    this.totalPointsNormalized = this.totalPointsAchievedByRegion.map((total, colIndex) =>
      this.totalMaximumPointsByRegion[colIndex]
        ? +((total / this.totalMaximumPointsByRegion[colIndex]) * 100).toFixed(2)
        : null
    );
  }

  private scheduleRowSync(): void {
    // Coalesce row changes into one animation-frame layout pass.
    if (!this.leftRowElements || !this.rightRowElements) return;

    if (this.pendingFrame !== null) cancelAnimationFrame(this.pendingFrame);

    this.pendingFrame = requestAnimationFrame(() => {
      this.pendingFrame = null;
      this.syncRowHeights();
    });
  }

  private syncRowHeights(): void {
    // Set each left/right row pair to the taller rendered height for visual alignment.
    const leftRows = this.leftRowElements.toArray().map((ref) => ref.nativeElement);
    const rightRows = this.rightRowElements.toArray().map((ref) => ref.nativeElement);

    if (!leftRows.length || !rightRows.length) return;

    leftRows.forEach((row) => row.style.removeProperty('height'));
    rightRows.forEach((row) => row.style.removeProperty('height'));

    const pairCount = Math.min(leftRows.length, rightRows.length);

    for (let i = 0; i < pairCount; i++) {
      const leftHeight = leftRows[i].getBoundingClientRect().height;
      const rightHeight = rightRows[i].getBoundingClientRect().height;
      const maxHeight = Math.max(leftHeight, rightHeight);

      leftRows[i].style.height = `${maxHeight}px`;
      rightRows[i].style.height = `${maxHeight}px`;
    }
  }

  /** Compute weightage dynamically based on total points (normalized to 100%) */
  getComputedWeightage(row: KpiRow): string {
    // Express a KPI's applicable points as a percentage of the configured total.
    if (this.totalPointsApplicable <= 0) return '0.00%';
    const weightage = (Number(row.pointsApplicable ?? 0) / this.totalPointsApplicable) * 100;
    return `${weightage.toFixed(2)}%`;
  }

  /** Compute the total KPI percentage achieved by all engineers for a row, capped at 100% */
  getTotalKpiPercentage(row: KpiRow): number | null {
    // Convert achieved points to a capped percentage of the KPI's applicable points.
    if (!row.metrics || row.metrics.length === 0 || !row.pointsApplicable) return null;

    const hasData = row.metrics.some((metric) =>
      metric.maximumPoints > 0 ||
      metric.pointsAchieved !== 0 ||
      (metric.achieved !== null && metric.achieved !== 0)
    );
    if (!hasData) return null;

    const totalPoints = row.metrics.reduce((sum, m) => sum + (m.pointsAchieved ?? 0), 0);
    const cappedPoints = Math.min(totalPoints, row.pointsApplicable);
    return (cappedPoints / row.pointsApplicable) * 100;
  }

  async exportToExcel(): Promise<void> {
    // Build and download a workbook containing the KPI definition and per-area performance tables.
    if (this.shouldShowQ1Redirect()) {
      this.toastr.info(
        'No KPI data is available for download for the selected month. Please use the 2026 Q1 page to view this data.',
        'No Data Available'
      );
      return;
    }

    const workbook = new ExcelJS.Workbook();
    const worksheet = workbook.addWorksheet('Current Month KPI');

    // Define colors matching your UI
    const headerBgColor = '0057A6'; // SLT Blue
    const headerTextColor = 'FFFFFF'; // White
    const altRowBgColor = 'E2EDFF'; // Light blue
    const totalRowBgColor = '02B28C'; // SLT Teal
    const borderColor = 'D1D5DB'; // Gray border

    // Starting column for left table
    let currentCol = 1;

    // ===== LEFT TABLE: KPI DEFINITIONS =====
    // Header row 1: R-GM
    const rgmCell = worksheet.getCell(1, currentCol);
    rgmCell.value = 'R-GM';
    rgmCell.font = { bold: true, color: { argb: headerTextColor }, size: 12 };
    rgmCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
    rgmCell.alignment = { horizontal: 'center', vertical: 'middle' };
    worksheet.mergeCells(1, currentCol, 1, currentCol + 6);

    // Header row 2: P-DGM
    const pdgmCell = worksheet.getCell(2, currentCol);
    pdgmCell.value = 'P-DGM';
    pdgmCell.font = { bold: true, color: { argb: headerTextColor }, size: 12 };
    pdgmCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
    pdgmCell.alignment = { horizontal: 'center', vertical: 'middle' };
    worksheet.mergeCells(2, currentCol, 2, currentCol + 6);

    // Header row 3: NW EE/RTOM AREA
    const nwCell = worksheet.getCell(3, currentCol);
    nwCell.value = 'NW EE/RTOM AREA';
    nwCell.font = { bold: true, color: { argb: headerTextColor }, size: 12 };
    nwCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
    nwCell.alignment = { horizontal: 'center', vertical: 'middle' };
    worksheet.mergeCells(3, currentCol, 3, currentCol + 6);

    // Column headers row 4
    const leftHeaders = ['Perspectives', 'Category', 'Key Performance Indicators (KPI)', 'Target', 'Weightage', 'Points Applicable', 'Total KPI Percentage'];
    leftHeaders.forEach((header, idx) => {
      const cell = worksheet.getCell(4, currentCol + idx);
      cell.value = header;
      cell.font = { bold: true, color: { argb: headerTextColor }, size: 10 };
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
      cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
      cell.border = {
        top: { style: 'thin', color: { argb: borderColor } },
        bottom: { style: 'thin', color: { argb: borderColor } },
        left: { style: 'thin', color: { argb: borderColor } },
        right: { style: 'thin', color: { argb: borderColor } }
      };
    });

    // Set column widths for left table
    worksheet.getColumn(currentCol).width = 18;     // Perspectives
    worksheet.getColumn(currentCol + 1).width = 25; // Category
    worksheet.getColumn(currentCol + 2).width = 35; // KPI
    worksheet.getColumn(currentCol + 3).width = 20; // Target
    worksheet.getColumn(currentCol + 4).width = 12; // Weightage
    worksheet.getColumn(currentCol + 5).width = 15; // Points Applicable
    worksheet.getColumn(currentCol + 6).width = 20; // Total KPI Percentage

    // Data rows
    let currentRow = 5;
    this.kpiRows.forEach((row, idx) => {
      const isAltRow = idx % 2 === 1;
      const rowData = [
        row.perspectives,
        row.category,
        row.kpi,
        row.target,
        this.getComputedWeightage(row),
        row.pointsApplicable,
        this.getTotalKpiPercentage(row) === null
          ? '-'
          : Number(this.getTotalKpiPercentage(row)!.toFixed(2))
      ];

      rowData.forEach((value, colIdx) => {
        const cell = worksheet.getCell(currentRow, currentCol + colIdx);
        cell.value = value;
        if (colIdx === 6) {
          cell.alignment = { horizontal: 'center', vertical: 'middle' };
          cell.numFmt = value === '-' ? '@' : '0.00"%"';
        } else {
          cell.alignment = { horizontal: 'left', vertical: 'middle', wrapText: true };
        }
        if (isAltRow) {
          cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: altRowBgColor } };
        }
        cell.border = {
          top: { style: 'thin', color: { argb: borderColor } },
          bottom: { style: 'thin', color: { argb: borderColor } },
          left: { style: 'thin', color: { argb: borderColor } },
          right: { style: 'thin', color: { argb: borderColor } }
        };
        if (colIdx === 2) { // KPI column
          cell.font = { bold: true };
        }
      });
      currentRow++;
    });

    // Total Marks row
    const totalCell1 = worksheet.getCell(currentRow, currentCol);
    totalCell1.value = 'Total Marks';
    totalCell1.font = { bold: true, color: { argb: headerTextColor } };
    totalCell1.alignment = { horizontal: 'right', vertical: 'middle' };
    totalCell1.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };
    worksheet.mergeCells(currentRow, currentCol, currentRow, currentCol + 4);

    const totalCell2 = worksheet.getCell(currentRow, currentCol + 5);
    totalCell2.value = this.totalPointsApplicable;
    totalCell2.font = { bold: true, color: { argb: headerTextColor } };
    totalCell2.alignment = { horizontal: 'center', vertical: 'middle' };
    totalCell2.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };

    const totalCell3 = worksheet.getCell(currentRow, currentCol + 6);
    totalCell3.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };
    totalCell3.border = {
      top: { style: 'thin', color: { argb: borderColor } },
      bottom: { style: 'thin', color: { argb: borderColor } },
      left: { style: 'thin', color: { argb: borderColor } },
      right: { style: 'thin', color: { argb: borderColor } }
    };
    currentRow++;

    // KPI label row
    const kpiLabelCell = worksheet.getCell(currentRow, currentCol);
    kpiLabelCell.value = 'KPI';
    kpiLabelCell.font = { bold: true };
    kpiLabelCell.alignment = { horizontal: 'right', vertical: 'middle' };
    kpiLabelCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'F3F4F6' } };
    worksheet.mergeCells(currentRow, currentCol, currentRow, currentCol + 4);

    const kpiLabelCell2 = worksheet.getCell(currentRow, currentCol + 5);
    kpiLabelCell2.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'F3F4F6' } };
    kpiLabelCell2.border = {
      top: { style: 'thin', color: { argb: borderColor } },
      bottom: { style: 'thin', color: { argb: borderColor } },
      left: { style: 'thin', color: { argb: borderColor } },
      right: { style: 'thin', color: { argb: borderColor } }
    };

    const kpiLabelCell3 = worksheet.getCell(currentRow, currentCol + 6);
    kpiLabelCell3.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'F3F4F6' } };
    kpiLabelCell3.border = {
      top: { style: 'thin', color: { argb: borderColor } },
      bottom: { style: 'thin', color: { argb: borderColor } },
      left: { style: 'thin', color: { argb: borderColor } },
      right: { style: 'thin', color: { argb: borderColor } }
    };
    currentRow++;

    // ===== RIGHT TABLE: REGION PERFORMANCE =====
    currentCol = 8; // Start after left table columns
    currentRow = 1;

    // Region headers
    this.regionGroups.forEach(region => {
      const startCol = currentCol;
      const regionCell = worksheet.getCell(currentRow, startCol);
      regionCell.value = region.region;
      regionCell.font = { bold: true, color: { argb: headerTextColor }, size: 12 };
      regionCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
      regionCell.alignment = { horizontal: 'center', vertical: 'middle' };
      worksheet.mergeCells(currentRow, startCol, currentRow, startCol + (region.totalEngineers * 3) - 1);
      currentCol += region.totalEngineers * 3;
    });

    // Province headers
    currentCol = 8;
    currentRow = 2;
    this.regionGroups.forEach(region => {
      region.provinces.forEach(province => {
        const startCol = currentCol;
        const provCell = worksheet.getCell(currentRow, startCol);
        provCell.value = province.province;
        provCell.font = { bold: true, color: { argb: headerTextColor }, size: 11 };
        provCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
        provCell.alignment = { horizontal: 'center', vertical: 'middle' };
        worksheet.mergeCells(currentRow, startCol, currentRow, startCol + (province.engineers.length * 3) - 1);
        currentCol += province.engineers.length * 3;
      });
    });

    // Network Engineer headers
    currentCol = 8;
    currentRow = 3;
    this.engineersFlat.forEach(eng => {
      const startCol = currentCol;
      const engCell = worksheet.getCell(currentRow, startCol);
      engCell.value = `${this.getEngineerHeaderLabel(eng)}\n(${eng.lea})`;
      engCell.font = { bold: true, color: { argb: headerTextColor }, size: 10 };
      engCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
      engCell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
      worksheet.mergeCells(currentRow, startCol, currentRow, startCol + 2);
      currentCol += 3;
    });

    // Column sub-headers (Achieved KPI, Maximum Points, Points Achieved)
    currentCol = 8;
    currentRow = 4;
    this.engineersFlat.forEach(() => {
      const headers = ['Achieved KPI', 'Maximum Points Per KPI', 'Points Achieved'];
      headers.forEach(header => {
        const cell = worksheet.getCell(currentRow, currentCol);
        cell.value = header;
        cell.font = { bold: true, color: { argb: headerTextColor }, size: 9 };
        cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
        cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
        cell.border = {
          top: { style: 'thin', color: { argb: borderColor } },
          bottom: { style: 'thin', color: { argb: borderColor } },
          left: { style: 'thin', color: { argb: borderColor } },
          right: { style: 'thin', color: { argb: borderColor } }
        };
        worksheet.getColumn(currentCol).width = 12;
        currentCol++;
      });
    });

    // Data rows for right table
    currentRow = 5;
    this.kpiRows.forEach((row, idx) => {
      const isAltRow = idx % 2 === 1;
      currentCol = 8;

      row.metrics.forEach(metric => {
        const achievedCell = worksheet.getCell(currentRow, currentCol);
        const achievedDisplay = this.getMetricAchievedDisplay(metric);
        achievedCell.value = achievedDisplay === '-'
          ? '-'
          : Number(metric.achieved!.toFixed(2));
        achievedCell.numFmt = achievedDisplay === '-' ? '@' : '0.00"%"';
        achievedCell.alignment = { horizontal: 'center', vertical: 'middle' };
        if (isAltRow) achievedCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: altRowBgColor } };
        achievedCell.border = {
          top: { style: 'thin', color: { argb: borderColor } },
          bottom: { style: 'thin', color: { argb: borderColor } },
          left: { style: 'thin', color: { argb: borderColor } },
          right: { style: 'thin', color: { argb: borderColor } }
        };

        const maxPointsCell = worksheet.getCell(currentRow, currentCol + 1);
        maxPointsCell.value = Number(metric.maximumPoints.toFixed(4));
        maxPointsCell.numFmt = '0.0000';
        maxPointsCell.alignment = { horizontal: 'center', vertical: 'middle' };
        if (isAltRow) maxPointsCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: altRowBgColor } };
        maxPointsCell.border = {
          top: { style: 'thin', color: { argb: borderColor } },
          bottom: { style: 'thin', color: { argb: borderColor } },
          left: { style: 'thin', color: { argb: borderColor } },
          right: { style: 'thin', color: { argb: borderColor } }
        };

        const pointsAchCell = worksheet.getCell(currentRow, currentCol + 2);
        pointsAchCell.value = Number(metric.pointsAchieved.toFixed(4));
        pointsAchCell.numFmt = '0.0000';
        pointsAchCell.alignment = { horizontal: 'center', vertical: 'middle' };
        if (isAltRow) pointsAchCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: altRowBgColor } };
        pointsAchCell.border = {
          top: { style: 'thin', color: { argb: borderColor } },
          bottom: { style: 'thin', color: { argb: borderColor } },
          left: { style: 'thin', color: { argb: borderColor } },
          right: { style: 'thin', color: { argb: borderColor } }
        };

        currentCol += 3;
      });
      currentRow++;
    });

    // Summary row (totals)
    currentCol = 8;
    this.totalPointsAchievedByRegion.forEach((total, idx) => {
      const emptyCell = worksheet.getCell(currentRow, currentCol);
      emptyCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };

      const maxCell = worksheet.getCell(currentRow, currentCol + 1);
      maxCell.value = Number(this.totalMaximumPointsByRegion[idx].toFixed(4));
      maxCell.numFmt = '0.0000';
      maxCell.font = { bold: true, color: { argb: headerTextColor } };
      maxCell.alignment = { horizontal: 'center', vertical: 'middle' };
      maxCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };

      const totalCell = worksheet.getCell(currentRow, currentCol + 2);
      totalCell.value = Number(total.toFixed(4));
      totalCell.numFmt = '0.0000';
      totalCell.font = { bold: true, color: { argb: headerTextColor } };
      totalCell.alignment = { horizontal: 'center', vertical: 'middle' };
      totalCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };

      currentCol += 3;
    });
    currentRow++;

    // Normalized percentage row
    currentCol = 8;
    this.totalPointsNormalized.forEach(norm => {
      const emptyCell1 = worksheet.getCell(currentRow, currentCol);
      emptyCell1.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };

      const emptyCell2 = worksheet.getCell(currentRow, currentCol + 1);
      emptyCell2.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };

      const normCell = worksheet.getCell(currentRow, currentCol + 2);
      normCell.value = norm === null ? '-' : Number(norm.toFixed(2));
      normCell.numFmt = norm === null ? '@' : '0.00"%"';
      normCell.font = { bold: true, color: { argb: headerTextColor } };
      normCell.alignment = { horizontal: 'center', vertical: 'middle' };
      normCell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };

      currentCol += 3;
    });

    // Generate and download
    const buffer = await workbook.xlsx.writeBuffer();
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const selectedLabel = this.getMonthLabel(this.selectedMonth) || 'Month';
    saveAs(blob, `Current_Month_KPI_${selectedLabel}_${this.selectedYear}.xlsx`);
  }

  getAchievedCellClass(metric: KpiMetric): string {
    // Mark cells according to whether achieved points meet the allocated maximum.
    if (!metric || metric.achieved === null || !metric.maximumPoints || metric.maximumPoints <= 0) return '';
    return metric.pointsAchieved >= metric.maximumPoints ? 'target-achieved' : 'target-failed';
  }

  getMetricAchievedDisplay(metric: KpiMetric): string {
    if (!metric || metric.achieved === null || metric.achieved === undefined) {
      return '-';
    }

    if (metric.achieved === 0 && metric.maximumPoints <= 0 && metric.pointsAchieved === 0) {
      return '-';
    }

    return `${metric.achieved.toFixed(2)}%`;
  }

  getKpiRowClass(row: KpiRow): string {
    // Assign category-specific styling to the KPI row.
    const cat = (row.category ?? '').toLowerCase();
    if (cat.includes('enterprise') || cat.includes('enteprise')) {
      return 'category-enterprise';
    } else if (cat.includes('other operator') || cat.includes('operator')) {
      return 'category-other-operator';
    } else if (cat.includes('assurance')) {
      return 'category-assurance';
    } else if (cat.includes('fulfillment') || cat.includes('fullfillment')) {
      return 'category-fulfillment';
    }
    return '';
  }
}
