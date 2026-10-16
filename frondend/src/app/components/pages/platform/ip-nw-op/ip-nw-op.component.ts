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
import { buildEngineerDisplayMap, mapRegionRecords } from '../../../../utils/region-display.utils';

interface RegionRow {
  region?: string;
  province?: string;
  networkEngineer?: string;
  lea?: string; // friendly string in DB like "AD / PR"
  engName?: string;
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
    dropdown4: '', // RTOM area db key (lowercase)
  };

  dropdown2Options: string[] = [];
  dropdown3Options: string[] = [];
  dropdown4Options: string[] = [];
  engineerNameMap: Record<string, string> = {};

  // edit cell state (nested key like "total_nodes.adipr")
  editCell: { rowId: number | null; key: string | null; value: string } = {
    rowId: null,
    key: null,
    value: '',
  };

  // simple toast system (no external lib)
  toasts: Array<{ id: number; type: 'success' | 'danger'; text: string }> = [];
  private toastId = 1;

  // ---- IP NW OP mapping (DB keys are lowercase) ----
  optionMapping: Record<string, string> = {
    cenhkmd: 'CEN/HK/MD',
    cenhkmd1: 'CEN/HK/MD',
    gqkintb: 'GQ / KI / NTB',
    ndfrm: 'ND / RM',
    awho: 'AW / HO',
    konix: 'KON / KX',
    ngivt: 'NG / WT',
    kgkly: 'KG / KLY',
    cwpx: 'CW / PX',
    debkymt: 'DB / KY / MT',
    gphtnw: 'GP / HT / NW',
    adipr: 'AD / PR',
    bddwmrg: 'BD / BW / MRG',
    keirn: 'KE / RN',
    embmbmh: 'EMB / HB / MH',
    aggl: 'AG / GL',
    hrktph: 'HR / KT / PH',
    bcjrdkltc: 'BC / AP / KL / TC',
    ja: 'JA',
    komltmbva: 'KO / MLT / MB / VA',
  };

  // friendly -> dbKey (built once)
  private friendlyToDbKey: Record<string, string> = {};
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
    this.buildFriendlyMap();
    this.loadRegionTable();
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

  // Builds a mapping of friendly names to database keys.
  private buildFriendlyMap(): void {
    const out: Record<string, string> = {};
    Object.keys(this.optionMapping).forEach((dbKey) => {
      out[this.norm(this.optionMapping[dbKey])] = dbKey;
      out[this.norm(dbKey)] = dbKey;
    });
    this.friendlyToDbKey = out;
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

  // Selected RTOM db key (lowercase)
  // Gets the normalized selected RTOM database key.
  get selectedKey(): string {
    return this.formValues.dropdown4 ? this.norm(this.formValues.dropdown4) : '';
  }

  // Regions list
  // Gets a list of unique regions from the region table.
  get regions(): string[] {
    return Array.from(new Set(this.regionTable.map((r) => r.region).filter(Boolean) as string[]));
  }

  // Checks if an area filter is currently applied.
  get hasAreaFilter(): boolean {
    return !!this.selectedKey;
  }

  // Checks if the user has permission to edit metrics for the selected area.
  get canEditMetrics(): boolean {
    return this.isEditingAllowed && this.hasAreaFilter;
  }

  // Gets the display label for the selected area.
  get selectedAreaLabel(): string {
    const key = this.formValues.dropdown4;
    if (!key) return '';
    return this.optionMapping[key] || key.toUpperCase();
  }

  // Calculates and returns the availability percentage for the selected area.
  getAreaPercentage(entry: IpNwOpKpiDto): string {
    if (!this.selectedKey) return '-';

    // Find metric for this KPI and area
    const metric = this.metrics.find(
      m => m.ip_nw_op_kpi_id === entry.id && m.area_code === this.selectedKey
    );

    if (!metric) {
      return '-';
    }

    const pct = this.calculatePercentage(metric.total_minutes, metric.unavailable_minutes, metric.total_nodes);
    return pct !== null && Number.isFinite(pct) ? `${pct.toFixed(2)}%` : '-';
  }

  // Gets the value for a specific metric bucket in the selected area.
  getAreaMetric(
    entry: IpNwOpKpiDto,
    bucket: 'total_minutes' | 'unavailable_minutes' | 'total_nodes'
  ): string {
    if (!this.selectedKey) return '-';

    // Find metric for this KPI and area
    const metric = this.metrics.find(
      m => m.ip_nw_op_kpi_id === entry.id && m.area_code === this.selectedKey
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
      },
      error: (err) => {
        console.error('Failed to fetch region table:', err);
        this.regionTable = [];
        this.showToast('danger', 'Failed to load region data. Filters are unavailable until regiondata is reachable.');
        this.initializeFilters();
      },
    });
  }

  // Loads IP network OP admin data and metrics for the selected period.
  loadData(): void {
    this.loading = true;
    this.error = null;
    this.metrics = [];

    // Pass area parameter if selected
    const areaParam = this.selectedKey || undefined;

    this.ipNwOpService.getAll(this.selectedMonth, this.selectedYear, areaParam).subscribe({
      next: (records) => {
        this.data = Array.isArray(records) ? (records as IpNwOpKpiDto[]) : [];
        this.metrics = this.selectedKey ? this.buildMetricsFromKpis(this.data, this.selectedKey) : [];
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

  // Builds a list of metrics from the KPI entries for the specified area.
  private buildMetricsFromKpis(entries: IpNwOpKpiDto[], areaKey: string): IpNwOpMetric[] {
    const key = this.norm(areaKey);
    if (!key) return [];

    return entries.map((entry) => {
      const unavailable = entry.unavailable_minutes?.[key] ?? null;
      const totalMinutes = entry.total_minutes?.[key] ?? null;
      const totalNodes = entry.total_nodes?.[key] ?? null;

      return {
        ip_nw_op_kpi_id: entry.id,
        area_code: key,
        month: this.selectedMonth,
        year: this.selectedYear,
        unavailable_minutes: unavailable,
        total_minutes: totalMinutes,
        total_nodes: totalNodes,
      } as IpNwOpMetric;
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

  // Updates the LEA area options based on the selected engineer.
  private updateDropdown4Options(engineer: string): void {
    if (!engineer || !this.formValues.dropdown1 || !this.formValues.dropdown2) {
      this.dropdown4Options = [];
      return;
    }

    const leas = Array.from(
      new Set(
        this.regionTable
          .filter(
            (x) =>
              x.region === this.formValues.dropdown1 &&
              x.province === this.formValues.dropdown2 &&
              x.networkEngineer === engineer
          )
          .map((x) => this.friendlyToDbKey[this.norm(x.lea)] || this.norm(x.lea))
          .filter(Boolean)
      )
    );

    this.dropdown4Options = leas;
  }

  // Initializes all filter dropdown values.
  private initializeFilters(): void {
    if (this.filtersInitialized) return;

    // Don't auto-select, leave all as empty strings
    this.formValues.dropdown1 = '';
    this.formValues.dropdown2 = '';
    this.formValues.dropdown3 = '';
    this.formValues.dropdown4 = '';

    this.filtersInitialized = true;
  }

  // Handles changes to any of the filter dropdowns.
  onDropdownChange(name: 'dropdown1' | 'dropdown2' | 'dropdown3' | 'dropdown4', value: string): void {
    if (name === 'dropdown1') {
      this.formValues.dropdown1 = value;
      this.formValues.dropdown2 = '';
      this.formValues.dropdown3 = '';
      this.formValues.dropdown4 = '';

      this.updateDropdown2Options(value);
      this.dropdown3Options = [];
      this.dropdown4Options = [];
      this.cancelEdit();
      this.loadData();
      return;
    }

    if (name === 'dropdown2') {
      this.formValues.dropdown2 = value;
      this.formValues.dropdown3 = '';
      this.formValues.dropdown4 = '';

      this.updateDropdown3Options(value);
      this.dropdown4Options = [];
      this.cancelEdit();
      this.loadData();
      return;
    }

    if (name === 'dropdown3') {
      this.formValues.dropdown3 = value;
      this.formValues.dropdown4 = '';

      this.updateDropdown4Options(value);
      this.cancelEdit();
      this.loadData();
      return;
    }

    // dropdown4 - Area changed
    this.formValues.dropdown4 = value;
    this.cancelEdit();
    this.loadData();
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
  startEdit(entry: IpNwOpKpiDto, key: 'unavailable_minutes' | 'total_minutes' | 'total_nodes'): void {
    if (!this.canEditMetrics) return;

    const k = this.selectedKey;
    const nestedKey = `${key}.${k}`;

    // Find the current metric value
    const metric = this.metrics.find(
      m => m.ip_nw_op_kpi_id === entry.id && m.area_code === k
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
        this.ipNwOpService.upsertMetric(kpiId, childKey, this.selectedMonth, this.selectedYear, payload)
      );
      this.showToast('success', 'Metric updated successfully.');
      const areaCode = this.norm(childKey);
      let metric = this.metrics.find(
        (m) => m.ip_nw_op_kpi_id === kpiId && m.area_code === areaCode
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

  // Cancels the current edit operation.
  cancelEdit(): void {
    this.editCell = { rowId: null, key: null, value: '' };
  }

  // Checks if a specific cell is currently being edited.
  isCellEditing(
    rowId: number,
    bucket: 'unavailable_minutes' | 'total_minutes' | 'total_nodes'
  ): boolean {
    if (!this.selectedKey || !this.editCell.key) return false;
    return this.editCell.rowId === rowId && this.editCell.key === `${bucket}.${this.selectedKey}`;
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

    const areas = Object.keys(this.optionMapping);

    ws.addRow(['KPI (NW Availability - IP Core NW / BSR NW / Service Edge NW)']);
    ws.addRow([`Generated Date: ${new Date().toISOString().split('T')[0]}`]);
    ws.addRow([]);

    const headers = [
      'No',
      'Network Engineer KPI',
      'Division',
      'Section',
      'KPI Percent',
      ...areas.map((a) => this.optionMapping[a] || a),
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

      areas.forEach((a) => {
        // Metrics are now stored separately, placeholder for now
        row.push('');
      });

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

      // Sub-rows (placeholders for metrics)
      const tm: any[] = ['', 'Total Minutes', '', '', ''];
      const um: any[] = ['', 'Unavailable Minutes', '', '', ''];
      const tn: any[] = ['', 'Total Nodes', '', '', ''];

      areas.forEach((a) => {
        // Metrics are now stored separately
        tm.push('');
        um.push('');
        tn.push('');
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

