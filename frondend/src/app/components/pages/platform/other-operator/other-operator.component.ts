import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import * as XLSX from 'xlsx';
import {
  OtherOperatorKpiPlatformService,
  OtherKpiDto,
  OtherMetricDto,
  UpsertOtherMetricRequest
} from '../../../../services/other-operator-kpi-platform.service';
import { RegionService, Region } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';
import { FilterUtils } from '../../../../utils/filter.utils';
import { buildEngineerDisplayMap, mapRegionRecords } from '../../../../utils/region-display.utils';

interface KpiData {
  _id: { $oid: string } | number;
  kpiId?: number;
  id?: number | string;
  no: number;
  networkEngineerKpi: string;
  division: string;
  section: string;
  kpiPercent: string;
  areas?: { [key: string]: number };
  updatedAt?: { $date: string };
  __v?: number;
  [key: string]: any;
}

type AdminKpiRow = OtherKpiDto & { displayOrder?: number };

interface RegionData {
  id?: number;
  region: string;
  province: string;
  networkEngineer: string;
  lea: string;
  engName?: string;
}

@Component({
  selector: 'app-other-operator',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './other-operator.component.html',
  styleUrls: ['./other-operator.component.scss']
})
export class OtherOperatorComponent implements OnInit {
  pageTitle = 'Other Operator';

  formValues = { dropdown1: '', dropdown2: '', dropdown3: '', dropdown4: '' };

  data: KpiData[] = [];
  regionTable: RegionData[] = [];
  adminKpiRows: AdminKpiRow[] = [];
  editingCell: { rowId: string | number | null; key: string | null } = { rowId: null, key: null };
  activeEditValue = '';

  dropdown2Options: string[] = [];
  dropdown3Options: string[] = [];
  dropdown4Options: string[] = [];
  visibleColumns: string[] = [];
  engineerNameMap: Record<string, string> = {};

  loading = true;
  error: string | null = null;
  isEditingAllowed = true;
  userRole: string = 'User';
  private editingMessageShown = false;

  nonEditableColumns = ['no', 'networkEngineerKpi', 'division', 'kpiPercent'];
  baseColumns = ['no', 'networkEngineerKpi', 'division', 'section', 'kpiPercent'];
  readonly metricColumnKey = 'kpiValue';

  headerMapping: { [key: string]: string } = {
    no: 'No',
    networkEngineerKpi: 'Network Engineer KPI',
    division: 'Category',
    section: 'Target',
    kpiPercent: 'Weightage',
    kpiValue: 'Achievement (%)'
  };

  optionMapping: { [key: string]: string } = {
    CENHK: 'CEN/HK',
    CENMD: 'CEN/MD',
    GQKINTB: 'GQ/KI/NTB',
    NDRM: 'ND/RM',
    AWHO: 'AW/HO',
    KONKX: 'KON/KX',
    KONIX: 'KON/KX',
    NGWT: 'NG/WT',
    NGIVT: 'NG/WT',
    KGKLY: 'KG/KLY',
    CWPX: 'CW/PX',
    KYMT: 'KY/MT',
    GPHTNW: 'GP/HT/NW',
    ADPR: 'AD/PR',
    ADIPR: 'AD/PR',
    BDBWMRG: 'BD/BW/MRG',
    BDDWMRG: 'BD/BW/MRG',
    KERN: 'KE/RN',
    KEIRN: 'KE/RN',
    EMBHBMH: 'EMB/HB/MH',
    EMBMBMH: 'EMB/HB/MH',
    AGGL: 'AG/GL',
    HRKTPH: 'HR/KT/PH',
    BCAPKLTC: 'BC/AP/KL/TC',
    BCJRDKLTC: 'BC/AP/KL/TC',
    JA: 'JA',
    KOMLTMBVA: 'KO/MLT/MB/VA'
  };

  metricsRows: OtherMetricDto[] = [];
  metricsLoading = false;
  metricsError: string | null = null;

  selectedMonth = new Date().getMonth() + 1;
  selectedYear = new Date().getFullYear();
  private periodLockedByUser = false;
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = [];

