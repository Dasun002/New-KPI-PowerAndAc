import { CommonModule } from '@angular/common';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Component, OnDestroy, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import * as ExcelJS from 'exceljs';
import { IpNwOpService, IpNwOpKpiDto, IpNwOpMetricPayload, IpNwOpMetric } from '../../../../services/ip-nw-op.service';
import { RegionService, Region } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';
import { FilterUtils } from '../../../../utils/filter.utils';
import { buildAreaLabels, buildEngineerDisplayMap, getScopedLeas, mapRegionRecords } from '../../../../utils/region-display.utils';
import { environment } from '../../../../../environments/environment';

interface RegionRow {
  region?: string;
  province?: string;
  networkEngineer?: string;
  lea?: string; // friendly string in DB like "AD / PR"
  engName?: string;
}

interface RtomAreaLookup {
  areaCode?: string;
  displayName?: string;
  AreaCode?: string;
  DisplayName?: string;
}


@Component({
  selector: 'app-ip-nw-op',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ip-nw-op.component.html',
  styleUrls: ['./ip-nw-op.component.scss'],
})
export class IpNwOpComponent implements OnInit, OnDestroy {
  pageTitle = 'IP NW OP';

  data: IpNwOpKpiDto[] = [];
  metrics: IpNwOpMetric[] = [];
  regionTable: RegionRow[] = [];

  loading = true;
  saving = false;
  error: string | null = null;



  // Month/Year selection (current month default)
  selectedMonth: number = new Date().getMonth() + 1; // 1-12
  selectedYear: number = new Date().getFullYear();
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = FilterUtils.generatePlatformYearOptions();

  // current month days (recalculated when month/year changes)
  daysInMonth: number = this.getDaysInMonth(this.selectedYear, this.selectedMonth);

  // dropdown form
  formValues = {
    dropdown1: '', // Region
    dropdown2: '', // Province
    dropdown3: '', // Engineer
  };

  dropdown2Options: string[] = [];
  dropdown3Options: string[] = [];
  engineerNameMap: Record<string, string> = {};

  // Normalized database area keys shown in the table and export.
  areaKeys: string[] = [];
  private areaLabels: Record<string, string> = {};
  areaEngineerGroups: { label: string; areaKeys: string[] }[] = [];
  private databaseAreaKeyByCode: Record<string, string> = {};
  private databaseAreaKeyByLabel: Record<string, string> = {};
  private databaseAreaCodeByKey: Record<string, string> = {};
  private databaseAreaLabelByKey: Record<string, string> = {};

  // edit cell state (nested key like "total_nodes.adipr")
  editCell: { rowId: number | null; key: string | null; value: string } = {
    rowId: null,
    key: null,
    value: '',
  };

  // simple toast system (no external lib)
  toasts: Array<{ id: number; type: 'success' | 'danger'; text: string }> = [];
  private toastId = 1;

  private filtersInitialized = false;

  // Injects the required dependencies.
  constructor(
    private http: HttpClient,
    private ipNwOpService: IpNwOpService,
    private regionService: RegionService,
    private authService: AuthService,
    private cdr: ChangeDetectorRef

  ) { }

  // Initializes the component, loads the region table, and fetches data.
  ngOnInit(): void {
    this.loadRegionTable();
    this.loadRtomAreaLookup();
    this.initializeFilters();
    this.loadData();
  }


  // Cleans up resources when the component is destroyed.
  ngOnDestroy(): void {
    // Cleanup if needed
  }

  // Checks if the user is authorized to edit the page.
  get isEditingAllowed(): boolean {
    return this.authService.canEditPage('IP NW OP');
  }

  // -------------------------
  // Helpers
  // -------------------------
  // Normalizes a string for comparison.
  private norm(s: any): string {
    return s ? String(s).replace(/[^A-Za-z0-9]/g, '').toLowerCase() : '';
  }

  // Calculates the number of days in the specified month and year.
  private getDaysInMonth(year: number, month: number): number {
    return new Date(year, month, 0).getDate();
  }

  // Displays a toast notification.
  private showToast(type: 'success' | 'danger', text: string): void {
    const id = this.toastId++;
    this.toasts.push({ id, type, text });
    setTimeout(() => this.dismissToast(id), 2600);
  }

