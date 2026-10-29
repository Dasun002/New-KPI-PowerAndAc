import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
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
import { buildAreaLabels, buildEngineerDisplayMap, getScopedLeas, mapRegionRecords } from '../../../../utils/region-display.utils';
import { environment } from '../../../../../environments/environment';

interface KpiData {
  _id: { $oid: string } | number;
  kpiId?: number;
  id?: number | string;
  no: number;
  networkEngineerKpi: string;
  division: string;
  section: string;
  kpiPercent: string;
  // Per-area values keyed by area code: achievement, target, and the site string stored on the metric row.
  areas?: { [key: string]: number };
  areaTargets?: { [key: string]: string };
  areaSites?: { [key: string]: string };
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

interface RtomAreaLookup {
  areaCode?: string;
  displayName?: string;
  AreaCode?: string;
  DisplayName?: string;
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

  formValues = { dropdown1: '', dropdown2: '', dropdown3: '' };

  data: KpiData[] = [];
  regionTable: RegionData[] = [];
  adminKpiRows: AdminKpiRow[] = [];

  dropdown2Options: string[] = [];
  dropdown3Options: string[] = [];
  engineerNameMap: Record<string, string> = {};

  // Database-backed area columns and NW EE header groups.
  areaKeys: string[] = [];
  areaLabels: Record<string, string> = {};
  areaEngineerGroups: { label: string; areaKeys: string[] }[] = [];

  loading = true;
  error: string | null = null;
  isEditingAllowed = true;
  userRole: string = 'User';

  nonEditableColumns = ['no', 'networkEngineerKpi', 'division', 'kpiPercent'];
  baseColumns = ['no', 'networkEngineerKpi', 'division', 'section', 'kpiPercent'];

  headerMapping: { [key: string]: string } = {
    no: 'No',
    networkEngineerKpi: 'Network Engineer KPI',
    division: 'Category',
    section: 'Target',
    kpiPercent: 'Weightage',
    kpiValue: 'Achievement (%)'
  };

  private databaseAreaKeyByCode: Record<string, string> = {};
  private databaseAreaKeysByLabel: Record<string, string[]> = {};
  private databaseAreaCodeByKey: Record<string, string> = {};
  private databaseAreaLabelByKey: Record<string, string> = {};

  metricsRows: OtherMetricDto[] = [];
  metricsLoading = false;
  metricsError: string | null = null;

  // Inline edit state for one KPI row + area cell.
  editingAreaKey: string | null = null;
  areaEdit: { kpiValue: string; target: string } = { kpiValue: '', target: '' };

  selectedMonth = new Date().getMonth() + 1;
  selectedYear = new Date().getFullYear();
  private periodLockedByUser = false;
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = [];

  constructor(
    private http: HttpClient,
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
    this.loadRtomAreaLookup();
    this.checkUserRole();
    this.setupEditPermissionCheck();
  }

