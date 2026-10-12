import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule, HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as ExcelJS from 'exceljs';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';

/* ================= TYPES ================= */

type RoutineRecord = {
  _id?: number;  // Changed from string to number (int identity)
  no?: number;
  kpi?: string;
  target?: string;
  calculation?: string;
  platform?: string;
  responsibleDGM?: string;
  definedOLADetails?: string;
  dataSources?: string;
};

type PlatformDetail = {
  Column1: string;
  column2?: number | string;
  column3?: number | string;
  Column4?: number | string;
  id?: number;
  isVerified?: boolean;
  scheduled?: number | string;
  attended?: number | string;
};

type PlatformRecord = {
  month: string;
  data: {
    [key: string]: PlatformDetail;
  };
  details?: any[];   // optional (since it exists in API)
};

type PlatformKey = 'msan' | 'vpn' | 'slbn';

type PlaceholderMap = Record<string, string>;
type TowerSumRecord = Record<string, number>;

type PlatformTableConfig = {
  key: PlatformKey;
  title: string;
  monthsLimit: number;
};

type MaintenanceRow = {
  routine: RoutineRecord;
  platformKey: PlatformKey | null;
};

/* ================= CONSTANTS ================= */

const PLATFORM_COLUMNS = [
  'NW/WPC-1', 'NW/WPC-2', 'NW/WPNE', 'NW/WPSW', 'NW/WPSE',
  'NW/WPE', 'NW/WPN', 'NW/NWPE', 'NW/NWPW', 'NW/CPN',
  'NW/CPS', 'NW/NCP', 'NW/UVA', 'NW/SAB', 'NW/SPE',
  'NW/SPW', 'NW/WPS', 'NW/EP', 'NW/NP-1', 'NW/NP-2'
];

const MONTH_NAMES = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
];

/* ================= COMPONENT ================= */
type ApiResponse = {
  msan: PlatformRecord[];
  vpn: PlatformRecord[];
  slbn: PlatformRecord[];
  routine: RoutineRecord[];
};

import { AuthService } from '../../../../services/auth.service';
import { Region, RegionService } from '../../../../services/region.service';
import { FilterUtils } from '../../../../utils/filter.utils';
import {
  buildEngineerDisplayLookupMap,
  mapRegionRecords,
  normalizeLookupKey
} from '../../../../utils/region-display.utils';