  // Dismisses a specific toast notification.
  dismissToast(id: number): void {
    this.toasts = this.toasts.filter((t) => t.id !== id);
  }

  // Regions list
  // Gets a list of unique regions from the region table.
  get regions(): string[] {
    return Array.from(new Set(this.regionTable.map((r) => r.region).filter(Boolean) as string[]));
  }

  // Checks if the user has permission to edit metrics.
  get canEditMetrics(): boolean {
    return this.isEditingAllowed;
  }

  // Gets the display label for an area code (disambiguated when two codes share a label).
  areaLabel(areaKey: string): string {
    return this.areaLabels[areaKey] ?? this.baseAreaLabel(areaKey);
  }

  private baseAreaLabel(areaKey: string): string {
    return this.databaseAreaLabelByKey[areaKey] || areaKey.toUpperCase();
  }

  private resolveDatabaseAreaKey(value?: string | null): string {
    const normalized = this.norm(value);
    return normalized
      ? (this.databaseAreaKeyByCode[normalized] || this.databaseAreaKeyByLabel[normalized] || normalized)
      : '';
  }

  private loadRtomAreaLookup(): void {
    this.http.get<RtomAreaLookup[]>(`${environment.apiUrl}/rtom-areas`).subscribe({
      next: (areas) => {
        this.databaseAreaKeyByCode = {};
        this.databaseAreaKeyByLabel = {};
        this.databaseAreaCodeByKey = {};
        this.databaseAreaLabelByKey = {};

        (areas ?? []).forEach((area) => {
          const databaseCode = (area.areaCode ?? area.AreaCode)?.trim();
          if (!databaseCode) return;
          const key = this.norm(databaseCode);
          if (!key) return;

          this.databaseAreaKeyByCode[key] = key;
          this.databaseAreaCodeByKey[key] = databaseCode;
          const displayName = (area.displayName ?? area.DisplayName)?.trim();
          if (displayName) {
            const normalizedLabel = this.norm(displayName);
            if (normalizedLabel) this.databaseAreaKeyByLabel[normalizedLabel] = key;
            this.databaseAreaLabelByKey[key] = displayName;
          }
        });

        if (!areas?.length) {
          console.error('RTOM area lookup returned no database records.');
          this.showToast('danger', 'RTOM area lookup is empty; area metrics may not match region codes.');
        }
        this.refreshAreaKeys();
      },
      error: (error) => {
        console.error('Failed to fetch RTOM area lookup from API:', error);
        this.showToast('danger', 'RTOM area labels could not be loaded; using region LEA codes.');
        this.refreshAreaKeys();
      }
    });
  }

  // Build area columns and NW EE groups from region data and stored KPI metric keys.
  private refreshAreaKeys(): void {
    const { dropdown1, dropdown2, dropdown3 } = this.formValues;
    const scopedRegions = this.regionTable.filter((region) =>
      (!dropdown1 || region.region === dropdown1) &&
      (!dropdown2 || region.province === dropdown2) &&
      (!dropdown3 || region.networkEngineer === dropdown3)
    );
    const labelsByAreaKey = new Map<string, string>();
    const engineerByAreaKey = new Map<string, string>();
    this.regionTable.forEach((region) => {
      const key = this.resolveDatabaseAreaKey(region.lea);
      if (!key) return;
      if (!labelsByAreaKey.has(key)) labelsByAreaKey.set(key, this.baseAreaLabel(key));
    });
    scopedRegions.forEach((region) => {
      const key = this.resolveDatabaseAreaKey(region.lea);
      if (key && !engineerByAreaKey.has(key)) {
        engineerByAreaKey.set(key, region.networkEngineer?.trim() || 'Unassigned NW EE');
      }
    });

    const keys = new Set(
      getScopedLeas(this.regionTable, { region: dropdown1, province: dropdown2, engineer: dropdown3 })
        .map((lea) => this.resolveDatabaseAreaKey(lea))
        .filter(Boolean)
    );
    if (!dropdown1) {
      this.metrics.forEach((metric) => {
        const key = this.resolveDatabaseAreaKey(metric.area_code);
        if (key) keys.add(key);
      });
    }
    const labelOf = (key: string) => labelsByAreaKey.get(key) || this.baseAreaLabel(key);
    this.areaKeys = Array.from(keys).sort((a, b) => labelOf(a).localeCompare(labelOf(b)));
    this.areaLabels = buildAreaLabels(this.areaKeys, labelOf);
    const groups = new Map<string, Set<string>>();
    this.areaKeys.forEach((key) => {
      const engineer = engineerByAreaKey.get(key) || 'Unassigned NW EE';
      if (!groups.has(engineer)) groups.set(engineer, new Set<string>());
      groups.get(engineer)!.add(key);
    });
    this.areaEngineerGroups = Array.from(groups.entries())
      .map(([engineer, areaKeys]) => ({
        label: this.engineerNameMap[engineer] || engineer,
        areaKeys: this.areaKeys.filter((key) => areaKeys.has(key))
      }))
      .filter((group) => group.areaKeys.length > 0)
      .sort((a, b) => a.label.localeCompare(b.label));
  }

