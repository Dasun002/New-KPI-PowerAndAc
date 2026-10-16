// Loads monthly KPI snapshots, maps them to regions, and exposes table and grouped dashboard views.
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
import { environment } from '../../../../environments/environment';
import { AnalyticsService } from '../../../services/analytics.service';
import { FilterUtils } from '../../../utils/filter.utils';
import { Region as RegionApi, RegionService } from '../../../services/region.service';
import { formatEngineerDisplay, getAreaLookupAliases } from '../../../utils/region-display.utils';

type Region = {
  id: number;
  region: string;
  province: string;
  networkEngineer: string;
  lea: string;
  engName?: string;
};

interface KpiMetric {
  achieved: number | null;
  maximumPoints: number;
  pointsAchieved: number;
}

interface KpiRow {
  id: number;
  number: number;
  perspectives: string;
  category: string;
  strategicObjectives: string;
  kpi: string;
  target: string;
  weightage: number;
  pointsApplicable: number;
  metrics: KpiMetric[];
}

interface DashboardEngineerSummary {
  engineer: Region;
  overallPercent: number | null;
  maxPoints: number;
  achievedPoints: number;
}

interface DashboardProvinceGroup {
  province: string;
  engineers: DashboardEngineerSummary[];
}

interface DashboardRegionGroup {
  region: string;
  provinces: DashboardProvinceGroup[];
}

type KpiDefinition = {
  id: number;
  perspectives: string;
  category?: string;
  strategicObjectives: string;
  keyPerformanceIndicators: string;
  unit: string;
  descriptionOfKPI: string;
  weightage: number;
  pointsApplicable: number;
  month?: number;
  year?: number;
};

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule],
  templateUrl: './analytics.component.html',
  styleUrls: ['../overall/current-month/current-month.component.scss'],
})
export class AnalyticsComponent implements OnInit, AfterViewInit, OnDestroy {
  // The selected range controls both the persisted-result calculation request and the analytics query.
  selectedYear: number;
  selectedStartMonth: number;
  selectedEndMonth: number;
  activeView: 'table' | 'dashboard' = 'table';

  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = [];

  loading = false;
  showCalculateButton = false;
  error: string | null = null;
  noDefinitions = false;
  noOverallResults = false;
  hoveredRowIndex: number | null = null;

  kpiRows: KpiRow[] = [];
  regions: Region[] = [];
  regionGroups: {
    region: string;
    provinces: { province: string; engineers: Region[] }[];
    totalEngineers: number;
  }[] = [];
  engineersFlat: Region[] = [];

  weightageSum = 0;
  totalPointsApplicable = 0;
  totalPointsAchievedByRegion: number[] = [];
  totalMaximumPointsByRegion: number[] = [];
  totalPointsNormalized: Array<number | null> = [];
  dashboardGroups: DashboardRegionGroup[] = [];

  private readonly apiBase = `${environment.apiUrl}/kpi-definitions`;

  @ViewChildren('leftRowRef', { read: ElementRef })
  private leftRowElements!: QueryList<ElementRef<HTMLTableRowElement>>;

  @ViewChildren('rightRowRef', { read: ElementRef })
  private rightRowElements!: QueryList<ElementRef<HTMLTableRowElement>>;

  private readonly rowChangesSub = new Subscription();
  private pendingFrame: number | null = null;

  // Provides API access, region metadata, analytics aggregation, and view-change notifications.
  constructor(
    private http: HttpClient,
    private regionService: RegionService,
    private analyticsService: AnalyticsService,
    private cdr: ChangeDetectorRef
  ) {
    const now = new Date();
    this.selectedYear = now.getFullYear();
    this.selectedStartMonth = now.getMonth() + 1;
    this.selectedEndMonth = now.getMonth() + 1;


    const available = this.getAvailableMonths(this.selectedYear);
    if (!available.find(m => m.value === this.selectedStartMonth)) {
      this.selectedStartMonth = available[0].value;
    }
    if (!available.find(m => m.value === this.selectedEndMonth)) {
      this.selectedEndMonth = available[0].value;
    }
  }

