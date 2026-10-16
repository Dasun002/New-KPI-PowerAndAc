import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ToastrService } from 'ngx-toastr';
import * as XLSX from 'xlsx';
import { ServiceFulfilmentKpiDto, ServiceFulfilmentKpiService, ServiceFulfilmentMetricDto, UpsertServiceFulfilmentMetricRequest } from '../../../../services/service-fulfilment-kpi.service';
import { RegionService, Region } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';
import { FilterUtils } from '../../../../utils/filter.utils';

interface KpiData {
  _id: { $oid: string } | number;
  kpiId?: number;
  id?: number | string;
  no: number;
  kpi: string;
  target: string;
  calculation: string;
  platform: string;
  responsibledgm: string;
  definedoladetails: string;
  weightage: string;
  datasources: string;
  areas?: { [key: string]: number };
  updatedAt?: { $date: string };
  __v?: number;
  [key: string]: any; // For dynamic area columns
}

type AdminKpiRow = ServiceFulfilmentKpiDto & { displayOrder?: number };

interface RegionData {
  id?: number;
  region: string;
  province: string;
  networkEngineer: string;
  lea: string;
  engName?: string;
}

@Component({
  selector: 'app-service-fulfilment',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './service-fulfilment.component.html',
  styleUrls: ['./service-fulfilment.component.scss']
})
export class ServiceFulfilmentComponent implements OnInit {
  pageTitle = 'Service Fulfilment';
  
  // Form values
  formValues = {
    dropdown1: '',
    dropdown2: '',
    dropdown3: '',
    dropdown4: ''
  };

  // Data
  data: KpiData[] = [];
  regionTable: RegionData[] = [];
  adminKpiRows: AdminKpiRow[] = [];
  editingCell: { rowId: string | number | null, key: string | null } = { rowId: null, key: null };
  activeEditValue = '';
  
  // Dropdown options
  dropdown2Options: string[] = [];
  dropdown3Options: string[] = [];
  dropdown4Options: string[] = [];
  engineerNameMap: Record<string, string> = {};
  
  // Columns
  visibleColumns: string[] = [];
  readonly metricColumnKey = 'kpiValue';
  
  // States
  loading = true;
  error: string | null = null;
  isEditingAllowed = true;
  userRole: string = 'User';
  private editingMessageShown = false;
  
  // Constants
  nonEditableColumns = [
    'no', 'kpi', 'target', 'calculation', 'weightage'
  ];

  baseColumns = [
    'no', 'kpi', 'target', 'calculation', 'weightage'
  ];

  headerMapping: { [key: string]: string } = {
    no: 'No',
    kpi: 'KPI',
    target: 'Target',
    calculation: 'Calculation',
    platform: 'Platform',
    responsibledgm: 'Responsible DGM',
    definedoladetails: 'Defined OLA Details',
    weightage: 'Weightage',
    datasources: 'Data Sources',
    kpiValue: 'KPI Value'
  };


  metricsRows: ServiceFulfilmentMetricDto[] = [];
  metricsLoading = false;
  metricsError: string | null = null;

  selectedMonth = new Date().getMonth() + 1;
  selectedYear = new Date().getFullYear();
  private periodLockedByUser = false;
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = [];


  constructor(
    private toastr: ToastrService,
    private serviceFulfilmentKpiService: ServiceFulfilmentKpiService,
    private regionService: RegionService,
    private authService: AuthService,
    private cdr: ChangeDetectorRef
  ) {
    this.yearOptions = FilterUtils.generatePlatformYearOptions();
  }

  toggleRoleSimulation() {
    this.userRole = this.userRole === 'PlatformAdmin' ? 'User' : 'PlatformAdmin';
    this.recomputeEditPermission();
    this.toastr.info(
      `Simulated as ${this.userRole === 'PlatformAdmin' ? 'Platform Admin' : 'User'}.`,
      'Role Simulation'
    );
  }

  ngOnInit() {
    this.loadRegionTable();
    this.loadData();
    this.checkUserRole();
    this.setupEditPermissionCheck();
  }