  // Calculates and returns the availability percentage for an area.
  getAreaPercentage(entry: IpNwOpKpiDto, areaKey: string): string {
    // Find metric for this KPI and area
    const metric = this.metrics.find(
      m => m.ip_nw_op_kpi_id === entry.id &&
        this.resolveDatabaseAreaKey(m.area_code) === this.resolveDatabaseAreaKey(areaKey)
    );

    if (!metric) {
      return '-';
    }

    const pct = this.calculatePercentage(metric.total_minutes, metric.unavailable_minutes, metric.total_nodes);
    return pct !== null && Number.isFinite(pct) ? `${pct.toFixed(2)}%` : '-';
  }

  // Gets the value for a specific metric bucket in an area.
  getAreaMetric(
    entry: IpNwOpKpiDto,
    bucket: 'total_minutes' | 'unavailable_minutes' | 'total_nodes',
    areaKey: string
  ): string {
    // Find metric for this KPI and area
    const metric = this.metrics.find(
      m => m.ip_nw_op_kpi_id === entry.id &&
        this.resolveDatabaseAreaKey(m.area_code) === this.resolveDatabaseAreaKey(areaKey)
    );

    if (!metric) return '-';

    const value = metric[bucket];
    return value === undefined || value === null ? '-' : String(value);
  }

  // Builds a payload object from an entry for API requests.
  private buildPayload(entry: IpNwOpKpiDto): IpNwOpKpiDto {
    return { ...entry };
  }

  // Normalizes a dictionary of metrics, ensuring all values are valid numbers.
  private normalizeMetricDict(
    source?: Record<string, string | number | undefined | null>
  ): Record<string, number | null> {
    if (!source) return {};

    return Object.entries(source).reduce((acc, [key, value]) => {
      if (value === undefined || value === null || value === '') return acc;
      const numeric = typeof value === 'number' ? value : Number(value);
      if (!Number.isFinite(numeric)) return acc;
      acc[key] = numeric;
      return acc;
    }, {} as Record<string, number | null>);
  }

  // -------------------------
  // API Calls
  // -------------------------
  // Legacy auth methods removed

  // Loads the region table data and initializes filters.
  loadRegionTable(): void {
    this.regionService.getAll().subscribe({
      next: (res: Region[] | any[]) => {
        this.regionTable = mapRegionRecords(res) as RegionRow[];
        this.initializeFilters();
        this.refreshAreaKeys();
      },
      error: (err) => {
        console.error('Failed to fetch region table:', err);
        this.regionTable = [];
        this.showToast('danger', 'Failed to load region data. Filters are unavailable until regiondata is reachable.');
        this.initializeFilters();
        this.refreshAreaKeys();
      },
    });
  }

  // Loads IP network OP admin data and metrics for the selected period.
  loadData(): void {
    this.loading = true;
    this.error = null;
    this.metrics = [];

    // No area parameter: load every area for the period.
    this.ipNwOpService.getAll(this.selectedMonth, this.selectedYear).subscribe({
      next: (records) => {
        this.data = Array.isArray(records) ? (records as IpNwOpKpiDto[]) : [];
        this.metrics = this.buildMetricsFromKpis(this.data);
        this.refreshAreaKeys();
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load IP NW OP admin data:', err);
        this.data = [];
        this.loading = false;
        this.error = 'Failed to load IP NW OP KPI data.';
        this.cdr.detectChanges();
      }
    });
  }