  ngOnInit(): void {
    // Initialize valid year/month filters before loading regions, definitions, and stored results.
    this.yearOptions = FilterUtils.generatePlatformYearOptions();
    if (!this.yearOptions.includes(this.selectedYear)) {
      this.selectedYear = this.yearOptions.includes(new Date().getFullYear())
        ? new Date().getFullYear()
        : this.yearOptions[0];
    }
    this.applyAvailableMonths([]);
    this.loadAvailableMonthsAndData();
  }

  ngAfterViewInit(): void {
    // Keep the two side-by-side tables aligned whenever Angular changes their rendered rows.
    this.scheduleRowSync();

    this.rowChangesSub.add(
      this.leftRowElements.changes.subscribe(() => this.scheduleRowSync())
    );

    this.rowChangesSub.add(
      this.rightRowElements.changes.subscribe(() => this.scheduleRowSync())
    );
  }

  ngOnDestroy(): void {
    // Release subscriptions and cancel a queued layout frame when leaving the page.
    this.rowChangesSub.unsubscribe();
    if (this.pendingFrame !== null) cancelAnimationFrame(this.pendingFrame);
  }

@HostListener('window:resize')
  onWindowResize(): void {
    // Recalculate paired row heights after the viewport changes.
    this.scheduleRowSync();
  }

  onYearChange(year: number): void {
    // A year change can alter the allowed months, so reload the available period and page data.
    this.selectedYear = Number(year);
    this.showCalculateButton = true;
    this.loadAvailableMonthsAndData();
  }

  onStartMonthChange(month: number): void {
    // Prevent the selected range from becoming inverted when the start month changes.
    this.selectedStartMonth = Number(month);
    if (this.selectedStartMonth > this.selectedEndMonth) {
      this.selectedEndMonth = this.selectedStartMonth;
    }
    this.showCalculateButton = true;
  }

  onEndMonthChange(month: number): void {
    // Prevent the selected range from becoming inverted when the end month changes.
    this.selectedEndMonth = Number(month);
    if (this.selectedEndMonth < this.selectedStartMonth) {
      this.selectedStartMonth = this.selectedEndMonth;
    }
    this.showCalculateButton = true;
  }

  setActiveView(view: 'table' | 'dashboard'): void {
    // Switch presentation only; both views use the same calculated KPI state.
    this.activeView = view;
  }

  getAvailableMonths(year: number): { value: number; label: string }[] {
    return FilterUtils.getMonthOptions(year);
  }

  getMonthLabel(monthValue: number): string {
    return this.monthOptions.find(m => m.value === monthValue)?.label || '';
  }

  private loadAvailableMonthsAndData(): void {
    // Ask the API which months have stored data, then load the common region/definition model.
    this.analyticsService.getAvailableMonths(this.selectedYear).subscribe({
      next: (months: number[]) => {
        this.applyAvailableMonths(months);
        this.loadRegions();
      },
      error: () => {
        this.applyAvailableMonths([]);
        this.loadRegions();
      }
    });
  }

  private applyAvailableMonths(_months: number[]): void {
    // Keep the selected range compatible with frontend period rules and the current year.
    const validMonths = this.getAvailableMonths(this.selectedYear).map(m => m.value);
    const currentMonth = new Date().getMonth() + 1;

    if (!validMonths.find(month => month === this.selectedStartMonth)) {
      this.selectedStartMonth = validMonths.includes(currentMonth)
        ? currentMonth
        : validMonths[0] ?? this.selectedStartMonth;
    }

    if (!validMonths.find(month => month === this.selectedEndMonth)) {
      this.selectedEndMonth = this.selectedStartMonth;
    }

    if (this.selectedStartMonth > this.selectedEndMonth) {
      this.selectedEndMonth = this.selectedStartMonth;
    }
  }