  loadData() {
    this.loading = true;
    this.serviceFulfilmentKpiService.getAll().subscribe({
      next: (kpis) => {
        const rows = Array.isArray(kpis) ? this.decorateAdminRows(kpis) : [];
        this.adminKpiRows = rows;
        this.syncSelectedPeriodFromData(rows);
        this.rebuildKpiMatrix();
        this.loading = false;
        this.loadMetrics();
      },
      error: (err) => {
        console.error('Failed to load Service Fulfilment admin data:', err);
        this.adminKpiRows = [];
        this.loading = false;
        this.error = 'Failed to load Service Fulfilment KPI data.';
      }
    });
  }

  loadMetrics() {
    const month = Number(this.selectedMonth);
    const year = Number(this.selectedYear);
    if (!month || !year) {
      console.warn('Service Fulfilment: Cannot load metrics - month or year not set', { month, year });
      return;
    }

    this.metricsLoading = true;
    this.metricsError = null;

    const areaFilter = this.resolveAreaCode(this.formValues.dropdown4);

    console.log('Service Fulfilment: Loading metrics', { month, year, areaFilter });

    this.serviceFulfilmentKpiService.getMetrics(month, year, areaFilter || undefined).subscribe({
      next: (metrics) => {
        console.log('Service Fulfilment: Metrics loaded', { count: metrics?.length, metrics: metrics?.slice(0, 3) });
        this.metricsRows = Array.isArray(metrics) ? metrics : [];
        
        // If we got metrics, sync the period from them (in case admin rows had wrong period)
        if (this.metricsRows.length > 0 && !this.periodLockedByUser) {
          const firstMetric = this.metricsRows[0];
          if (firstMetric.month && firstMetric.year) {
            if (firstMetric.month !== this.selectedMonth || firstMetric.year !== this.selectedYear) {
              console.log('Service Fulfilment: Syncing period from metrics', {
                from: { month: this.selectedMonth, year: this.selectedYear },
                to: { month: firstMetric.month, year: firstMetric.year }
              });
              this.selectedMonth = firstMetric.month;
              this.selectedYear = firstMetric.year;
            }
          }
        }
        
        this.metricsLoading = false;
        this.rebuildKpiMatrix();
        console.log('Service Fulfilment: KPI matrix rebuilt', { 
          dataRows: this.data.length, 
          areaKeys: this.getAreaKeys(),
          visibleColumns: this.visibleColumns 
        });
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load Service Fulfilment metrics:', err);
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
        const source = Array.isArray(res) ? res : [];
        const mapped: RegionData[] = source.map((item: any) => ({
          region: item.region ?? item.Region ?? '',
          province: item.province ?? item.Province ?? '',
          networkEngineer: item.networkEngineer ?? item.networkengineer ?? item.NetworkEngineer ?? '',
          lea: item.lea ?? item.leacode ?? item.leaCode ?? item.LEA ?? '',
          engName: item.engName ?? item.EngName ?? item.engname ?? ''
        }));
        this.regionTable = mapped;
      },
      error: (err) => {
        console.error('Failed to fetch region table from API:', err);
        this.regionTable = [];
      }
    });
  }

  checkUserRole() {
    this.userRole = this.authService.getRole() ?? 'User';
    this.recomputeEditPermission();
  }

  setupEditPermissionCheck() {
    this.checkEditPermission();
    // Check permission every minute
    setInterval(() => {
      this.checkEditPermission();
    }, 60000);
  }

  checkEditPermission(): boolean {
    // Only platform admins can edit
    this.recomputeEditPermission();
    return this.isEditingAllowed;
  }

  private recomputeEditPermission() {
    const roleAllowsEdit = this.authService.canEditPage('SERVICE FULFILMENT');
    this.isEditingAllowed = roleAllowsEdit;
  }

  getUniqueRegions(): string[] {
    return Array.from(new Set(this.regionTable.map(r => r.region))).filter(Boolean);
  }

  getMonthLabel(value: number): string {
    const month = this.monthOptions.find((option: { value: number; label: string }) => option.value === value);
    return month?.label ?? `M${value}`;
  }