  constructor(
    private toastr: ToastrService,
    private otherOperatorKpiService: OtherOperatorKpiPlatformService,
    private regionService: RegionService,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {
    this.yearOptions = FilterUtils.generatePlatformYearOptions();
  }

  ngOnInit() {
    this.loadRegionTable();
    this.loadData();
    this.checkUserRole();
    this.setupEditPermissionCheck();
  }

  loadData() {
    this.loading = true;
    this.otherOperatorKpiService.getAll().subscribe({
      next: (kpis) => {
        const rows = Array.isArray(kpis) ? this.decorateAdminRows(kpis) : [];
        this.adminKpiRows = rows;

        this.syncSelectedPeriodFromData(rows);
        this.rebuildKpiMatrix();
        this.loading = false;
        this.loadMetrics();
      },
      error: (err) => {
        console.error('Failed to load Other Operator KPI admin data:', err);
        this.adminKpiRows = [];
        this.loading = false;
        this.error = 'Failed to load Other Operator KPI data.';
      }
    });
  }

  loadMetrics() {
    const month = Number(this.selectedMonth);
    const year = Number(this.selectedYear);
    if (!month || !year) return;

    const site = this.resolveAreaCode(this.formValues.dropdown4);
    if (!site) {
      this.metricsRows = [];
      this.metricsLoading = false;
      this.metricsError = null;
      this.rebuildKpiMatrix();
      this.cdr.detectChanges();
      return;
    }

    this.metricsLoading = true;
    this.metricsError = null;

    this.otherOperatorKpiService.getMetrics(month, year, site).subscribe({
      next: (metrics) => {
        this.metricsRows = Array.isArray(metrics) ? metrics : [];
        if (this.metricsRows.length > 0 && !this.periodLockedByUser) {
          const firstMetric = this.metricsRows[0];
          if (firstMetric.month && firstMetric.year) {
            this.selectedMonth = firstMetric.month;
            this.selectedYear = firstMetric.year;
          }
        }
        this.metricsLoading = false;
        this.rebuildKpiMatrix();
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load Other Operator KPI metrics:', err);
        this.metricsRows = [];
        this.metricsLoading = false;
        this.metricsError = `Failed to load KPI metrics for ${this.getMonthLabel(month)} ${year}. Please check if data exists for this period.`;
        this.rebuildKpiMatrix();
        this.cdr.detectChanges();
      }
    });
  }

  loadRegionTable() {
    this.regionService.getAll().subscribe({
      next: (res: Region[] | any[]) => {
        this.regionTable = mapRegionRecords(res) as RegionData[];
      },
      error: (err) => {
        console.error('Failed to fetch region table from API:', err);
        this.regionTable = [];
        this.toastr.error('Region data could not be loaded. Filters are unavailable until regiondata is reachable.', 'Region Data');
      }
    });
  }

  checkUserRole() {
    this.userRole = this.authService.getRole() ?? 'User';
    this.recomputeEditPermission();
  }

  setupEditPermissionCheck() {
    this.checkEditPermission();
    setInterval(() => this.checkEditPermission(), 60000);
  }

  checkEditPermission(): boolean {
    this.recomputeEditPermission();
    return this.isEditingAllowed;
  }

  private recomputeEditPermission() {
    this.isEditingAllowed = this.authService.canEditPage('Other Operator KPI');
  }

  getUniqueRegions(): string[] {
    return Array.from(new Set(this.regionTable.map(r => r.region))).filter(Boolean);
  }

  getMonthLabel(value: number): string {
    return this.monthOptions.find((option: { value: number; label: string }) => option.value === value)?.label ?? `M${value}`;
  }

  private decorateAdminRows(rows: OtherKpiDto[]): AdminKpiRow[] {
    return rows.map((kpi, index) => ({ ...kpi, displayOrder: this.resolveDisplayOrder(kpi, index) }));
  }

  private resolveDisplayOrder(kpi?: OtherKpiDto | null, fallbackIndex: number = 0): number {
    if (kpi?.displayOrder && kpi.displayOrder > 0) return kpi.displayOrder;
    const legacyNo = (kpi as any)?.no;
    if (typeof legacyNo === 'number' && legacyNo > 0) return legacyNo;
    return fallbackIndex + 1;
  }

  private getIdKey(id?: number | string | null): string | null {
    if (id === null || id === undefined || id === '') return null;
    return String(id);
  }

  private resolveNumericId(id?: number | string | null): number | undefined {
    if (typeof id === 'number') return id;
    if (typeof id === 'string') {
      const parsed = Number(id);
      if (!Number.isNaN(parsed)) return parsed;
    }
    return undefined;
  }

  private buildRowId(sourceId: number | string | undefined, fallback: number): { $oid: string } | number {
    if (typeof sourceId === 'number') return sourceId;
    if (typeof sourceId === 'string' && sourceId.trim().length) return { $oid: sourceId };
    return fallback;
  }

  private resolveKpiIdentifier(row: KpiData): number | null {
    if (typeof row.kpiId === 'number') return row.kpiId;
    if (typeof row._id === 'number') return row._id;
    if (typeof row._id === 'object' && row._id.$oid) {
      const parsed = Number(row._id.$oid);
      if (!Number.isNaN(parsed)) return parsed;
    }
    return null;
  }

  private rebuildKpiMatrix() {
    if (!this.formValues.dropdown4) {
      this.data = this.buildBaseKpiDataFromAdmin();
      this.visibleColumns = [...this.baseColumns];
      return;
    }

    if (!this.metricsRows.length) {
      this.data = this.buildBaseKpiDataFromAdmin();
      this.visibleColumns = [...this.baseColumns, this.metricColumnKey];
      return;
    }

    this.data = this.buildKpiDataFromMetrics(this.metricsRows);
    this.visibleColumns = [...this.baseColumns, this.metricColumnKey];
  }

  private buildBaseKpiDataFromAdmin(): KpiData[] {
    if (!this.adminKpiRows.length) return [];
    return this.adminKpiRows.map((kpi, index) => {
      const displayOrder = this.resolveDisplayOrder(kpi, index);
      const numericId = this.resolveNumericId(kpi.id);
      return {
        _id: this.buildRowId(kpi.id, displayOrder),
        kpiId: numericId,
        no: displayOrder,
        networkEngineerKpi: kpi.networkEngineerKpi,
        division: kpi.division,
        section: kpi.section,
        kpiPercent: this.formatWeightageValue(kpi.kpiPercent),
        areas: {}
      };
    });
  }

  private syncSelectedPeriodFromData(rows: OtherKpiDto[]) {
    if (this.periodLockedByUser || !rows || !rows.length) return;
    const ordered = rows.filter(row => (row as any).month > 0 && (row as any).year > 0).sort((a, b) => {
      if ((a as any).year === (b as any).year) return (a as any).month - (b as any).month;
      return (a as any).year - (b as any).year;
    });
    if (!ordered.length) return;
    const latest = ordered[ordered.length - 1] as any;
    this.selectedYear = latest.year;
    this.selectedMonth = latest.month;
  }

  private buildKpiDataFromMetrics(metrics: OtherMetricDto[]): KpiData[] {
    const baseRows = this.buildBaseKpiDataFromAdmin();
    if (!metrics || !metrics.length) return baseRows;
    if (!baseRows.length) return this.buildKpiDataFromMetricsFallback(metrics);

    const rowsByKey = new Map<string, KpiData>();
    baseRows.forEach(row => {
      const key = this.getRowMatchKey(row);
      if (key) rowsByKey.set(key, row);
    });

    const extraRows: KpiData[] = [];
    metrics.forEach((metric, metricIndex) => {
      const metricKey = this.getRowMatchKey(metric) ?? `metric-${metricIndex}`;
      let targetRow = rowsByKey.get(metricKey);
      if (!targetRow) {
        targetRow = this.createRowFromMetric(metric, baseRows.length + extraRows.length + 1);
        rowsByKey.set(metricKey, targetRow);
        extraRows.push(targetRow);
      } else {
        // update section/kpiPercent from metric (month/year-aware values)
        targetRow.section = metric.target ?? targetRow.section;
        targetRow.kpiPercent = this.formatWeightageValue(metric.kpiPercent ?? null) || targetRow.kpiPercent;
      }
      this.applyMetricValueToRow(targetRow, metric);
    });

    return [...baseRows, ...extraRows].sort((a, b) => a.no - b.no);
  }

  private buildKpiDataFromMetricsFallback(metrics: OtherMetricDto[]): KpiData[] {
    if (!metrics || !metrics.length) return [];
    const masterById = new Map<string, AdminKpiRow>();
    this.adminKpiRows.forEach((kpi, index) => {
      const decoratedOrder = this.resolveDisplayOrder(kpi, index);
      const idKey = this.getIdKey(kpi.id);
      if (idKey) masterById.set(idKey, { ...kpi, displayOrder: decoratedOrder });
    });

    const grouped = new Map<string, KpiData>();
    metrics.forEach((metric, metricIndex) => {
      const idKey = this.getIdKey(metric.id);
      const groupKey = idKey ?? `legacy-${metricIndex}`;
      if (!grouped.has(groupKey)) {
        const master = idKey ? masterById.get(idKey) : undefined;
        const displayOrder = this.resolveDisplayOrder(master, grouped.size);
        grouped.set(groupKey, {
          _id: this.buildRowId(metric.id ?? master?.id, displayOrder),
          kpiId: this.resolveNumericId(metric.id ?? master?.id),
          no: displayOrder,
          networkEngineerKpi: master?.networkEngineerKpi ?? metric.networkEngineerKpi ?? '',
          division: master?.division ?? metric.division ?? '',
          section: metric.target ?? '',
          kpiPercent: this.formatWeightageValue(metric.kpiPercent ?? master?.kpiPercent),
          areas: {}
        });
      }
      this.applyMetricValueToRow(grouped.get(groupKey)!, metric);
    });
    return Array.from(grouped.values()).sort((a, b) => a.no - b.no);
  }

  private normalizeAreaValue(value?: string | null): string {
    return value ? value.replace(/[^A-Za-z0-9]/g, '').toUpperCase() : '';
  }

  private getRowMatchKey(source: Partial<KpiData | AdminKpiRow | OtherMetricDto> | null | undefined): string | null {
    if (!source) return null;
    const numericKey = this.resolveNumericId((source as any).kpiId ?? (source as any).otherKpiId ?? (source as any).otherOperatorKpiId ?? undefined);
    if (numericKey !== undefined) return `num:${numericKey}`;
    const rawId = (source as any).id ?? (source as any)._id;
    if (typeof rawId === 'string' && rawId.trim()) return `str:${rawId.trim()}`;
    if (typeof rawId === 'number' && !Number.isNaN(rawId)) return `num:${rawId}`;
    if (rawId && typeof rawId === 'object' && typeof rawId.$oid === 'string') return `oid:${rawId.$oid}`;
    return null;
  }

  private createRowFromMetric(metric: OtherMetricDto, fallbackOrder: number): KpiData {
    const kpiId = metric.otherKpiId ?? (metric as any).otherOperatorKpiId ?? metric.id;
    const numericId = this.resolveNumericId(kpiId);
    return {
      _id: this.buildRowId(kpiId, fallbackOrder),
      kpiId: numericId,
      no: fallbackOrder,
      networkEngineerKpi: metric.networkEngineerKpi ?? '',
      division: metric.division ?? '',
      section: metric.target ?? '',
      kpiPercent: this.formatWeightageValue(metric.kpiPercent),
      areas: {}
    };
  }

  private applyMetricValueToRow(row: KpiData, metric: OtherMetricDto) {
    const areaCode = metric.site ? metric.site.trim().toUpperCase() : '';
    const areaKey = this.normalizeAreaKey(areaCode);
    if (!row.areas) row.areas = {};
    row.areas[areaKey] = metric.kpiValue ?? 0;
    row[areaKey] = metric.kpiValue ?? 0;
    if (areaCode && areaCode !== areaKey) {
      row.areas[areaCode] = metric.kpiValue ?? 0;
      row[areaCode] = metric.kpiValue ?? 0;
    }
    if (this.formValues.dropdown4 && this.formValues.dropdown4 !== areaKey && this.formValues.dropdown4 !== areaCode) {
      row.areas[this.formValues.dropdown4] = metric.kpiValue ?? 0;
      row[this.formValues.dropdown4] = metric.kpiValue ?? 0;
    }
  }

  private resolveAreaCode(value?: string | null): string {
    const normalized = this.normalizeAreaValue(value);
    if (!normalized) return '';
    const directMatch = Object.keys(this.optionMapping).find(key => this.normalizeAreaValue(key) === normalized);
    if (directMatch) return directMatch;
    const labelMatch = Object.entries(this.optionMapping).find(([, label]) => this.normalizeAreaValue(label) === normalized);
    if (labelMatch) return labelMatch[0];
    return normalized;
  }

  private normalizeAreaKey(area?: string | null): string {
    const resolved = this.resolveAreaCode(area);
    return resolved || 'UNKNOWN';
  }

  private formatWeightageValue(value?: number | string | null): string {
    if (value === undefined || value === null) return '';
    if (typeof value === 'number') return `${value}%`;
    const clean = value.toString().trim();
    return clean.endsWith('%') ? clean : `${clean}%`;
  }

  updateDropdown2Options() {
    if (!this.formValues.dropdown1) { this.dropdown2Options = []; this.formValues.dropdown2 = ''; return; }
    const provinces = Array.from(new Set(this.regionTable.filter(x => x.region === this.formValues.dropdown1).map(x => x.province))).filter(Boolean);
    this.dropdown2Options = provinces;
    this.formValues.dropdown2 = '';
    this.dropdown3Options = []; this.formValues.dropdown3 = '';
    this.dropdown4Options = []; this.formValues.dropdown4 = '';
    this.visibleColumns = [...this.baseColumns];
  }

  updateDropdown3Options() {
    if (!this.formValues.dropdown2 || !this.formValues.dropdown1) { this.dropdown3Options = []; this.formValues.dropdown3 = ''; return; }
    const engineers = Array.from(new Set(this.regionTable.filter(x => x.region === this.formValues.dropdown1 && x.province === this.formValues.dropdown2).map(x => x.networkEngineer))).filter(Boolean);
    this.dropdown3Options = engineers;
    
    this.engineerNameMap = buildEngineerDisplayMap(this.regionTable, this.dropdown3Options);

    this.formValues.dropdown3 = '';
    this.dropdown4Options = []; this.formValues.dropdown4 = '';
    this.visibleColumns = [...this.baseColumns];
  }

  updateDropdown4Options() {
    if (!this.formValues.dropdown3 || !this.formValues.dropdown1 || !this.formValues.dropdown2) {
      this.dropdown4Options = []; this.formValues.dropdown4 = ''; this.loadMetrics(); this.recomputeEditPermission(); return;
    }
    const leas = this.regionTable.filter(x => x.region === this.formValues.dropdown1 && x.province === this.formValues.dropdown2 && x.networkEngineer === this.formValues.dropdown3).map(x => this.resolveAreaCode(x.lea)).filter(code => !!code);
    this.dropdown4Options = Array.from(new Set(leas));
    this.formValues.dropdown4 = '';
    this.visibleColumns = [...this.baseColumns];
    this.loadMetrics();
    this.recomputeEditPermission();
  }

  updateVisibleColumns() { this.refreshColumnsFromData(); }

  onDropdownChange(field: string, value: string) {
    const normalizedValue = field === 'dropdown4' ? this.resolveAreaCode(value) : value;
    (this.formValues as any)[field] = normalizedValue;
    switch (field) {
      case 'dropdown1': this.formValues.dropdown2 = ''; this.formValues.dropdown3 = ''; this.formValues.dropdown4 = ''; this.dropdown3Options = []; this.dropdown4Options = []; this.visibleColumns = [...this.baseColumns]; this.updateDropdown2Options(); this.loadMetrics(); this.recomputeEditPermission(); break;
      case 'dropdown2': this.formValues.dropdown3 = ''; this.formValues.dropdown4 = ''; this.dropdown4Options = []; this.visibleColumns = [...this.baseColumns]; this.updateDropdown3Options(); this.loadMetrics(); this.recomputeEditPermission(); break;
      case 'dropdown3': this.formValues.dropdown4 = ''; this.visibleColumns = [...this.baseColumns]; this.updateDropdown4Options(); this.recomputeEditPermission(); break;
      case 'dropdown4': this.updateVisibleColumns(); this.loadMetrics(); this.recomputeEditPermission(); break;
    }
  }

  onPeriodChange() { this.periodLockedByUser = true; this.editingRowId = null; this.rowEditValues = {}; this.loadMetrics(); }

  resetAreaFilter() {
    if (!this.formValues.dropdown4) return;
    this.formValues.dropdown4 = '';
    this.updateVisibleColumns();
    this.loadMetrics();
    this.recomputeEditPermission();
  }

  formatPercent(val: any): string {
    if (val === undefined || val === null || val === '') return '-';
    if (typeof val === 'number') return `${val.toFixed(2)}%`;
    const s = String(val).trim();
    const cleanValue = s.replace(/%/g, '');
    const numValue = parseFloat(cleanValue);
    if (!isNaN(numValue)) return `${numValue.toFixed(2)}%`;
    return s.endsWith('%') ? s : `${s}%`;
  }

  formatMetricValue(val: any): string {
    if (val === undefined || val === null || val === '') return '-';
    return String(val);
  }

  getCellValue(item: KpiData, key: string): any {
    if (key === this.metricColumnKey) {
      return this.getSelectedAreaMetricValue(item);
    }
    if (item[key] !== undefined && item[key] !== null && item[key] !== '') return item[key];
    if (item.areas && typeof item.areas === 'object' && item.areas[key] !== undefined) return item.areas[key];
    const normalizedKey = this.normalizeAreaKey(key);
    if (normalizedKey && item.areas && item.areas[normalizedKey] !== undefined) return item.areas[normalizedKey];
    const resolvedKey = this.resolveAreaCode(key);
    if (resolvedKey && item.areas && item.areas[resolvedKey] !== undefined) return item.areas[resolvedKey];
    const upperKey = key.toUpperCase().trim();
    if (upperKey && item.areas && item.areas[upperKey] !== undefined) return item.areas[upperKey];
    return null;
  }

  getRowId(item: KpiData): string | number { return typeof item._id === 'object' && item._id.$oid ? item._id.$oid : item._id as number; }
  isEditingCell(item: KpiData, key: string): boolean { return this.editingCell.rowId === this.getRowId(item) && this.editingCell.key === key; }
  selectMetricInput(event: Event) { const target = event.target as HTMLInputElement | null; if (target) requestAnimationFrame(() => target.select()); }

  startEdit(item: KpiData, key: string) {
    if (this.nonEditableColumns.includes(key)) return;
    if (!this.isEditingAllowed) {
      if (!this.editingMessageShown) {
        this.toastr.info('You do not currently have edit permission on this page.', 'Edit Disabled');
        this.editingMessageShown = true;
        setTimeout(() => (this.editingMessageShown = false), 3000);
      }
      return;
    }
    this.editingCell = { rowId: this.getRowId(item), key };
    const existing = this.getCellValue(item, key);
    this.activeEditValue = existing === null || existing === undefined ? '' : String(existing);
  }

  rowEditValues: { [rowId: string]: { kpiValue: string; section: string } } = {};
  editingRowId: string | number | null = null;

  enterRowEditMode(row: KpiData) {
    if (!this.isEditingAllowed) { this.toastr.info('You do not have edit permission on this page.', 'Edit Disabled'); return; }
    const id = this.getRowId(row);
    const kv = this.getCellValue(row, this.metricColumnKey);
    this.rowEditValues[String(id)] = {
      kpiValue: kv !== null && kv !== undefined ? String(kv) : '',
      section: row.section ?? ''
    };
    this.editingRowId = id;
  }

  cancelRowEditMode() {
    this.editingRowId = null;
    this.rowEditValues = {};
  }

  isEditingRow(row: KpiData): boolean {
    return this.editingRowId !== null && this.editingRowId === this.getRowId(row);
  }

  saveRow(row: KpiData) {
    if (!this.isEditingAllowed) { this.toastr.error('You do not have permission to update KPI values.', 'Permission Denied'); return; }
    const areaCode = this.resolveAreaCode(this.formValues.dropdown4);
    if (!areaCode) { this.toastr.error('Unable to determine the selected RTOM area.', 'Invalid Area'); return; }
    const kpiId = this.resolveKpiIdentifier(row);
    if (kpiId === null) { this.toastr.error('Unable to resolve the KPI identifier for this row.', 'Missing KPI Id'); return; }

    const edits = this.rowEditValues[String(this.getRowId(row))];
    if (!edits) return;

    const kpiValueStr = String(edits.kpiValue ?? '').replace(/%/g, '').trim();
    const kpiValue = kpiValueStr === '' ? undefined : Number(kpiValueStr);
    if (kpiValue !== undefined && !Number.isFinite(kpiValue)) {
      this.toastr.error('Please enter a valid numeric value for Achievement.', 'Invalid Value'); return;
    }

    const request: UpsertOtherMetricRequest = {
      otherKpiId: kpiId,
      site: areaCode,
      kpiValue: kpiValue ?? undefined,
      target: edits.section.trim() || undefined,
      month: Number(this.selectedMonth),
      year: Number(this.selectedYear)
    };

    this.metricsLoading = true;
    this.otherOperatorKpiService.upsertMetric(request).subscribe({
      next: () => {
        this.metricsLoading = false;
        this.editingRowId = null;
        this.rowEditValues = {};
        this.cdr.detectChanges();
        this.toastr.success('Saved successfully.', 'Success');
        this.loadMetrics();
      },
      error: (err) => {
        this.metricsLoading = false;
        console.error('Failed to save Other Operator KPI metric value', err);
        this.cdr.detectChanges();
        this.toastr.error('Save failed. Please try again.', 'Save Failed');
      }
    });
  }

  saveEdit(item: KpiData, key: string) {
    if (!this.isEditingAllowed) { this.toastr.error('You do not have permission to update this KPI value.', 'Permission Denied'); return; }
    const areaCode = this.resolveAreaCode(this.formValues.dropdown4);
    if (!areaCode) { this.toastr.error('Unable to determine the selected RTOM area. Please pick an area and try again.', 'Invalid Area'); return; }

    const rowId = this.getRowId(item);
    const latestRow = this.data.find(row => this.getRowId(row) === rowId) ?? item;
    const kpiId = this.resolveKpiIdentifier(latestRow);
    if (kpiId === null) { this.toastr.error('Unable to resolve the KPI identifier for this row.', 'Missing KPI Id'); return; }

    const normalizedValue = String(this.activeEditValue ?? '').trim();
    if (key === 'section') {
      // section (Target) accepts any string
      if (!normalizedValue) { this.toastr.error('Please enter a value before saving.', 'Invalid Value'); return; }
    } else {
      const cleanValue = normalizedValue.replace(/%/g, '');
      if (!/^([0-9]+(\.[0-9]+)?|\.[0-9]+)$/.test(cleanValue)) { this.toastr.error('Please enter a valid numeric or decimal value before saving.', 'Invalid Value'); return; }
      const numericValue = Number(cleanValue);
      if (!Number.isFinite(numericValue)) { this.toastr.error('Please enter a valid numeric or decimal value before saving.', 'Invalid Value'); return; }
    }
    const numericValue = key === 'section' ? 0 : Number(String(this.activeEditValue ?? '').replace(/%/g, '').trim());

    const request: UpsertOtherMetricRequest = {
      otherKpiId: kpiId,
      site: areaCode,
      kpiValue: key === this.metricColumnKey ? numericValue : (this.getCellValue(latestRow, this.metricColumnKey) ?? undefined),
      target: key === 'section' ? String(this.activeEditValue ?? '').trim() : (latestRow.section ? String(latestRow.section) : undefined),
      month: Number(this.selectedMonth),
      year: Number(this.selectedYear)
    };
    this.metricsLoading = true;
    this.otherOperatorKpiService.upsertMetric(request).subscribe({
      next: (result) => { this.metricsLoading = false; this.editingCell = { rowId: null, key: null }; this.activeEditValue = ''; this.cdr.detectChanges(); this.toastr.success('Saved successfully.', 'Success'); this.loadMetrics(); },
      error: (err) => { this.metricsLoading = false; console.error('Failed to save Other Operator KPI metric value', err); this.cdr.detectChanges(); this.toastr.error('Saving metric failed. Please try again.', 'Save Failed'); }
    });
  }

  saveAllChanges() { this.toastr.success('All changes have been saved successfully!', 'Success', { timeOut: 2500, progressBar: true }); }

  generateExcelReport() {
    this.loading = true;
    setTimeout(() => {
      this.loading = false;
      const dataToExport = this.data.map(item => {
        const row: any = {};
        this.getColumnsToRender().forEach(col => {
          const value = this.getCellValue(item, col);
          row[this.headerMapping[col] || this.optionMapping[col] || col] = col === this.metricColumnKey
            ? this.formatMetricValue(value)
            : (this.nonEditableColumns.includes(col) ? value : this.formatPercent(value));
        });
        return row;
      });
      const worksheet = XLSX.utils.json_to_sheet(dataToExport);
      const workbook = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(workbook, worksheet, 'KPI Report');
      const date = new Date().toISOString().split('T')[0];
      const filename = `KPI_Report_${date}.xlsx`;
      XLSX.writeFile(workbook, filename);
      this.toastr.success(`Excel report "${filename}" generated successfully!`, 'Report Generated');
    }, 2000);
  }

  getColumnsToRender(): string[] { return this.visibleColumns.length ? this.visibleColumns : [...this.baseColumns]; }

  getAreaKeys(): string[] {
    const keys = new Set<string>();
    this.data.forEach(item => {
      if (item.areas && typeof item.areas === 'object') Object.keys(item.areas).forEach(key => key && key !== 'UNKNOWN' && keys.add(key));
      Object.keys(item).forEach(key => { if (/^[A-Z0-9]+$/.test(key) && key.length >= 3 && key.length <= 12 && !this.baseColumns.includes(key) && key !== '_id' && key !== '__v') keys.add(key); });
    });
    const sortedKeys = Array.from(keys).sort();
    const validAreaCodes = Object.keys(this.optionMapping);
    const result = new Set<string>();
    sortedKeys.forEach(key => { if (validAreaCodes.includes(key) || validAreaCodes.some(code => this.normalizeAreaValue(code) === this.normalizeAreaValue(key))) result.add(key); });
    this.data.forEach(item => { sortedKeys.forEach(key => { const value = item[key] ?? (item.areas && item.areas[key]); if (value !== null && value !== undefined && value !== '') result.add(key); }); });
    return Array.from(result).sort();
  }

  private refreshColumnsFromData() {
    if (this.formValues.dropdown4) {
      this.visibleColumns = [...this.baseColumns, this.metricColumnKey];
      return;
    }
    this.visibleColumns = [...this.baseColumns];
  }

  private getSelectedAreaMetricValue(item: KpiData): any {
    const selectedAreaKey = this.resolveAreaCode(this.formValues.dropdown4);
    if (!selectedAreaKey) return null;

    const normalizedAreaKey = this.normalizeAreaKey(selectedAreaKey);
    if (item.areas && typeof item.areas === 'object') {
      if (item.areas[selectedAreaKey] !== undefined) return item.areas[selectedAreaKey];
      if (item.areas[normalizedAreaKey] !== undefined) return item.areas[normalizedAreaKey];
    }

    if (item[selectedAreaKey] !== undefined && item[selectedAreaKey] !== null && item[selectedAreaKey] !== '') {
      return item[selectedAreaKey];
    }

    if (item[normalizedAreaKey] !== undefined && item[normalizedAreaKey] !== null && item[normalizedAreaKey] !== '') {
      return item[normalizedAreaKey];
    }

    return null;
  }

  getLastUpdated(item: KpiData): string {
    if (item.updatedAt && item.updatedAt.$date) {
      const date = new Date(item.updatedAt.$date);
      return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    }
    return 'N/A';
  }
}