  calculate(): void {
    // Calculate and persist each month in the selected range before reading aggregated analytics.
    if (this.loading) return;

    this.loading = true;
    this.showCalculateButton = true;
    this.error = null;
    this.noOverallResults = false;

    const url = `${environment.apiUrl}/overall-kpi-results/calculate-range?year=${this.selectedYear}&startMonth=${this.selectedStartMonth}&endMonth=${this.selectedEndMonth}`;
    this.http.post(url, {}).subscribe({
      next: () => {
        this.loading = false;
        this.showCalculateButton = false;
        this.loadAnalyticsResults();
      },
      error: (err: any) => {
        console.error('Failed calculating range:', err);
        this.loading = false;
        this.showCalculateButton = false;
        this.error = 'Unable to calculate analytics data.';
        this.cdr.detectChanges();
      }
    });
  }

  private loadAnalyticsResults(): void {
    // Read the persisted range aggregation and distribute each result into its KPI/engineer cell.
    this.loading = true;
    this.error = null;
    this.noOverallResults = false;

    this.analyticsService.getCumulativeAnalytics(this.selectedYear, this.selectedStartMonth, this.selectedEndMonth)
      .subscribe({
        next: (results: any[]) => {
          // Clear stale cells before applying the latest API response.
          this.kpiRows.forEach(row => {
            row.metrics.forEach((m: any) => {
              m.achieved = null;
              m.maximumPoints = 0;
              m.pointsAchieved = 0;
            });
          });

          if (!results || results.length === 0) {
            this.noOverallResults = true;
          } else {
            results.forEach((apiResult: any) => {
              const row = this.kpiRows.find(r => r.id === apiResult.kpiDefinitionId);
              if (!row) return;

              const idx = this.findEngineerIndexByArea(apiResult.areaCode);
              if (idx === undefined) return;

              row.metrics[idx].achieved = apiResult.achievedKpi == null
                ? null
                : Number(apiResult.achievedKpi);
              row.metrics[idx].maximumPoints = Number(apiResult.maximumPointsPerKpi ?? 0);
              row.metrics[idx].pointsAchieved = Number(apiResult.pointsAchieved ?? 0);
            });
          }

          this.computeTotals();
          this.buildDashboardGroups();
          this.scheduleRowSync();
          this.loading = false;
          this.cdr.detectChanges();
        },
        error: (err: any) => {
          console.error('Failed loading analytics results:', err);
          this.error = 'Unable to load analytics results from backend.';
          this.noOverallResults = true;
          this.loading = false;
          this.cdr.detectChanges();
        }
      });
  }