  private decorateAdminRows(rows: ServiceFulfilmentKpiDto[]): AdminKpiRow[] {
    return rows.map((kpi, index) => ({
      ...kpi,
      displayOrder: this.resolveDisplayOrder(kpi, index)
    }));
  }

  private resolveDisplayOrder(kpi?: ServiceFulfilmentKpiDto | null, fallbackIndex: number = 0): number {
    if (kpi?.displayOrder && kpi.displayOrder > 0) {
      return kpi.displayOrder;
    }

    const legacyNo = (kpi as any)?.no;
    if (typeof legacyNo === 'number' && legacyNo > 0) {
      return legacyNo;
    }

    return fallbackIndex + 1;
  }

  private getIdKey(id?: number | string | null): string | null {
    if (id === null || id === undefined || id === '') {
      return null;
    }
    return String(id);
  }

  private resolveNumericId(id?: number | string | null): number | undefined {
    if (typeof id === 'number') {
      return id;
    }
    if (typeof id === 'string') {
      const parsed = Number(id);
      if (!Number.isNaN(parsed)) {
        return parsed;
      }
    }
    return undefined;
  }

  private buildRowId(sourceId: number | string | undefined, fallback: number): { $oid: string } | number {
    if (typeof sourceId === 'number') {
      return sourceId;
    }
    if (typeof sourceId === 'string' && sourceId.trim().length) {
      return { $oid: sourceId };
    }
    return fallback;
  }

  private resolveKpiIdentifier(row: KpiData): number | null {
    if (typeof row.kpiId === 'number') {
      return row.kpiId;
    }
    if (typeof row._id === 'number') {
      return row._id;
    }
    if (typeof row._id === 'object' && row._id.$oid) {
      const parsed = Number(row._id.$oid);
      if (!Number.isNaN(parsed)) {
        return parsed;
      }
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
      console.log('Service Fulfilment: No metrics, building base data from admin rows', { adminRows: this.adminKpiRows.length });
      this.data = this.buildBaseKpiDataFromAdmin();
      this.visibleColumns = [...this.baseColumns, this.metricColumnKey];
      return;
    }
    console.log('Service Fulfilment: Building KPI matrix from metrics', { metricsCount: this.metricsRows.length });
    this.data = this.buildKpiDataFromMetrics(this.metricsRows);
    this.refreshColumnsFromData();
    console.log('Service Fulfilment: Columns refreshed', { visibleColumns: this.visibleColumns.length, columns: this.visibleColumns });
  }

  private buildBaseKpiDataFromAdmin(): KpiData[] {
    if (!this.adminKpiRows.length) {
      return [];
    }

    return this.adminKpiRows.map((kpi, index) => {
      const displayOrder = this.resolveDisplayOrder(kpi, index);
      const numericId = this.resolveNumericId(kpi.id);

      return {
        _id: this.buildRowId(kpi.id, displayOrder),
        kpiId: numericId,
        no: displayOrder,
        kpi: kpi.kpi,
        target: kpi.target,
        calculation: kpi.calculation ?? '',
        platform: kpi.platform ?? '',
        responsibledgm: kpi.responsibleDgm ?? '',
        definedoladetails: this.resolveDefinedOlaValue(kpi),
        weightage: this.formatWeightageValue(kpi.weightage),
        datasources: kpi.dataSources ?? '',
        areas: {}
      };
    });
  }