  get groupedAreaKeys(): string[] {
    return this.areaEngineerGroups.flatMap(group => group.areaKeys);
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

  // Loads every site's metrics for the period; the site filter is omitted on purpose.
  loadMetrics() {
    const month = Number(this.selectedMonth);
    const year = Number(this.selectedYear);
    if (!month || !year) return;

    this.metricsLoading = true;
    this.metricsError = null;

    this.otherOperatorKpiService.getMetrics(month, year).subscribe({
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
        this.engineerNameMap = buildEngineerDisplayMap(this.regionTable);
        this.refreshAreaKeys();
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
    this.data = this.metricsRows.length
      ? this.buildKpiDataFromMetrics(this.metricsRows)
      : this.buildBaseKpiDataFromAdmin();
    this.refreshAreaKeys();
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
        areas: {},
        areaTargets: {},
        areaSites: {}
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
        // update kpiPercent from metric (month/year-aware values)
        targetRow.kpiPercent = this.formatWeightageValue(metric.kpiPercent ?? null) || targetRow.kpiPercent;
      }
      this.applyMetricValueToRow(targetRow, metric);
    });

    const rows = [...baseRows, ...extraRows].sort((a, b) => a.no - b.no);
    rows.forEach(row => this.applySharedTarget(row));
    return rows;
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
          section: '',
          kpiPercent: this.formatWeightageValue(metric.kpiPercent ?? master?.kpiPercent),
          areas: {},
          areaTargets: {},
          areaSites: {}
        });
      }
      this.applyMetricValueToRow(grouped.get(groupKey)!, metric);
    });
    const rows = Array.from(grouped.values()).sort((a, b) => a.no - b.no);
    rows.forEach(row => this.applySharedTarget(row));
    return rows;
  }

  private normalizeAreaValue(value?: string | null): string {
    return value ? value.replace(/[^A-Za-z0-9]/g, '').toUpperCase() : '';
  }

  private loadRtomAreaLookup(): void {
    this.http.get<RtomAreaLookup[]>(`${environment.apiUrl}/rtom-areas`).subscribe({
      next: areas => {
        this.databaseAreaKeyByCode = {};
        this.databaseAreaKeysByLabel = {};
        this.databaseAreaCodeByKey = {};
        this.databaseAreaLabelByKey = {};

        (areas ?? []).forEach(area => {
          const code = (area.areaCode ?? area.AreaCode)?.trim();
          if (!code) return;
          const key = this.normalizeAreaValue(code);
          if (!key) return;
          this.databaseAreaKeyByCode[key] = key;
          this.databaseAreaCodeByKey[key] = code;

          const label = (area.displayName ?? area.DisplayName)?.trim();
          if (!label) return;
          const normalizedLabel = this.normalizeAreaValue(label);
          if (normalizedLabel) {
            const keys = this.databaseAreaKeysByLabel[normalizedLabel] ?? [];
            if (!keys.includes(key)) keys.push(key);
            this.databaseAreaKeysByLabel[normalizedLabel] = keys;
          }
          this.databaseAreaLabelByKey[key] = label;
        });

        if (!areas?.length) {
          console.error('RTOM area lookup returned no database records.');
          this.toastr.error('RTOM area lookup is empty; using region LEA codes and stored sites.', 'Area Data');
        }
        this.rebuildKpiMatrix();
      },
      error: error => {
        console.error('Failed to fetch RTOM area lookup from API:', error);
        this.toastr.error('RTOM area labels could not be loaded; using region LEA codes.', 'Area Data');
        this.refreshAreaKeys();
      }
    });
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
      section: '',
      kpiPercent: this.formatWeightageValue(metric.kpiPercent),
      areas: {},
      areaTargets: {},
      areaSites: {}
    };
  }

  // Stores one metric under its own area code, keeping the stored site string for later saves.
  private applyMetricValueToRow(row: KpiData, metric: OtherMetricDto) {
    const areaKey = this.normalizeAreaKey(metric.site);
    row.areas = { ...(row.areas ?? {}), [areaKey]: metric.kpiValue ?? 0 };
    row.areaTargets = { ...(row.areaTargets ?? {}), [areaKey]: metric.target ?? '' };
    row.areaSites = { ...(row.areaSites ?? {}), [areaKey]: metric.site };
  }

  // Shows a target in the shared Target column only when every area reports the same one.
  private applySharedTarget(row: KpiData) {
    const targets = Array.from(new Set(Object.values(row.areaTargets ?? {}).filter(Boolean)));
    if (targets.length === 1) row.section = targets[0];
  }

  private resolveAreaCode(value?: string | null): string {
    const normalized = this.normalizeAreaValue(value);
    if (!normalized) return '';
    const codeMatch = this.databaseAreaKeyByCode[normalized];
    if (codeMatch) return codeMatch;
    const labelMatches = this.databaseAreaKeysByLabel[normalized];
    if (labelMatches?.length === 1) return labelMatches[0];
    return normalized;
  }

  private normalizeAreaKey(area?: string | null): string {
    const resolved = this.resolveAreaCode(area);
    return resolved || 'UNKNOWN';
  }

  // Rebuild columns and NW EE groups from region assignments and the selected-period metrics.
  private refreshAreaKeys() {
    const { dropdown1, dropdown2, dropdown3 } = this.formValues;
    const scopedRegions = this.regionTable.filter(region =>
      (!dropdown1 || region.region === dropdown1)
      && (!dropdown2 || region.province === dropdown2)
      && (!dropdown3 || region.networkEngineer === dropdown3)
    );
    const engineerByAreaKey = new Map<string, string>();
    const labelsByAreaKey = new Map<string, string>();
    this.regionTable.forEach(region => {
      const key = this.resolveAreaCode(region.lea);
      if (!key) return;
      if (!engineerByAreaKey.has(key)) {
        engineerByAreaKey.set(key, region.networkEngineer?.trim() || 'Unassigned NW EE');
      }
      if (!labelsByAreaKey.has(key)) {
        labelsByAreaKey.set(key, this.databaseAreaLabelByKey[key] || region.lea || key);
      }
    });
    const keys = new Set(
      getScopedLeas(this.regionTable, { region: dropdown1, province: dropdown2, engineer: dropdown3 })
        .map(lea => this.resolveAreaCode(lea))
        .filter(Boolean)
    );
    if (!dropdown1) {
      this.metricsRows.forEach(metric => {
        const key = this.normalizeAreaKey(metric.site);
        if (key !== 'UNKNOWN') keys.add(key);
      });
    }
    const labelOf = (key: string) => labelsByAreaKey.get(key) || this.databaseAreaLabelByKey[key] || key;
    this.areaKeys = Array.from(keys).sort((a, b) => labelOf(a).localeCompare(labelOf(b)));
    this.areaLabels = buildAreaLabels(this.areaKeys, labelOf);

    const groups = new Map<string, Set<string>>();
    const assigned = new Set<string>();
    scopedRegions.forEach(region => {
      const key = this.resolveAreaCode(region.lea);
      if (!key || !keys.has(key) || assigned.has(key)) return;
      const engineer = region.networkEngineer?.trim() || 'Unassigned NW EE';
      if (!groups.has(engineer)) groups.set(engineer, new Set<string>());
      groups.get(engineer)!.add(key);
      assigned.add(key);
    });
    const grouped = new Set(Array.from(groups.values()).flatMap(group => Array.from(group)));
    const unassigned = this.areaKeys.filter(key => !grouped.has(key));
    if (unassigned.length) groups.set('Unassigned NW EE', new Set(unassigned));
    this.areaEngineerGroups = Array.from(groups.entries())
      .map(([engineer, areaKeys]) => ({
        label: this.engineerNameMap[engineer] || engineer,
        areaKeys: this.areaKeys.filter(key => areaKeys.has(key))
      }))
      .filter(group => group.areaKeys.length > 0)
      .sort((a, b) => a.label.localeCompare(b.label));
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
  }

  updateDropdown3Options() {
    if (!this.formValues.dropdown2 || !this.formValues.dropdown1) { this.dropdown3Options = []; this.formValues.dropdown3 = ''; return; }
    const engineers = Array.from(new Set(this.regionTable.filter(x => x.region === this.formValues.dropdown1 && x.province === this.formValues.dropdown2).map(x => x.networkEngineer))).filter(Boolean);
    this.dropdown3Options = engineers;

    this.engineerNameMap = buildEngineerDisplayMap(this.regionTable, this.dropdown3Options);

    this.formValues.dropdown3 = '';
  }

  // Region filters only narrow the area columns; all area metrics for the period are already loaded.
  onDropdownChange(field: string, value: string) {
    (this.formValues as any)[field] = value;
    switch (field) {
      case 'dropdown1': this.formValues.dropdown2 = ''; this.formValues.dropdown3 = ''; this.dropdown3Options = []; this.updateDropdown2Options(); break;
      case 'dropdown2': this.formValues.dropdown3 = ''; this.updateDropdown3Options(); break;
    }
    this.cancelAreaEdit();
    this.refreshAreaKeys();
    this.recomputeEditPermission();
  }

  onPeriodChange() { this.periodLockedByUser = true; this.cancelAreaEdit(); this.loadMetrics(); }

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
    const value = item[key];
    return value === undefined || value === null || value === '' ? null : value;
  }

  getAreaValue(item: KpiData, areaKey: string): any {
    return item.areas?.[areaKey] ?? null;
  }

  getAreaTarget(item: KpiData, areaKey: string): string {
    return item.areaTargets?.[areaKey] ?? '';
  }

  getRowId(item: KpiData): string | number { return typeof item._id === 'object' && item._id.$oid ? item._id.$oid : item._id as number; }

  private areaEditKey(row: KpiData, areaKey: string): string { return `${this.getRowId(row)}|${areaKey}`; }

  isEditingArea(row: KpiData, areaKey: string): boolean {
    return this.editingAreaKey === this.areaEditKey(row, areaKey);
  }

  enterAreaEdit(row: KpiData, areaKey: string) {
    if (!this.isEditingAllowed) { this.toastr.info('You do not have edit permission on this page.', 'Edit Disabled'); return; }
    const kv = this.getAreaValue(row, areaKey);
    this.areaEdit = {
      kpiValue: kv !== null && kv !== undefined ? String(kv) : '',
      target: this.getAreaTarget(row, areaKey)
    };
    this.editingAreaKey = this.areaEditKey(row, areaKey);
  }

  cancelAreaEdit() {
    this.editingAreaKey = null;
    this.areaEdit = { kpiValue: '', target: '' };
  }

  // Upserts achievement and target for one KPI in one area, using the area's stored site when it already exists.
  saveArea(row: KpiData, areaKey: string) {
    if (!this.isEditingAllowed) { this.toastr.error('You do not have permission to update KPI values.', 'Permission Denied'); return; }
    const site = row.areaSites?.[areaKey] || this.databaseAreaCodeByKey[areaKey] || areaKey;
    if (!site) { this.toastr.error('Unable to determine the RTOM area for this value.', 'Invalid Area'); return; }
    const kpiId = this.resolveKpiIdentifier(row);
    if (kpiId === null) { this.toastr.error('Unable to resolve the KPI identifier for this row.', 'Missing KPI Id'); return; }

    const kpiValueStr = String(this.areaEdit.kpiValue ?? '').replace(/%/g, '').trim();
    const kpiValue = kpiValueStr === '' ? undefined : Number(kpiValueStr);
    if (kpiValue !== undefined && !Number.isFinite(kpiValue)) {
      this.toastr.error('Please enter a valid numeric value for Achievement.', 'Invalid Value'); return;
    }

    const request: UpsertOtherMetricRequest = {
      otherKpiId: kpiId,
      site,
      kpiValue: kpiValue ?? undefined,
      target: String(this.areaEdit.target ?? '').trim() || undefined,
      month: Number(this.selectedMonth),
      year: Number(this.selectedYear)
    };

    this.metricsLoading = true;
    this.otherOperatorKpiService.upsertMetric(request).subscribe({
      next: () => {
        this.metricsLoading = false;
        this.cancelAreaEdit();
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

  saveAllChanges() { this.toastr.success('All changes have been saved successfully!', 'Success', { timeOut: 2500, progressBar: true }); }

  generateExcelReport() {
    this.loading = true;
    setTimeout(() => {
      this.loading = false;
      const dataToExport = this.data.map(item => {
        const row: any = {};
        this.baseColumns.forEach(col => {
          const value = this.getCellValue(item, col);
          row[this.headerMapping[col] || col] = this.nonEditableColumns.includes(col) ? value : this.formatPercent(value);
        });
        this.areaKeys.forEach(area => {
          const label = this.areaLabels[area] || area;
          const engineer = this.areaEngineerGroups.find(group => group.areaKeys.includes(area))?.label;
          const heading = engineer ? `${engineer} - ${label}` : label;
          row[`${heading} ${this.headerMapping['kpiValue']}`] = this.formatMetricValue(this.getAreaValue(item, area));
          row[`${heading} Target`] = this.getAreaTarget(item, area) || '-';
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

  getLastUpdated(item: KpiData): string {
    if (item.updatedAt && item.updatedAt.$date) {
      const date = new Date(item.updatedAt.$date);
      return date.toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
    }
    return 'N/A';
  }
}