@Component({
  selector: 'app-routine-mtnc',
  standalone: true,
  imports: [CommonModule, HttpClientModule, FormsModule],
  templateUrl: './routine-mtnc.component.html',
  styleUrls: ['./routine-mtnc.component.scss']
})
export class RoutineMtncComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly authService = inject(AuthService);
  private readonly regionService = inject(RegionService);

  // Checks if the user is authorized to edit the metrics on the page.
  get canEditMetrics(): boolean {
    return this.authService.canEditPage('Routine Maintenance')
      || this.authService.canEditPage('Routine MTNC')
      || this.authService.canEditPage('RoutineMtnc');
  }

  pageTitle = 'Routine Maintenance';
  heroSubtitle = 'Monitor routine maintenance schedules, node-level compliance, and performance metrics across platforms.';

  readonly columns = PLATFORM_COLUMNS;
  readonly combinedTableStaticColumns = 4;
  engineerNameMap: Record<string, string> = {};

  readonly platformConfigs: PlatformTableConfig[] = [
    { key: 'msan', title: 'MSAN Data Table', monthsLimit: 6 },
    { key: 'vpn', title: 'VPN Data Table', monthsLimit: 2 },
    { key: 'slbn', title: 'SLBN Data Table', monthsLimit: 2 }
  ];

  loading = false;
  errorMessage = '';

  routineData: RoutineRecord[] = [];

  platformDataMap: Record<PlatformKey, PlatformRecord[]> = {
    msan: [],
    vpn: [],
    slbn: []
  };

  placeholderMap: Record<PlatformKey, PlaceholderMap> = {
    msan: this.buildDefaultPlaceholders(),
    vpn: this.buildDefaultPlaceholders(),
    slbn: this.buildDefaultPlaceholders()
  };

  towerSumsMap: Record<PlatformKey, TowerSumRecord> = {
    msan: {},
    vpn: {},
    slbn: {}
  };

  /* ===================== FILTER STATE ===================== */

  private readonly now = new Date();

  selectedMonth: number = this.now.getMonth() + 1;   // 1-indexed (1 = January)
  selectedYear: number = this.now.getFullYear();
  private periodLockedByUser = false;
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = FilterUtils.generatePlatformYearOptions();

  /* -------------------- */

  // Initializes the component by loading region data and fetching KPI metrics.
  ngOnInit(): void {
    this.loadEngineerNames();
    this.fetchData();
  }

  /* ===================== FILTER HANDLERS ===================== */

  // Handles changes to the month dropdown filter.
  onMonthChange(month: number): void {
    this.selectedMonth = Number(month);
    // Request filtered data from backend for selected month and year
    this.fetchData();
  }

  // Handles changes to the year dropdown filter.
  onYearChange(year: number): void {
    this.selectedYear = Number(year);
    this.fetchData();
  }

  // Recalculates placeholders and tower sums based on the latest data.
  private applyFiltersAndRecalculate(): void {
    (['msan', 'vpn', 'slbn'] as PlatformKey[]).forEach(key => {
      this.placeholderMap[key] = this.calculatePlaceholderValues(this.platformDataMap[key], key);
      const cfg = this.platformConfigs.find(c => c.key === key);
      this.towerSumsMap[key] = cfg
        ? this.calculateTowerSums(this.platformDataMap[key], cfg.monthsLimit)
        : {};
    });
    this.cdr.detectChanges();
  }

  /* ================= GETTERS ================= */

  // Maps the raw routine data into rows with their determined platform keys.
  get maintenanceRows(): MaintenanceRow[] {
    return this.routineData.map(routine => {
      const kpiLower = (routine.kpi ?? '').toLowerCase();
      const platformLower = (routine.platform ?? '').toLowerCase();
      const combined = `${kpiLower}|${platformLower}`.replace(/[\s&]/g, '');
      let platformKey: PlatformKey | null = null;

      if (combined.includes('slbn') || combined.includes('slb')) {
        platformKey = 'slbn';
      } else if (combined.includes('ipnw') || combined.includes('vpn')) {
        platformKey = 'vpn';
      } else if (combined.includes('msan') || combined.includes('olte')) {
        platformKey = 'msan';
      }

      return { routine, platformKey };
    });
  }

  // Calculates the combined colspan for the table header based on static and dynamic columns.
  get combinedTableColspan(): number {
    return this.combinedTableStaticColumns + this.columns.length;
  }

  /* ================= API ================= */

  // Fetches MSAN, VPN, SLBN and Routine KPI data concurrently from the backend API.
  fetchData(): void {
    this.loading = true;
    this.errorMessage = '';

    forkJoin({
      msan: this.http.get<PlatformRecord[]>(`${environment.apiUrl}/multi-table/fetchMsan?year=${this.selectedYear}`).pipe(catchError(() => of([]))),
      vpn: this.http.get<PlatformRecord[]>(`${environment.apiUrl}/multi-table/fetchVpn?year=${this.selectedYear}`).pipe(catchError(() => of([]))),
      slbn: this.http.get<PlatformRecord[]>(`${environment.apiUrl}/multi-table/fetchSlbn?year=${this.selectedYear}`).pipe(catchError(() => of([]))),
      routine: this.http.get<RoutineRecord[]>(`${environment.apiUrl}/mtnc-routine`).pipe(
        catchError((err: HttpErrorResponse) => {
          console.error(err.message);
          this.setError('Unable to load routine KPI definitions.');
          return of([]);
        })
      )
    })
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe((response: ApiResponse) => {
        const { msan, vpn, slbn, routine } = response;
        this.platformDataMap = { msan, vpn, slbn };
        this.routineData = routine ?? [];

        console.log('SLBN DATA', this.platformDataMap['slbn']);

        (['msan', 'vpn', 'slbn'] as PlatformKey[]).forEach(key => {
          this.placeholderMap[key] = this.calculatePlaceholderValues(this.platformDataMap[key], key);
          const cfg = this.platformConfigs.find(c => c.key === key);
          this.towerSumsMap[key] = cfg
            ? this.calculateTowerSums(this.platformDataMap[key], cfg.monthsLimit)
            : {};
        });
      });
  }

  /* ================= TEMPLATE METHODS ================= */

  // Retrieves the loaded records for the specified platform key.
  getPlatformRecords(key: PlatformKey): PlatformRecord[] {
    return this.platformDataMap[key] ?? [];
  }

  // Generates and downloads an Excel report containing the Routine Maintenance KPI data.
  exportToExcel(): void {
    const workbook = new ExcelJS.Workbook();
    const worksheet = workbook.addWorksheet('Routine Maintenance');

    const headers = [
      'KPI', 'Target', 'Category',
      'Responsible DGM',
      ...this.columns.map(column => this.getColumnDisplay(column))
    ];

    worksheet.addRow(headers);

    this.maintenanceRows.forEach(row => {
      worksheet.addRow([
        row.routine.kpi ?? '',
        row.routine.target ?? '',
        row.routine.calculation ?? '',
        row.routine.responsibleDGM ?? '',
        ...this.columns.map(c => this.placeholderMap[row.platformKey!]?.[c] ?? '')
      ]);
    });

    workbook.xlsx.writeBuffer().then((buffer: ArrayBuffer) => {
      const blob = new Blob([buffer], {
        type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
      });

      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = 'Routine_Maintenance_KPI.xlsx';
      link.click();
    });

  }

  /* ================= HELPERS ================= */

  // Formats a generic string value, defaulting to 'No data' if empty.
  formatRoutineValue(value?: string): string {
    return value?.trim() || 'No data';
  }

  // Formats a calculated placeholder percentage value for a specific column and platform.
  formatPlaceholderValue(platformKey: PlatformKey | null, column: string): string {
    if (!platformKey) return 'No data';
    const value = this.placeholderMap[platformKey]?.[column] ?? '-';
    return value === '-' ? '-' : `${value}%`;
  }

  // Gets the appropriate display name for a column based on the region lookup map.
  getColumnDisplay(column: string): string {
    return this.engineerNameMap[column]
      ?? this.engineerNameMap[normalizeLookupKey(column)]
      ?? column;
  }


  // Retrieves the detailed scheduled or attended value for a column from a platform record.
  getDetailValue(
    record: PlatformRecord,
    column: string,
    field: 'scheduled' | 'attended'
  ): string {

    if (!record || !record.data) return 'No data';

    const key = Object.keys(record.data).find(
      k => k.trim().toLowerCase() === column.trim().toLowerCase()
    );

    if (!key) return 'No data';

    return record.data[key]?.[field]?.toString() ?? 'No data';
  }



  /*getDetailValue(record: PlatformRecord, column: string, field: 'Column2' | 'Column3'): string {
    const detail = record.details?.find(d => d.Column1 === column);
    const val = detail?.[field];
    return val === undefined || val === null || val === '' ? 'No data' : String(val);
  }*/

  // Returns whether a given column's data for the record has been verified.
  getIsVerified(record: PlatformRecord, column: string): boolean {
    if (!record || !record.data) return false;
    const key = Object.keys(record.data).find(
      k => k.trim().toLowerCase() === column.trim().toLowerCase()
    );
    return key ? (record.data[key]?.isVerified ?? false) : false;
  }

  // Retrieves the identifier for the data detail belonging to a specific column.
  getDetailId(record: PlatformRecord, column: string): number | undefined {
    if (!record || !record.data) return undefined;
    const key = Object.keys(record.data).find(
      k => k.trim().toLowerCase() === column.trim().toLowerCase()
    );
    return key ? record.data[key]?.id : undefined;
  }




  // Toggles the verified status for a specific entry and makes an API call to save the change.
  toggleVerified(platformKey: PlatformKey, record: PlatformRecord, column: string): void {
    if (!this.canEditMetrics) return;

    const id = this.getDetailId(record, column);
    if (id) {
      // Determine the correct endpoint based on platformKey
      let endpoint = '';
      if (platformKey === 'msan') endpoint = 'msan-mtc-data';
      else if (platformKey === 'vpn') endpoint = 'ipnw-mtc-data';
      else if (platformKey === 'slbn') endpoint = 'slbn-mtc-data';

      if (endpoint) {
        this.http.patch<{ id: number; isVerified: boolean }>(`${environment.apiUrl}/${endpoint}/${id}/toggle-verified`, {}).subscribe({
          next: (res) => {
            const key = Object.keys(record.data).find(
              k => k.trim().toLowerCase() === column.trim().toLowerCase()
            );
            if (key && record.data[key]) {
              record.data[key].isVerified = res.isVerified;
            }
            this.cdr.detectChanges();
          },
          error: (err) => {
            console.error('Toggle verified failed', err);
          }
        });
      }
    }
  }

  // Retrieves the calculated tower sum for a particular platform and column.
  getTowerSum(key: PlatformKey, column: string): number {
    return this.towerSumsMap[key]?.[column] ?? 0;
  }

  trackByMaintenanceRow = (_: number, item: MaintenanceRow) =>
    `${item.routine.no}-${item.platformKey}`;
  trackByColumn = (_: number, column: string) => column;
  trackByMonth = (_: number, record: PlatformRecord) => record.month;

  /* ================= CALCULATIONS ================= */

  // Initializes the placeholder map with dashes when no platform period data exists.
  private buildDefaultPlaceholders(): PlaceholderMap {
    const map: PlaceholderMap = {};
    PLATFORM_COLUMNS.forEach(c => (map[c] = '-'));
    return map;
  }

  // Computes placeholder percentages based on the platform and its records.
  private calculatePlaceholderValues(data: PlatformRecord[], platform: PlatformKey): PlaceholderMap {
    const result = this.buildDefaultPlaceholders();
    if (!data.length) return result;

    const months = this.getTargetMonths(platform);
    if (!months.length) return result;

    const selectedMonthLabel = this.monthOptions.find(m => m.value === this.selectedMonth)?.label ?? '';
    const selectedMonthIndex = this.selectedMonth - 1;

    // Prefer the exact selected month entry; otherwise fall back to the latest
    // available month in the selected window that is on or before the selected month.
    const exactEntry = data.find(d => d.month === selectedMonthLabel);
    const targetEntry = exactEntry
      || months
        .filter(m => MONTH_NAMES.indexOf(m) <= selectedMonthIndex)
        .reverse()
        .map(m => data.find(d => d.month === m))
        .find(entry => entry !== undefined);

    if (!targetEntry) return result;

    this.applyCumulativePercentage(result, targetEntry);
    return result;
  }

  // Returns the correct PlatformRecord corresponding to the platform selection for the main table.
  private getTargetEntryForMainTable(platformKey: PlatformKey | null): PlatformRecord | undefined {
    if (!platformKey) return undefined;
    const data = this.platformDataMap[platformKey];
    if (!data || !data.length) return undefined;

    const months = this.getTargetMonths(platformKey);
    if (!months.length) return undefined;

    const selectedMonthLabel = this.monthOptions.find(m => m.value === this.selectedMonth)?.label ?? '';
    const selectedMonthIndex = this.selectedMonth - 1;

    const exactEntry = data.find(d => d.month === selectedMonthLabel);
    return exactEntry
      || months
        .filter(m => MONTH_NAMES.indexOf(m) <= selectedMonthIndex)
        .reverse()
        .map(m => data.find(d => d.month === m))
        .find(entry => entry !== undefined);
  }

  // Checks if the active target record has data details for the specified column.
  hasDetailForMainTable(platformKey: PlatformKey | null, column: string): boolean {
    const entry = this.getTargetEntryForMainTable(platformKey);
    return entry ? !!this.getDetailId(entry, column) : false;
  }

  // Checks if the detail in the main table target record has been verified.
  isVerifiedForMainTable(platformKey: PlatformKey | null, column: string): boolean {
    const entry = this.getTargetEntryForMainTable(platformKey);
    return entry ? this.getIsVerified(entry, column) : false;
  }

  // Applies cumulative percentages mapping over the respective columns.
  private applyCumulativePercentage(result: PlaceholderMap, entry: PlatformRecord): void {
    PLATFORM_COLUMNS.forEach(column => {
      const detail = entry.data?.[column];
      const cumSched = Number(detail?.column2) || 0; // CumulativeSched
      const cumAchieved = Number(detail?.column3) || 0; // CumulativeAchieved

      result[column] = cumSched
        ? Math.min((cumAchieved / cumSched) * 100, 100).toFixed(2)
        : '-';
    });
  }

  // Computes the sums of scheduled tasks for each region from limited records.
  private calculateTowerSums(data: PlatformRecord[], limit: number): TowerSumRecord {
    const sums: TowerSumRecord = {};



    data.slice(0, limit).forEach(entry => {


      console.log('Month:', entry.month, 'WPC1 column2:',entry.data?.['NW/WPC-1']?.column2);

      Object.keys(entry.data || {}).forEach(key => {
        const val = entry.data[key]?.column2;
        sums[key] = (sums[key] ?? 0) + (val ? Number(val) : 0);
      });
    });
    console.log('FINAL WPC1=', sums['NW/WPC-1']);
    return sums;
  }

  // Resolves the period months that are targeted based on the selected platform type.
  private getTargetMonths(platform: PlatformKey): string[] {
    if (platform === 'msan') {
      // Use the half-year that contains the selected month
      if (this.selectedMonth >= 1 && this.selectedMonth <= 6) {
        return MONTH_NAMES.slice(0, 6);   // Jan–Jun
      }
      return MONTH_NAMES.slice(6);      // Jul–Dec (7..12)
    }

    // VPN and SLBN: two-month windows (Jan-Feb, Mar-Apr, ...)
    const monthIndex = this.selectedMonth - 1;
    if (monthIndex < 0 || monthIndex > 11) return [];

    const startIndex = monthIndex % 2 === 0 ? monthIndex : monthIndex - 1;
    return [MONTH_NAMES[startIndex], MONTH_NAMES[startIndex + 1]];
  }

  // Sets the error message if one has not already been populated.
  private setError(msg: string): void {
    if (!this.errorMessage) this.errorMessage = msg;
  }

  // Requests region data from the backend to construct the display name lookup map.
  private loadEngineerNames(): void {
    this.regionService.getAll().subscribe({
      next: (res: Region[] | any[]) => {
        this.engineerNameMap = buildEngineerDisplayLookupMap(mapRegionRecords(res));
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to fetch region table for Routine Maintenance headers:', err);
        this.engineerNameMap = {};
      }
    });
  }
}