  private syncSelectedPeriodFromData(rows: ServiceFulfilmentKpiDto[]) {
    if (this.periodLockedByUser || !rows || !rows.length) {
      console.log('Service Fulfilment: Period sync skipped', { 
        periodLocked: this.periodLockedByUser, 
        rowsCount: rows?.length || 0 
      });
      return;
    }

    const ordered = rows
      .filter(row => row.month > 0 && row.year > 0)
      .sort((a, b) => {
        if (a.year === b.year) {
          return a.month - b.month;
        }
        return a.year - b.year;
      });

    console.log('Service Fulfilment: Period sync from admin rows', {
      totalRows: rows.length,
      rowsWithPeriod: ordered.length,
      currentPeriod: { month: this.selectedMonth, year: this.selectedYear }
    });

    if (!ordered.length) {
      console.warn('Service Fulfilment: No admin rows with valid month/year found');
      return;
    }

    const latest = ordered[ordered.length - 1];
    const periodChanged = latest.year !== this.selectedYear || latest.month !== this.selectedMonth;

    if (periodChanged) {
      console.log('Service Fulfilment: Period changed', {
        from: { month: this.selectedMonth, year: this.selectedYear },
        to: { month: latest.month, year: latest.year }
      });
      this.selectedYear = latest.year;
      this.selectedMonth = latest.month;
    } else {
      console.log('Service Fulfilment: Period unchanged', {
        month: this.selectedMonth,
        year: this.selectedYear
      });
    }
  }

  private buildKpiDataFromMetrics(metrics: ServiceFulfilmentMetricDto[]): KpiData[] {
    const baseRows = this.buildBaseKpiDataFromAdmin();

    if (!metrics || !metrics.length) {
      return baseRows;
    }

    if (!baseRows.length) {
      return this.buildKpiDataFromMetricsFallback(metrics);
    }

    const rowsByKey = new Map<string, KpiData>();
    baseRows.forEach(row => {
      const key = this.getRowMatchKey(row);
      if (key) {
        rowsByKey.set(key, row);
      }
    });

    const extraRows: KpiData[] = [];

    metrics.forEach((metric, metricIndex) => {
      const metricKey = this.getRowMatchKey(metric) ?? `metric-${metricIndex}`;
      let targetRow = rowsByKey.get(metricKey);

      if (!targetRow) {
        targetRow = this.createRowFromMetric(metric, baseRows.length + extraRows.length + 1);
        rowsByKey.set(metricKey, targetRow);
        extraRows.push(targetRow);
      }

      this.applyMetricValueToRow(targetRow, metric);
    });

    const combined = [...baseRows, ...extraRows].sort((a, b) => a.no - b.no);
    console.log('Service Fulfilment: Built KPI data with base rows', {
      totalBaseRows: baseRows.length,
      extraRows: extraRows.length,
      metricsApplied: metrics.length
    });
    return combined;
  }

  private buildKpiDataFromMetricsFallback(metrics: ServiceFulfilmentMetricDto[]): KpiData[] {
    if (!metrics || !metrics.length) {
      return [];
    }

    const masterById = new Map<string, AdminKpiRow>();
    this.adminKpiRows.forEach((kpi, index) => {
      const decoratedOrder = this.resolveDisplayOrder(kpi, index);
      const idKey = this.getIdKey(kpi.id);
      const decorated: AdminKpiRow = {
        ...kpi,
        displayOrder: decoratedOrder
      };
      if (idKey) {
        masterById.set(idKey, decorated);
      }
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
          kpi: master?.kpi ?? metric.kpi ?? '',
          target: master?.target ?? metric.target ?? '',
          calculation: master?.calculation ?? '',
          platform: master?.platform ?? metric.platform ?? '',
          responsibledgm: master?.responsibleDgm ?? metric.responsibleDgm ?? '',
          definedoladetails: this.resolveDefinedOlaValue(master) || this.resolveDefinedOlaValue(metric),
          weightage: this.formatWeightageValue(master?.weightage ?? metric.weightage),
          datasources: master?.dataSources ?? '',
          areas: {}
        });
      }