  // Builds one metric per KPI and stored area code; keys are the server's lowercase area codes.
  private buildMetricsFromKpis(entries: IpNwOpKpiDto[]): IpNwOpMetric[] {
    return entries.flatMap((entry) => {
      const keys = new Set([
        ...Object.keys(entry.unavailable_minutes ?? {}),
        ...Object.keys(entry.total_minutes ?? {}),
        ...Object.keys(entry.total_nodes ?? {}),
      ]);

      return Array.from(keys).filter(Boolean).map((key) => ({
        ip_nw_op_kpi_id: entry.id,
        area_code: key,
        month: this.selectedMonth,
        year: this.selectedYear,
        unavailable_minutes: entry.unavailable_minutes?.[key] ?? null,
        total_minutes: entry.total_minutes?.[key] ?? null,
        total_nodes: entry.total_nodes?.[key] ?? null,
      } as IpNwOpMetric));
    });
  }

  // Handles changes to the selected month and year, reloading data.
  onMonthYearChange(): void {
    // Update daysInMonth when month/year changes
    this.daysInMonth = this.getDaysInMonth(this.selectedYear, this.selectedMonth);
    // Reset editing state
    this.cancelEdit();
    // Reload data with new month/year
    this.loadData();
  }


  // -------------------------
  // Dropdown cascading
  // -------------------------
  // Updates the province options based on the selected region.
  private updateDropdown2Options(region: string): void {
    if (!region) {
      this.dropdown2Options = [];
      return;
    }
    const provinces = Array.from(
      new Set(
        this.regionTable
          .filter((x) => x.region === region)
          .map((x) => x.province)
          .filter(Boolean) as string[]
      )
    );
    this.dropdown2Options = provinces;
  }

  // Updates the engineer options based on the selected province.
  private updateDropdown3Options(province: string): void {
    if (!province || !this.formValues.dropdown1) {
      this.dropdown3Options = [];
      return;
    }
    const engineers = Array.from(
      new Set(
        this.regionTable
          .filter((x) => x.region === this.formValues.dropdown1 && x.province === province)
          .map((x) => x.networkEngineer)
          .filter(Boolean) as string[]
      )
    );
    this.dropdown3Options = engineers;
    
    this.engineerNameMap = buildEngineerDisplayMap(this.regionTable, this.dropdown3Options);
  }

  // Initializes all filter dropdown values.
  private initializeFilters(): void {
    if (this.filtersInitialized) return;

    // Don't auto-select, leave all as empty strings
    this.formValues.dropdown1 = '';
    this.formValues.dropdown2 = '';
    this.formValues.dropdown3 = '';

    this.filtersInitialized = true;
  }

  // Handles changes to any of the filter dropdowns; they narrow the area columns client-side.
  onDropdownChange(name: 'dropdown1' | 'dropdown2' | 'dropdown3', value: string): void {
    if (name === 'dropdown1') {
      this.formValues.dropdown1 = value;
      this.formValues.dropdown2 = '';
      this.formValues.dropdown3 = '';

      this.updateDropdown2Options(value);
      this.dropdown3Options = [];
    } else if (name === 'dropdown2') {
      this.formValues.dropdown2 = value;
      this.formValues.dropdown3 = '';

      this.updateDropdown3Options(value);
    } else {
      this.formValues.dropdown3 = value;
    }

    this.cancelEdit();
    this.refreshAreaKeys();
  }


  // -------------------------
  // KPI calculations
  // -------------------------
  // Calculates the network availability percentage.
  calculatePercentage(totalMinutes: any, unavailableMinutes: any, totalNodes: any): number | null {
    const tm = Number(totalMinutes) || 0;
    const um = Number(unavailableMinutes) || 0;
    const tn = Number(totalNodes) || 0;

    const denominator = tm > 0
      ? tm
      : 24 * 60 * this.daysInMonth * tn;
    if (denominator <= 0) return null;

    const numerator = denominator - um;
    const pct = (100 * numerator) / denominator;
    return Math.max(0, Math.min(100, pct));
  }