  private loadRegions(): void {
    // Load region metadata first because KPI results are matched to engineers through area codes.
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
            lea: lea || '—',
            engName: (r as any).engName ?? (r as any).EngName ?? (r as any)['EngName'] ?? (r as any)['engName'] ?? undefined,
          };
        });

        this.buildRegionGrouping();
        this.loadKpiDefinitions();
      },
      error: (err: any) => {
        console.error('Failed loading regions:', err);
        this.error = 'Unable to load regions from backend.';
        this.regions = [];
        this.regionGroups = [];
        this.engineersFlat = [];
        this.loading = false;
        this.kpiRows = [];
        this.dashboardGroups = [];
        this.computeTotals();
        this.scheduleRowSync();
      },
    });
  }

  private buildRegionGrouping(): void {
    // Build the region/province hierarchy used by table headers and the dashboard grouping.
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

  private loadKpiDefinitions(): void {
    // Create empty KPI rows from the selected period's definitions, then populate their metrics.
    this.loading = true;
    this.error = null;
    this.noDefinitions = false;
    this.noOverallResults = false;

    // Definitions are requested using the end month because the API exposes a month/year definition query.
    const month = this.selectedEndMonth;
    const year = this.selectedYear;
    const url = `${this.apiBase}?month=${month}&year=${year}`;

    this.http.get<KpiDefinition[]>(url).subscribe({
      next: (res) => {
        const list = (res ?? []).sort((a, b) => a.id - b.id);

        this.noDefinitions = list.length === 0;
        if (this.noDefinitions) {
          this.kpiRows = [];
          this.dashboardGroups = [];
          this.computeTotals();
          this.scheduleRowSync();
          this.noOverallResults = true;
          this.loading = false;
          this.cdr.detectChanges();
          return;
        }

        this.kpiRows = list.map((row, rowIndex) => {
          const metrics: KpiMetric[] = this.engineersFlat.map(() => {
            return { achieved: null, maximumPoints: 0, pointsAchieved: 0 };
          });

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
            target: row.descriptionOfKPI,
            weightage: row.weightage,
            pointsApplicable: row.pointsApplicable ?? 0,
            metrics,
          };
        });

        this.computeTotals();
        this.buildDashboardGroups();
        this.scheduleRowSync();
        this.loadAnalyticsResults();
      },
      error: (err: any) => {
        console.error('Failed loading final table KPI rows:', err);
        this.error = 'Unable to load KPI rows from backend.';
        this.noDefinitions = true;
        this.noOverallResults = true;
        this.kpiRows = [];
        this.dashboardGroups = [];
        this.computeTotals();
        this.scheduleRowSync();
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  private findEngineerIndexByArea(areaCode: string): number | undefined {
    // Resolve an API area code to the flat engineer index used by each KPI row's metric array.
    const targetAliases = new Set(getAreaLookupAliases(areaCode));
    if (!targetAliases.size) return undefined;

    const exactIndex = this.engineersFlat.findIndex(eng =>
      this.getEngineerAreaCandidates(eng).some(candidate => targetAliases.has(candidate))
    );
    return exactIndex >= 0 ? exactIndex : undefined;
  }

  private getEngineerAreaCandidates(engineer: Region): string[] {
    // Accept either the LEA code or network-engineer code when matching a result to a region.
    return Array.from(new Set(
      [engineer.lea, engineer.networkEngineer]
        .flatMap(value => getAreaLookupAliases(value))
    ));
  }

  private normalizeArea(value: string | null | undefined): string {
    // Normalize formatting differences so codes such as NW/ABC and nwabc compare consistently.
    return (value ?? '').replace(/[^A-Za-z0-9]/g, '').toLowerCase();
  }

  private computeTotals(): void {
    // Recompute footer totals and normalized percentages after definitions or metrics change.
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

  private buildDashboardGroups(): void {
    // Convert flat KPI metrics into region/province/engineer cards for Dashboard View.
    const regionMap = new Map<string, Map<string, DashboardEngineerSummary[]>>();

    this.engineersFlat.forEach((engineer, index) => {
      const metrics = this.kpiRows
        .map((row) => row.metrics[index])
        .filter((metric): metric is KpiMetric => Boolean(metric));

      const maxPoints = metrics.reduce((sum, metric) => sum + (metric.maximumPoints ?? 0), 0);
      const achievedPoints = metrics.reduce((sum, metric) => sum + (metric.pointsAchieved ?? 0), 0);
      const overallPercent = maxPoints > 0
        ? +((achievedPoints / maxPoints) * 100).toFixed(1)
        : null;

      const region = engineer.region || 'Unknown';
      const province = engineer.province || 'Unknown';

      if (!regionMap.has(region)) {
        regionMap.set(region, new Map<string, DashboardEngineerSummary[]>());
      }

      const provinceMap = regionMap.get(region)!;
      const engineersForProvince = provinceMap.get(province) ?? [];
      engineersForProvince.push({
        engineer,
        overallPercent,
        maxPoints,
        achievedPoints,
      });
      provinceMap.set(province, engineersForProvince);
    });

    this.dashboardGroups = Array.from(regionMap.entries()).map(([region, provinceMap]) => ({
      region,
      provinces: Array.from(provinceMap.entries())
        .map(([province, engineers]) => ({
          province,
          engineers: engineers.sort((a, b) => a.engineer.networkEngineer.localeCompare(b.engineer.networkEngineer)),
        }))
        .sort((a, b) => a.province.localeCompare(b.province)),
    }));
  }

  getDashboardColor(percent: number | null): string {
    // Map the period score to the visual threshold color used by dashboard cards.
    if (percent === null) return '#9ca3af';
    if (percent >= 90) return '#10b981';
    if (percent >= 75) return '#3b82f6';
    if (percent >= 50) return '#f59e0b';
    return '#ef4444';
  }

  // Prevent progress bars from extending beyond their 100% visual range.
getDashboardProgressPercent(percent: number | null): number {
  return percent === null ? 0 : Math.min(100, percent);
}

  getEngineerHeaderLabel(eng: Region): string {
    return formatEngineerDisplay(eng.networkEngineer, eng.engName);
  }

  private scheduleRowSync(): void {
    // Coalesce repeated DOM changes into one animation-frame height synchronization.
    if (!this.leftRowElements || !this.rightRowElements) return;

    if (this.pendingFrame !== null) cancelAnimationFrame(this.pendingFrame);

    this.pendingFrame = requestAnimationFrame(() => {
      this.pendingFrame = null;
      this.syncRowHeights();
    });
  }

  private syncRowHeights(): void {
    // Give each paired left/right table row the larger rendered height for visual alignment.
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

  setHoveredRowIndex(index: number | null): void {
    // Store the row currently being highlighted by the table interaction.
    this.hoveredRowIndex = index;
    this.cdr.detectChanges();
  }

  async exportToExcel(): Promise<void> {
    // Recreate both analytics tables in an XLSX worksheet, including headers, totals, and formatting.
    if (this.kpiRows.length === 0 || this.noOverallResults) {
      console.warn('No KPI data is available for download for the selected period.');
      return;
    }

    const workbook = new ExcelJS.Workbook();
    const worksheet = workbook.addWorksheet('Analytics KPI');

    // Use the same brand and alternating-row colors as the on-screen analytics tables.
    const headerBgColor = '0057A6'; // SLT Blue
    const headerTextColor = 'FFFFFF'; // White
    const altRowBgColor = 'E2EDFF'; // Light blue
    const totalRowBgColor = '02B28C'; // SLT Teal
    const borderColor = 'D1D5DB'; // Gray border

    // The left table contains KPI definitions and per-KPI totals.
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

    // The right table contains the three metrics for every engineer and area.
    // ===== RIGHT TABLE: REGION PERFORMANCE =====
    currentCol = 8; // Start after left table columns
    currentRow = 1;

    // Region headers
    this.regionGroups.forEach(region => {
      const startCol = currentCol;
      const regionCell = worksheet.getCell(currentRow, startCol);
      regionCell.value = this.formatHeaderLabel(region.region);
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
        provCell.value = this.formatHeaderLabel(province.province);
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

    // Serialize the workbook in memory and trigger a browser download.
    const buffer = await workbook.xlsx.writeBuffer();
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const startLabel = this.getMonthLabel(this.selectedStartMonth) || 'Start';
    const endLabel = this.getMonthLabel(this.selectedEndMonth) || 'End';
    saveAs(blob, `Analytics_KPI_${startLabel}_to_${endLabel}_${this.selectedYear}.xlsx`);
  }

  getComputedWeightage(row: KpiRow): string {
    // Express this KPI's applicable points as a percentage of the configured total.
    if (this.totalPointsApplicable <= 0) return '0.00%';
    const weightage = (Number(row.pointsApplicable ?? 0) / this.totalPointsApplicable) * 100;
    return `${weightage.toFixed(2)}%`;
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

  getTotalKpiPercentage(row: KpiRow): number | null {
    // Convert the row's achieved points into a capped percentage of its applicable points.
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

  getKpiRowClass(row: KpiRow): string {
    // Select a category class so KPI rows receive the matching visual treatment.
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

  getAchievedCellClass(metric: KpiMetric): string {
    // Mark a metric as achieved when its points meet or exceed its allocated maximum.
    if (!metric || !metric.maximumPoints || metric.maximumPoints <= 0) return '';
    return metric.pointsAchieved >= metric.maximumPoints ? 'target-achieved' : 'target-failed';
  }

  formatHeaderLabel(value: string | null | undefined): string {
    // Format compact API labels such as Region1 into readable table headings.
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
}