      const row = grouped.get(groupKey)!;
      this.applyMetricValueToRow(row, metric);
    });

    return Array.from(grouped.values()).sort((a, b) => a.no - b.no);
  }

  private normalizeAreaValue(value?: string | null): string {
    return value ? value.replace(/[^A-Za-z0-9]/g, '').toUpperCase() : '';
  }

  private getRowMatchKey(source: Partial<KpiData | AdminKpiRow | ServiceFulfilmentMetricDto> | null | undefined): string | null {
    if (!source) {
      return null;
    }
    const numericKey = this.resolveNumericId((source as any).kpiId ?? (source as any).id ?? undefined);
    if (numericKey !== undefined) {
      return `num:${numericKey}`;
    }

    const rawId = (source as any).id ?? (source as any)._id;
    if (typeof rawId === 'string' && rawId.trim()) {
      return `str:${rawId.trim()}`;
    }
    if (typeof rawId === 'number' && !Number.isNaN(rawId)) {
      return `num:${rawId}`;
    }
    if (rawId && typeof rawId === 'object' && typeof rawId.$oid === 'string') {
      return `oid:${rawId.$oid}`;
    }
    return null;
  }

  private createRowFromMetric(metric: ServiceFulfilmentMetricDto, fallbackOrder: number): KpiData {
    const numericId = this.resolveNumericId(metric.id);
    return {
      _id: this.buildRowId(metric.id, fallbackOrder),
      kpiId: numericId,
      no: fallbackOrder,
      kpi: metric.kpi ?? '',
      target: metric.target ?? '',
      calculation: '',
      platform: metric.platform ?? '',
      responsibledgm: metric.responsibleDgm ?? '',
      definedoladetails: this.resolveDefinedOlaValue(metric),
      weightage: this.formatWeightageValue(metric.weightage),
      datasources: '',
      areas: {}
    };
  }

  private applyMetricValueToRow(row: KpiData, metric: ServiceFulfilmentMetricDto) {
    const areaCode = metric.area ? metric.area.trim().toUpperCase() : '';
    const areaKey = this.normalizeAreaKey(areaCode);

    if (!row.areas) {
      row.areas = {};
    }

    row.areas[areaKey] = metric.kpiValue;
    row[areaKey] = metric.kpiValue;

    if (areaCode && areaCode !== areaKey) {
      row.areas[areaCode] = metric.kpiValue;
      row[areaCode] = metric.kpiValue;
    }

    if (this.formValues.dropdown4 && this.formValues.dropdown4 !== areaKey && this.formValues.dropdown4 !== areaCode) {
      row.areas[this.formValues.dropdown4] = metric.kpiValue;
      row[this.formValues.dropdown4] = metric.kpiValue;
    }
  }

  private resolveAreaCode(value?: string | null): string {
    const normalized = this.normalizeAreaValue(value);
    if (!normalized) {
      return '';
    }

    return normalized;
  }

  private normalizeAreaKey(area?: string | null): string {
    const resolved = this.resolveAreaCode(area);
    return resolved || 'UNKNOWN';
  }

  private formatWeightageValue(value?: number | string | null): string {
    if (value === undefined || value === null) {
      return '';
    }
    if (typeof value === 'number') {
      return `${value}%`;
    }
    const clean = value.toString().trim();
    return clean.endsWith('%') ? clean : `${clean}%`;
  }

  private resolveDefinedOlaValue(source?: Partial<ServiceFulfilmentKpiDto> | Partial<ServiceFulfilmentMetricDto> | null): string {
    if (!source) {
      return '';
    }
    const raw = (source as any).definedoladetails ?? (source as any).defineDoladetails ?? '';
    return typeof raw === 'string' ? raw : '';
  }

  updateDropdown2Options() {
    if (!this.formValues.dropdown1) {
      this.dropdown2Options = [];
      this.formValues.dropdown2 = '';
      return;
    }
    const provinces = Array.from(
      new Set(
        this.regionTable
          .filter(x => x.region === this.formValues.dropdown1)
          .map(x => x.province)
      )
    ).filter(Boolean);
    this.dropdown2Options = provinces;
    this.formValues.dropdown2 = '';
    this.dropdown3Options = [];
    this.formValues.dropdown3 = '';
    this.dropdown4Options = [];
    this.formValues.dropdown4 = '';
    this.visibleColumns = [...this.baseColumns];
  }

  updateDropdown3Options() {
    if (!this.formValues.dropdown2 || !this.formValues.dropdown1) {
      this.dropdown3Options = [];
      this.formValues.dropdown3 = '';
      return;
    }
    const engineers = Array.from(
      new Set(
        this.regionTable
          .filter(x => 
            x.region === this.formValues.dropdown1 && 
            x.province === this.formValues.dropdown2
          )
          .map(x => x.networkEngineer)
      )
    ).filter(Boolean);
    this.dropdown3Options = engineers;
    
    this.engineerNameMap = {};
    this.dropdown3Options.forEach(eng => {
      const regionRec = this.regionTable.find(x => x.networkEngineer === eng && x.engName);
      this.engineerNameMap[eng] = regionRec?.engName ? `${eng} [${regionRec.engName}]` : eng;
    });

    this.formValues.dropdown3 = '';
    this.dropdown4Options = [];
    this.formValues.dropdown4 = '';
    this.visibleColumns = [...this.baseColumns];
  }

  updateDropdown4Options() {
    if (!this.formValues.dropdown3 || !this.formValues.dropdown1 || !this.formValues.dropdown2) {
      this.dropdown4Options = [];
      this.formValues.dropdown4 = '';
      this.loadMetrics();
      this.recomputeEditPermission();
      return;
    }
    const leas = this.regionTable
      .filter(x => 
        x.region === this.formValues.dropdown1 &&
        x.province === this.formValues.dropdown2 &&
        x.networkEngineer === this.formValues.dropdown3
      )
      .map(x => this.resolveAreaCode(x.lea))
      .filter(code => !!code);

    this.dropdown4Options = Array.from(new Set(leas));
    this.formValues.dropdown4 = '';
    this.visibleColumns = [...this.baseColumns];
    this.loadMetrics();
    this.recomputeEditPermission();
  }

  updateVisibleColumns() {
    this.refreshColumnsFromData();
  }

  onDropdownChange(field: string, value: string) {
    const normalizedValue = field === 'dropdown4' ? this.resolveAreaCode(value) : value;
    (this.formValues as any)[field] = normalizedValue;
    
    switch (field) {
      case 'dropdown1':
        this.formValues.dropdown2 = '';
        this.formValues.dropdown3 = '';
        this.formValues.dropdown4 = '';
        this.dropdown3Options = [];
        this.dropdown4Options = [];
        this.visibleColumns = [...this.baseColumns];
        this.updateDropdown2Options();
        this.loadMetrics();
        this.recomputeEditPermission();
        break;
      case 'dropdown2':
        this.formValues.dropdown3 = '';
        this.formValues.dropdown4 = '';
        this.dropdown4Options = [];
        this.visibleColumns = [...this.baseColumns];
        this.updateDropdown3Options();
        this.loadMetrics();
        this.recomputeEditPermission();
        break;
      case 'dropdown3':
        this.formValues.dropdown4 = '';
        this.visibleColumns = [...this.baseColumns];
        this.updateDropdown4Options();
        this.recomputeEditPermission();
        break;
      case 'dropdown4':
        this.updateVisibleColumns();
        this.loadMetrics();
        this.recomputeEditPermission();
        break;
    }
  }

  onPeriodChange() {
    this.periodLockedByUser = true;
    this.loadMetrics();
  }

  resetAreaFilter() {
    if (!this.formValues.dropdown4) {
      return;
    }
    this.formValues.dropdown4 = '';
    this.updateVisibleColumns();
    this.loadMetrics();
    this.recomputeEditPermission();
  }

  formatPercent(val: any): string {
    if (val === undefined || val === null || val === '') return '-';
    if (typeof val === 'number') {
      // Format to 2 decimal places
      return `${val.toFixed(2)}%`;
    }
    const s = String(val).trim();
    // Remove existing % and add back
    const cleanValue = s.replace(/%/g, '');
    const numValue = parseFloat(cleanValue);
    if (!isNaN(numValue)) {
      return `${numValue.toFixed(2)}%`;
    }
    return s.endsWith('%') ? s : `${s}%`;
  }

  private getSelectedAreaMetricValue(item: KpiData): any {
    const selectedAreaKey = this.resolveAreaCode(this.formValues.dropdown4);
    if (!selectedAreaKey) {
      return null;
    }

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

  getCellValue(item: KpiData, key: string): any {
    if (key === this.metricColumnKey) {
      return this.getSelectedAreaMetricValue(item);
    }

    // First check direct property
    if (item[key] !== undefined && item[key] !== null && item[key] !== '') {
      return item[key];
    }
    
    // Then check areas object with exact key
    if (item.areas && typeof item.areas === 'object' && item.areas[key] !== undefined) {
      return item.areas[key];
    }
    
    // Try to normalize key for area lookup
    const normalizedKey = this.normalizeAreaKey(key);
    if (normalizedKey && item.areas && item.areas[normalizedKey] !== undefined) {
      return item.areas[normalizedKey];
    }
    
    // Try resolving the area code (in case key is a display label)
    const resolvedKey = this.resolveAreaCode(key);
    if (resolvedKey && item.areas && item.areas[resolvedKey] !== undefined) {
      return item.areas[resolvedKey];
    }
    
    // Try direct uppercase match
    const upperKey = key.toUpperCase().trim();
    if (upperKey && item.areas && item.areas[upperKey] !== undefined) {
      return item.areas[upperKey];
    }
    
    // Debug logging for area columns that return null
    if (!this.baseColumns.includes(key) && item.areas) {
      console.warn('Service Fulfilment: Could not find value for area column', {
        key,
        normalizedKey,
        resolvedKey,
        upperKey,
        availableAreas: Object.keys(item.areas),
        availableDirectKeys: Object.keys(item).filter(k => !this.baseColumns.includes(k) && k !== '_id' && k !== '__v' && typeof item[k] === 'number')
      });
    }
    
    return null;
  }

  getRowId(item: KpiData): string | number {
    if (typeof item._id === 'object' && item._id.$oid) {
      return item._id.$oid;
    }
    return item._id as number;
  }

  isEditingCell(item: KpiData, key: string): boolean {
    return (
      this.editingCell.rowId === this.getRowId(item) &&
      this.editingCell.key === key
    );
  }

  selectMetricInput(event: Event) {
    const target = event.target as HTMLInputElement | null;
    if (!target) {
      return;
    }
    requestAnimationFrame(() => target.select());
  }

  startEdit(item: KpiData, key: string) {
    if (this.nonEditableColumns.includes(key)) {
      return;
    }

    if (!this.isEditingAllowed) {
      if (!this.editingMessageShown) {
        if (this.userRole !== 'PlatformAdmin') {
          this.toastr.error('Only Platform Admins can edit KPI values.', 'Access Denied');
        } else {
          this.toastr.info('You do not currently have edit permission on this page.', 'Edit Disabled');
        }
        this.editingMessageShown = true;
        setTimeout(() => (this.editingMessageShown = false), 3000);
      }
      return;
    }
    this.editingCell = { rowId: this.getRowId(item), key };
    const existing = this.getCellValue(item, key);
    this.activeEditValue = existing === null || existing === undefined ? '' : String(existing);
  }

  cancelEdit() {
    this.editingCell = { rowId: null, key: null };
    this.activeEditValue = '';
  }

  saveEdit(item: KpiData, key: string) {
    if (!this.isEditingAllowed) {
      this.toastr.error('You do not have permission to update this KPI value.', 'Permission Denied');
      return;
    }

    const areaCode = this.resolveAreaCode(this.formValues.dropdown4);
    if (!areaCode) {
      this.toastr.error('Unable to determine the selected RTOM area. Please pick an area and try again.', 'Invalid Area');
      return;
    }

    const rowId = this.getRowId(item);
    const latestRow = this.data.find(row => this.getRowId(row) === rowId) ?? item;
    const kpiId = this.resolveKpiIdentifier(latestRow);
    if (kpiId === null) {
      this.toastr.error('Unable to resolve the KPI identifier for this row.', 'Missing KPI Id');
      return;
    }
    const numericValue = parseFloat(String(this.activeEditValue ?? '').replace(/%/g, '').trim());

    if (isNaN(numericValue)) {
      this.toastr.error('Please enter a valid numeric value before saving.', 'Invalid Value');
      return;
    }

    const request: UpsertServiceFulfilmentMetricRequest = {
      serviceFulfilmentKpiId: kpiId,
      areaCode,
      kpiValue: numericValue,
      month: Number(this.selectedMonth),
      year: Number(this.selectedYear)
    };

    this.metricsLoading = true;
    this.serviceFulfilmentKpiService.upsertMetric(request).subscribe({
      next: () => {
        this.metricsLoading = false;
        this.editingCell = { rowId: null, key: null };
        this.activeEditValue = '';
        this.cdr.detectChanges();
        this.toastr.success(`Saved ${areaCode} metric successfully.`, 'Success');
        this.loadMetrics();
      },
      error: (err) => {
        this.metricsLoading = false;
        console.error('Failed to save Service Fulfilment metric value', err);
        this.cdr.detectChanges();
        this.toastr.error('Saving metric failed. Please try again.', 'Save Failed');
      }
    });
  }

  saveAllChanges() {
    if (!this.isEditingAllowed) {
      this.toastr.error('You do not have permission to edit. Only Platform Admins can edit KPI data.', 'Permission Denied');
      return;
    }

    this.loading = true;
    
    // Simulate API call
    setTimeout(() => {
      this.loading = false;
      this.editingCell = { rowId: null, key: null };
      this.toastr.success('✅ All changes have been saved successfully!', 'Success', {
        timeOut: 2500,
        progressBar: true
      });
    }, 1500);
  }

  generateExcelReport() {
    this.loading = true;
    
    setTimeout(() => {
      this.loading = false;
      
      // Create Excel workbook
      const dataToExport = this.data.map(item => {
        const row: any = {};
        
        // Add all columns
        this.getColumnsToRender().forEach(col => {
          const value = this.getCellValue(item, col);
          row[this.headerMapping[col] || col] = 
            this.nonEditableColumns.includes(col) ? value : this.formatPercent(value);
        });
        
        return row;
      });
      
      // Convert to Excel
      const worksheet = XLSX.utils.json_to_sheet(dataToExport);
      const workbook = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(workbook, worksheet, 'KPI Report');
      
      // Generate and download
      const date = new Date().toISOString().split('T')[0];
      const filename = `KPI_Report_${date}.xlsx`;
      XLSX.writeFile(workbook, filename);
      
      this.toastr.success(`Excel report "${filename}" generated successfully!`, 'Report Generated');
    }, 2000);
  }

  getColumnsToRender(): string[] {
    if (this.visibleColumns.length) {
      return this.visibleColumns;
    }
    return [...this.baseColumns];
  }

  getAreaKeys(): string[] {
    const keys = new Set<string>();
    this.data.forEach(item => {
      // Check areas object first
      if (item.areas && typeof item.areas === 'object') {
        Object.keys(item.areas).forEach(key => {
          if (key && key !== 'UNKNOWN') {
            keys.add(key);
          }
        });
      }
      // Also check direct properties that look like area codes
      Object.keys(item).forEach(key => {
        // Match area codes: uppercase letters, 3-12 characters, not in base columns
        if (/^[A-Z0-9]+$/.test(key) && key.length >= 3 && key.length <= 12 && !this.baseColumns.includes(key) && key !== '_id' && key !== '__v') {
          keys.add(key);
        }
      });
    });
    
    // Sort area keys for consistent display
    const sortedKeys = Array.from(keys).sort();
    
    const result = new Set<string>();
    
    // Also add any keys that have actual data (non-null values)
    this.data.forEach(item => {
      sortedKeys.forEach(key => {
        const value = item[key] ?? (item.areas && item.areas[key]);
        if (value !== null && value !== undefined && value !== '') {
          result.add(key);
        }
      });
    });
    
    return Array.from(result).sort();
  }

  private refreshColumnsFromData() {
    if (this.formValues.dropdown4) {
      this.visibleColumns = [...this.baseColumns, this.metricColumnKey];
      return;
    }
    this.visibleColumns = [...this.baseColumns];
  }

  getLastUpdated(item: KpiData): string {
    if (item.updatedAt && item.updatedAt.$date) {
      const date = new Date(item.updatedAt.$date);
      return date.toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      });
    }
    return 'N/A';
  }
}