  // -------------------------
  // Editing
  // -------------------------
  // Starts editing a specific metric cell.
  startEdit(entry: IpNwOpKpiDto, key: 'unavailable_minutes' | 'total_minutes' | 'total_nodes', areaKey: string): void {
    if (!this.canEditMetrics || !areaKey) return;

    const k = areaKey;
    const nestedKey = `${key}.${k}`;

    // Find the current metric value
    const metric = this.metrics.find(
      m => m.ip_nw_op_kpi_id === entry.id &&
        this.resolveDatabaseAreaKey(m.area_code) === this.resolveDatabaseAreaKey(k)
    );

    const currentVal = metric ? metric[key] : null;

    this.editCell = {
      rowId: entry.id,
      key: nestedKey,
      value: currentVal === undefined || currentVal === null ? '' : String(currentVal),
    };
  }

  // Handles input changes while editing a metric cell.
  onEditInput(value: string | number | null | undefined): void {
    const normalizedValue = value === null || value === undefined ? '' : String(value);
    this.editCell = { ...this.editCell, value: normalizedValue };
  }

  // Saves the edited metric cell value to the database.
  async doneEdit(): Promise<void> {
    if (!this.canEditMetrics || this.editCell.rowId === null || !this.editCell.key || this.saving) {
      return;
    }

    const [parentKey, childKey] = this.editCell.key.split('.');
    if (!parentKey || !childKey) {
      this.cancelEdit();
      return;
    }

    const newValue = this.editCell.value.trim();
    const numericValue = newValue === '' ? null : Number(newValue);
    const nextValue =
      numericValue === null || Number.isFinite(numericValue) ? numericValue : null;

    const kpiId = this.editCell.rowId;
    const areaCode = this.getStoredAreaCode(childKey);
    this.cancelEdit();

    const payload: IpNwOpMetricPayload = {};
    if (parentKey === 'unavailable_minutes') payload.unavailableMinutes = nextValue;
    else if (parentKey === 'total_minutes') payload.totalMinutes = nextValue;
    else if (parentKey === 'total_nodes') {
      payload.totalNodes = nextValue;
      const autoTotalMinutes =
        nextValue === null ? null : Math.round(24 * 60 * this.daysInMonth * Number(nextValue));
      payload.totalMinutes = autoTotalMinutes;
    }

    try {
      this.saving = true;
      await firstValueFrom(
        this.ipNwOpService.upsertMetric(kpiId, areaCode, this.selectedMonth, this.selectedYear, payload)
      );
      this.showToast('success', 'Metric updated successfully.');
      let metric = this.metrics.find(
        (m) => m.ip_nw_op_kpi_id === kpiId &&
          this.resolveDatabaseAreaKey(m.area_code) === this.resolveDatabaseAreaKey(childKey)
      );

      if (!metric) {
        metric = {
          ip_nw_op_kpi_id: kpiId,
          area_code: areaCode,
          month: this.selectedMonth,
          year: this.selectedYear,
        } as IpNwOpMetric;
        this.metrics = [...this.metrics, metric];
      }

      if (payload.unavailableMinutes !== undefined) {
        metric.unavailable_minutes = payload.unavailableMinutes ?? null;
      }
      if (payload.totalMinutes !== undefined) {
        metric.total_minutes = payload.totalMinutes ?? null;
      }
      if (payload.totalNodes !== undefined) {
        metric.total_nodes = payload.totalNodes ?? null;
      }
    } catch (error) {
      console.error('Failed to persist metric change:', error);
      this.showToast('danger', 'Failed to save change. Reloading latest data.');
      this.loadData();
    } finally {
      this.saving = false;
    }
  }

  private getStoredAreaCode(areaKey: string): string {
    const storedCode = this.metrics.find(
      metric => this.resolveDatabaseAreaKey(metric.area_code) === this.resolveDatabaseAreaKey(areaKey)
    )?.area_code;
    return storedCode || this.databaseAreaCodeByKey[this.resolveDatabaseAreaKey(areaKey)] || areaKey;
  }

  private getAreaEngineerLabel(areaKey: string): string {
    return this.areaEngineerGroups.find(group => group.areaKeys.includes(areaKey))?.label ?? '';
  }

  // Cancels the current edit operation.
  cancelEdit(): void {
    this.editCell = { rowId: null, key: null, value: '' };
  }

  // Checks if a specific cell is currently being edited.
  isCellEditing(
    rowId: number,
    bucket: 'unavailable_minutes' | 'total_minutes' | 'total_nodes',
    areaKey: string
  ): boolean {
    if (!this.editCell.key) return false;
    return this.editCell.rowId === rowId && this.editCell.key === `${bucket}.${areaKey}`;
  }

  // Handles keyboard events for the editing input field.
  handleEditKey(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.doneEdit();
    } else if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelEdit();
    }
  }

  // -------------------------
  // Save All
  // -------------------------
  // -------------------------
  // Excel Export (same structure as React)
  // -------------------------
  // Exports the KPI data and metrics to an Excel file.
  async exportToExcel(): Promise<void> {
    const workbook = new ExcelJS.Workbook();
    const ws = workbook.addWorksheet('Network Availability');

    const areas = this.areaKeys;

    ws.addRow(['KPI (NW Availability - IP Core NW / BSR NW / Service Edge NW)']);
    ws.addRow([`Generated Date: ${new Date().toISOString().split('T')[0]}`]);
    ws.addRow([]);

    const headers = [
      'No',
      'Network Engineer KPI',
      'Division',
      'Section',
      'KPI Percent',
      ...areas.map((area) => {
        const engineer = this.getAreaEngineerLabel(area);
        return engineer ? `${engineer} - ${this.areaLabel(area)}` : this.areaLabel(area);
      }),
    ];

    const headerRow = ws.addRow(headers);
    headerRow.eachCell((cell: ExcelJS.Cell) => {
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: '0070C0' } };
      cell.font = { bold: true, color: { argb: 'FFFFFF' } };
      cell.alignment = { horizontal: 'center', vertical: 'middle' };
      cell.border = {
        top: { style: 'thin' },
        left: { style: 'thin' },
        bottom: { style: 'thin' },
        right: { style: 'thin' },
      };
    });

    this.data.forEach((entry) => {
      const row: any[] = [
        entry.id,
        entry.network_engineer_kpi,
        entry.division,
        entry.section,
        entry.kpi_percent,
      ];

      areas.forEach((a) => row.push(this.getAreaPercentage(entry, a)));

      const r = ws.addRow(row);
      r.eachCell((cell: ExcelJS.Cell) => {
        cell.border = {
          top: { style: 'thin' },
          left: { style: 'thin' },
          bottom: { style: 'thin' },
          right: { style: 'thin' },
        };
        cell.alignment = { horizontal: 'center', vertical: 'middle' };
      });

      // Sub-rows with each area's stored metric values
      const tm: any[] = ['', 'Total Minutes', '', '', ''];
      const um: any[] = ['', 'Unavailable Minutes', '', '', ''];
      const tn: any[] = ['', 'Total Nodes', '', '', ''];

      areas.forEach((a) => {
        tm.push(this.getAreaMetric(entry, 'total_minutes', a));
        um.push(this.getAreaMetric(entry, 'unavailable_minutes', a));
        tn.push(this.getAreaMetric(entry, 'total_nodes', a));
      });

      [tm, um, tn].forEach((arr) => {
        const rr = ws.addRow(arr);
        rr.eachCell((cell: ExcelJS.Cell) => {
          cell.border = {
            top: { style: 'thin' },
            left: { style: 'thin' },
            bottom: { style: 'thin' },
            right: { style: 'thin' },
          };
          cell.alignment = { horizontal: 'center', vertical: 'middle' };
        });
      });
    });

    ws.columns.forEach((column: Partial<ExcelJS.Column>) => {
      if (column) {
        column.width = 15;
      }
    });

    const buffer = await workbook.xlsx.writeBuffer();
    const blob = new Blob([buffer], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });

    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `KPI_Report_${new Date().toISOString().split('T')[0]}.xlsx`;
    link.click();

    URL.revokeObjectURL(link.href);
  }
}
