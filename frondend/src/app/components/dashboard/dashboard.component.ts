/*
 File: dashboard.component.ts
 Description: Main KPI dashboard display component
 Purpose: Display comprehensive KPI metrics with region data, overall performance indicators, and detailed meter information.
 Features: Region-based KPI display, meter details modal, animations, responsive layout, performance tracking
*/

import { animate, state, style, transition, trigger } from '@angular/animations';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ChangeDetectorRef, Component, Inject, OnDestroy, OnInit, PLATFORM_ID } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FilterUtils } from '../../utils/filter.utils';
import { formatEngineerDisplay, getAreaLookupAliases } from '../../utils/region-display.utils';


interface MeterData {
  code: string;
  label: string;
  engineer?: string;
}

interface RegionData {
  title: string;
  meters: MeterData[];
}

interface TotalsData {
  [key: string]: number;
}

type RegionApi = {
  id: number;
  region: string;
  province: string;
  networkEngineer: string;
  leaCode: string;
  engName?: string;
};

type RtomAreaApi = {
  areaCode: string;
  displayName: string;
};

type OverallKpiResultApi = {
  areaCode: string;
  overallKpiValuePercent: number;
  kpiDefinitionId: number;
  kpiName?: string;
  achievedKpi: number;
  maximumPointsPerKpi: number;
  pointsAchieved: number;
};

type MeterKpiRow = {
  kpiDefinitionId: number;
  kpiName: string;
  achievedKpi: number;
  maximumPointsPerKpi: number;
  pointsAchieved: number;
  category?: string;
};

type MeterDetails = {
  region: string;
  province: string;
  networkEngineer: string;
  leaCode: string;
  displayName: string;
  overallPercent: number;
  totalMaximumPoints: number;
  totalPointsAchieved: number;
  kpiRows: MeterKpiRow[];
};

type QuarterKey = 'Q2' | 'Q3';

// The dashboard supports the two requested financial/reporting quarters.
// Keeping the month ranges in one place prevents the button handlers and data loader from drifting apart.
const QUARTER_MONTHS: Record<QuarterKey, number[]> = {
  Q2: [4, 5, 6],
  Q3: [7, 8, 9],
};

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, HttpClientModule, FormsModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.scss'],
  animations: [
    trigger('fadeIn', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('600ms ease-out', style({ opacity: 1 }))
      ])
    ]),
    trigger('slideIn', [
      transition(':enter', [
        style({ opacity: 0, transform: 'translateY(30px)' }),
        animate('500ms ease-out', style({ opacity: 1, transform: 'translateY(0)' }))
      ])
    ]),
    trigger('scaleIn', [
      transition(':enter', [
        style({ opacity: 0, transform: 'scale(0.8)' }),
        animate('400ms ease-out', style({ opacity: 1, transform: 'scale(1)' }))
      ])
    ]),
    trigger('hoverScale', [
      state('normal', style({ transform: 'scale(1)', boxShadow: '0 8px 20px rgba(0,0,0,0.1)' })),
      state('hover', style({ transform: 'scale(1.02)', boxShadow: '0 12px 30px rgba(0,0,0,0.15)' })),
      transition('normal <=> hover', animate('200ms ease-in-out'))
    ]),
    trigger('meterHover', [
      state('normal', style({ transform: 'scale(1)' })),
      state('hover', style({ transform: 'scale(1.1)' })),
      transition('normal <=> hover', animate('200ms ease-in-out'))
    ])
  ]
})
export class DashboardComponent implements OnInit, OnDestroy {
  // The dashboard reads persisted overall results for one selected month/year and never recalculates implicitly.
  private readonly now = new Date();

  selectedMonth = this.now.getMonth() + 1;
  selectedYear = this.now.getFullYear();

  // A null value means the dashboard is in normal single-month mode.
  activeQuarter: QuarterKey | null = null;


  get monthOptions(): { label: string; value: number }[] { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = [];

  regions: RegionData[] = [];
  totals: TotalsData = {};
  loading = true;
  calculating = false;
  showCalculateButton = false;
  error: string | null = null;
  selectedDetails: MeterDetails | null = null;
  selectedRegionTitle = '';
  regionCount = 0;
  engineerCount = 0;
  provinceCount = 0;
  leaCount = 0;

  // Hover state management used by region cards and individual meters.
  regionHoverStates: { [key: number]: boolean } = {};
  meterHoverStates: { [key: string]: boolean } = {};

 // private readonly regionApiBase = 'http://localhost:5043/api/regiondata';
 // private readonly rtomApiBase = 'http://localhost:5043/api/rtom-areas';
 // private readonly overallKpiApiBase = 'http://localhost:5043/api/overall-kpi-results';

  private readonly regionApiBase = `${environment.apiUrl}/regiondata`;
  private readonly rtomApiBase = `${environment.apiUrl}/rtom-areas`;
  private readonly overallKpiApiBase = `${environment.apiUrl}/overall-kpi-results`;

  private kpiCategoryMap = new Map<number, string>();
  private regionRows: RegionApi[] = [];
  private overallRows: OverallKpiResultApi[] = [];
  private engineerLookup = new Map<string, string>();

  // Provides API access, change detection, and the browser-platform guard.
  constructor(
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
    @Inject(PLATFORM_ID) private platformId: any
  ) {}

  get currentMonthLabel(): string {
    const found = this.monthOptions.find((m: { label: string; value: number }) => m.value === this.selectedMonth);
    return found ? found.label : '';
  }

  get currentPeriodLabel(): string {
    // Keep the header text aligned with the data currently shown in the meters.
    if (this.activeQuarter === 'Q2') return 'Q2 (April - June)';
    if (this.activeQuarter === 'Q3') return 'Q3 (July - September)';
    return this.currentMonthLabel;
  }

  ngOnInit(): void {
    // Initialize valid year options before loading region metadata and selected-period results.
    this.yearOptions = FilterUtils.generateYearOptions();
    if (!this.yearOptions.includes(this.selectedYear)) {
      this.selectedYear = this.yearOptions[this.yearOptions.length - 1];
    }
    this.loadDashboardData();
  }

  ngOnDestroy(): void {
    // Reserved lifecycle hook for future dashboard subscriptions or browser listeners.
  }

  onMonthChange(month: number): void {
    // Reload the selected month, or keep the active quarter view if the user changes
    // the filter while a quarter is selected.
    this.selectedMonth = Number(month);
    this.showCalculateButton = true;
    this.loadDashboardData(this.getActiveMonths());
  }

  onYearChange(year: number): void {
    // Keep the selected month valid for the new year before reloading the dashboard.
    this.selectedYear = Number(year);
    const available = this.monthOptions;
    if (!available.find(m => m.value === this.selectedMonth)) {
      this.selectedMonth = available[0]?.value ?? this.selectedMonth;
    }
    this.showCalculateButton = true;
    this.loadDashboardData(this.getActiveMonths());
  }

  calculate(): void {
    // The regular Calculate button always returns the dashboard to single-month mode.
    // This makes the user's explicit month/year selection take precedence over a quarter.
    if (this.loading || this.calculating) return;

    this.activeQuarter = null;
    this.calculating = true;
    this.showCalculateButton = true;
    this.error = null;

    const url = `${this.overallKpiApiBase}/calculate?month=${this.selectedMonth}&year=${this.selectedYear}`;

    this.http.post(url, {}).subscribe({
      next: () => {
        this.calculating = false;
        this.showCalculateButton = false;
        this.loadDashboardData();
      },
      error: (err) => {
        console.error('Failed to calculate dashboard data', err);
        this.calculating = false;
        this.showCalculateButton = false;
        this.error = 'Unable to calculate dashboard data.';
        this.cdr.detectChanges();
      },
    });
  }

  calculateQuarter(quarter: QuarterKey): void {
    // Quarter buttons are mutually exclusive because activeQuarter stores only one value.
    if (this.loading || this.calculating) return;

    this.activeQuarter = quarter;
    this.calculating = true;
    this.showCalculateButton = true;
    this.error = null;

    const months = this.getQuarterMonths(quarter);
    const url = `${this.overallKpiApiBase}/calculate-range?year=${this.selectedYear}`
      + `&startMonth=${months[0]}&endMonth=${months[months.length - 1]}`;

    this.http.post(url, {}).subscribe({
      next: () => {
        this.calculating = false;
        this.showCalculateButton = false;
        // Reload all three persisted monthly results so the dashboard can display
        // the quarter aggregate using the same area meters as monthly mode.
        this.loadDashboardData(months);
      },
      error: (err) => {
        console.error(`Failed to calculate ${quarter} dashboard data`, err);
        this.calculating = false;
        this.showCalculateButton = false;
        this.error = `Unable to calculate ${quarter} dashboard data.`;
        this.cdr.detectChanges();
      },
    });
  }

  private getQuarterMonths(quarter: QuarterKey): number[] {
    // Return a copy so callers cannot accidentally mutate the shared quarter definition.
    return [...QUARTER_MONTHS[quarter]];
  }

  private getActiveMonths(): number[] {
    return this.activeQuarter ? this.getQuarterMonths(this.activeQuarter) : [this.selectedMonth];
  }

  private sortRegionNames(a: string, b: string): number {
    // Keep Metro first, sort numbered regions numerically, and use alphabetical order otherwise.
    if (a === 'Metro' && b !== 'Metro') return -1;
    if (b === 'Metro' && a !== 'Metro') return 1;

    const ra = a.match(/Region\s*(\d+)/i);
    const rb = b.match(/Region\s*(\d+)/i);
    if (ra && rb) return Number(ra[1]) - Number(rb[1]);

    return a.localeCompare(b);
  }

  private loadDashboardData(months: number[] = [this.selectedMonth]): void {
    // Fetch region structure, area labels, KPI definitions, and all requested result months together.
    if (!isPlatformBrowser(this.platformId)) {
      this.loading = false;
      return;
    }

    this.loading = true;
    this.error = null;

    const requestedMonths = Array.from(new Set(months.map(Number)));
    const year = this.selectedYear;

    forkJoin({
      regions: this.http.get<RegionApi[]>(this.regionApiBase),
      rtomAreas: this.http.get<RtomAreaApi[]>(this.rtomApiBase),
      overall: forkJoin(requestedMonths.map(month =>
        this.http.get<OverallKpiResultApi[]>(`${this.overallKpiApiBase}?month=${month}&year=${year}`)
      )),
      // KPI definitions remain month-specific in the API; the selected month is used
      // for the details modal while quarter meter values are aggregated below.
      kpis: this.http.get<any[]>(`${environment.apiUrl}/kpi-definitions?month=${this.selectedMonth}&year=${year}`)
    }).subscribe({
      next: ({ regions, rtomAreas, overall: monthlyOverallRows, kpis }) => {
        this.regionRows = regions ?? [];
        this.overallRows = this.activeQuarter
          ? this.aggregateQuarterRows(monthlyOverallRows)
          : (monthlyOverallRows[0] ?? []);
        this.kpiCategoryMap.clear();
        (kpis ?? []).forEach((kpi) => {
          this.kpiCategoryMap.set(kpi.id, kpi.category ?? '');
        });
        const rtomLookup = new Map<string, string>();
        (rtomAreas ?? []).forEach((area) => {
          const code = this.normalizeArea((area as any).areaCode ?? (area as any).AreaCode ?? '');
          const name = (area as any).displayName ?? (area as any).DisplayName ?? '';
          if (code) rtomLookup.set(code, name || (area as any).areaCode || (area as any).AreaCode || '');
        });

        const engineerLookup = new Map<string, string>();
        (regions ?? []).forEach((row) => {
          const networkEngineer = (row as any).networkEngineer ?? (row as any).NetworkEngineer ?? '';
          const engName = (row as any).engName ?? (row as any).EngName ?? (row as any).engname ?? '';
          const code = this.normalizeArea((row as any).leaCode ?? (row as any).LeaCode ?? networkEngineer);
          const engineer = formatEngineerDisplay(networkEngineer, engName);
          if (code && engineer && !engineerLookup.has(code)) {
            engineerLookup.set(code, engineer);
          }
        });
        this.engineerLookup = engineerLookup;

        // Relate legacy NW-prefixed result codes to the official LEA code used by dashboard meters.
        const resultAreaToDisplayArea = new Map<string, string>();
        (regions ?? []).forEach((row) => {
          const networkEngineer = (row as any).networkEngineer ?? (row as any).NetworkEngineer ?? '';
          const leaCode = (row as any).leaCode ?? (row as any).LeaCode ?? '';
          const displayCode = this.normalizeArea(leaCode || networkEngineer);
          if (!displayCode) return;

          [...getAreaLookupAliases(networkEngineer), ...getAreaLookupAliases(leaCode)]
            .forEach((alias) => resultAreaToDisplayArea.set(alias, displayCode));
        });

        // Use one overall percentage per normalized area for the meter display.
        const percentLookup = new Map<string, number>();
        this.overallRows.forEach((row) => {
          const code = this.normalizeArea((row as any).areaCode ?? (row as any).AreaCode ?? '');
          if (!code || percentLookup.has(code)) return;
          const raw = (row as any).overallKpiValuePercent ?? (row as any).OverallKpiValuePercent ?? 0;
          percentLookup.set(code, Number(raw) || 0);
        });

        // Build unique region-to-area membership so each meter is rendered once.
        const regionMap = new Map<string, Set<string>>();
        (regions ?? []).forEach((row) => {
          const regionName = (row as any).region ?? (row as any).Region ?? 'Unknown';
          const areaCode = (row as any).leaCode ?? (row as any).LeaCode ?? (row as any).networkEngineer ?? (row as any).NetworkEngineer ?? '';
          if (!areaCode) return;
          const set = regionMap.get(regionName) ?? new Set<string>();
          set.add(areaCode);
          regionMap.set(regionName, set);
        });

        // Compute summary counts from normalized region metadata.
        const uniqueRegions = new Set<string>();
        const uniqueEngineers = new Set<string>();
        const uniqueProvinces = new Set<string>();
        const uniqueLeas = new Set<string>();
        (regions ?? []).forEach((row) => {
          const regionName = (row as any).region ?? (row as any).Region ?? '';
          if (regionName) uniqueRegions.add(regionName.trim());

          const engineer = this.normalizeName((row as any).networkEngineer ?? (row as any).NetworkEngineer ?? '');
          if (engineer) uniqueEngineers.add(engineer);

          const province = this.normalizeName((row as any).province ?? (row as any).Province ?? '');
          if (province) uniqueProvinces.add(province);

          const leaCode = this.normalizeName((row as any).leaCode ?? (row as any).LeaCode ?? (row as any).networkEngineer ?? (row as any).NetworkEngineer ?? '');
          if (leaCode) uniqueLeas.add(leaCode);
        });
        this.regionCount = uniqueRegions.size;
        this.engineerCount = uniqueEngineers.size;
        this.provinceCount = uniqueProvinces.size;
        this.leaCount = uniqueLeas.size;

        this.regions = Array.from(regionMap.entries())
          .map(([regionName, areaCodes]) => {
            const meters = Array.from(areaCodes)
              .map((code) => {
                const normalizedCode = this.normalizeArea(code);
                const label = rtomLookup.get(normalizedCode) || code;
                const engineer = engineerLookup.get(normalizedCode);
                return { code, label, engineer } as MeterData;
              })
              .sort((a, b) => a.label.localeCompare(b.label));
            return { title: regionName, meters } as RegionData;
          })
          .sort((a, b) => this.sortRegionNames(a.title, b.title));

        const totals: TotalsData = {};
        percentLookup.forEach((value, code) => {
          totals[code] = value;
          const displayCode = resultAreaToDisplayArea.get(code);
          if (displayCode) totals[displayCode] = value;
        });
        this.totals = totals;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load dashboard data', err);
        this.error = 'Unable to load dashboard data.';
        this.loading = false;
        this.regions = [];
        this.totals = {};
        this.regionCount = 0;
        this.engineerCount = 0;
        this.provinceCount = 0;
        this.leaCount = 0;
        this.cdr.detectChanges();
      },
    });
  }

  private aggregateQuarterRows(monthlyRows: OverallKpiResultApi[][]): OverallKpiResultApi[] {
    // Average each KPI area's monthly points so Q2/Q3 follows the same cumulative rule
    // as the analytics page. Missing months are excluded from the average.
    const groups = new Map<string, {
      rows: OverallKpiResultApi[];
      areaCode: string;
      kpiDefinitionId: number;
    }>();

    monthlyRows.flat().forEach((row) => {
      const areaCode = String((row as any).areaCode ?? (row as any).AreaCode ?? '');
      const kpiDefinitionId = Number((row as any).kpiDefinitionId ?? (row as any).KpiDefinitionId ?? 0);
      const key = `${this.normalizeArea(areaCode)}|${kpiDefinitionId}`;
      const group = groups.get(key) ?? { rows: [], areaCode, kpiDefinitionId };
      group.rows.push(row);
      groups.set(key, group);
    });

    const aggregatedRows = Array.from(groups.values()).map(({ rows, areaCode, kpiDefinitionId }) => {
      const average = (selector: (row: OverallKpiResultApi) => number): number => {
        const values = rows.map(selector).filter(value => Number.isFinite(value));
        return values.length ? values.reduce((sum, value) => sum + value, 0) / values.length : 0;
      };
      const first = rows[0];

      return {
        ...first,
        areaCode,
        kpiDefinitionId,
        // The overall percentage is recalculated below from the averaged point totals.
        overallKpiValuePercent: 0,
        achievedKpi: average(row => Number(row.achievedKpi)),
        maximumPointsPerKpi: average(row => Number(row.maximumPointsPerKpi)),
        pointsAchieved: average(row => Number(row.pointsAchieved)),
      };
    });

    // Calculate one weighted overall percentage per area, matching AnalyticsService.
    const areaTotals = new Map<string, { maximumPoints: number; pointsAchieved: number }>();
    aggregatedRows.forEach((row) => {
      const key = this.normalizeArea(row.areaCode);
      const totals = areaTotals.get(key) ?? { maximumPoints: 0, pointsAchieved: 0 };
      totals.maximumPoints += row.maximumPointsPerKpi;
      totals.pointsAchieved += row.pointsAchieved;
      areaTotals.set(key, totals);
    });

    return aggregatedRows.map((row) => {
      const totals = areaTotals.get(this.normalizeArea(row.areaCode));
      const overallPercent = totals && totals.maximumPoints > 0
        ? (totals.pointsAchieved / totals.maximumPoints) * 100
        : 0;
      return { ...row, overallKpiValuePercent: overallPercent };
    });
  }

  private normalizeArea(value: string): string {
    // Normalize LEA/area identifiers before joining values from different API responses.
    return String(value ?? '').replace(/[^A-Za-z0-9]/g, '').toLowerCase();
  }

  private normalizeName(value: string): string {
    // Trim display metadata before using it in unique-count calculations.
    return String(value ?? '').trim();
  }

  valueForMeter(meter: MeterData): number {
    // Return the persisted area percentage, defaulting to zero when no result exists.
    const key = this.normalizeArea(meter.code);
    const exact = this.totals[key];
    return Number.isFinite(exact) ? exact : 0;
  }

  getMaxValue(meters: MeterData[]): number {
    // Find the highest area value in a region for the max-value highlight.
    const values = meters.map((m) => this.valueForMeter(m));
    return values.length ? Math.max(...values) : 0;
  }

  isMaxValue(meter: MeterData, meters: MeterData[]): boolean {
    // Compare with a tolerance so decimal rounding does not affect the highlight.
    const value = this.valueForMeter(meter);
    const max = this.getMaxValue(meters);
    return Math.abs(value - max) < 0.0001 && max > 0;
  }

  getProgressBarColor(meter: MeterData, meters: MeterData[]): string {
    // Use a green max-value accent or an opacity-scaled SLT blue for other meters.
    const value = this.valueForMeter(meter);
    const isMax = this.isMaxValue(meter, meters);

    if (isMax) {
      // 10% Accent - Green for good KPIs
      return '#28A745';
    }

    // 30% Secondary - SLT Blue with opacity
    const opacity = value / 100;
    return `rgba(0, 87, 166, ${opacity})`;
  }

  getMeterTextColor(meter: MeterData, meters: MeterData[]): string {
    // Emphasize the text of the highest-valued meter in its region.
    // 10% Accent - Green for max values (good KPIs)
    return this.isMaxValue(meter, meters) ? '#28A745' : '#000';
  }

  getMeterFontWeight(meter: MeterData, meters: MeterData[]): string {
    // Return the font weight associated with the meter's max-value state.
    return this.isMaxValue(meter, meters) ? 'bold' : 'normal';
  }

  getEngineerForMeter(meter: MeterData): string {
    // Resolve the display engineer from the meter first, then the normalized lookup map.
    const key = this.normalizeArea(meter.code);
    return meter.engineer || this.engineerLookup.get(key) || '—';
  }

  getCircularProgressBackground(meter: MeterData, meters: MeterData[]): string {
    // Build the conic gradient used for the circular percentage indicator.
    const value = this.valueForMeter(meter);
    const maxValue = 102; // Match React's maxValue
    const normalizedValue = Math.min(value, maxValue);

    // Color based on value thresholds
    const color = this.getColorForValue(value);

    // Light grey trail
    const trailColor = '#E0E0E0';

    return `conic-gradient(${color} 0% ${normalizedValue}%, ${trailColor} ${normalizedValue}% 100%)`;
  }

  getColorForValue(value: number): string {
    // Map KPI percentage thresholds to the dashboard's green/yellow/red palette.
    if (value > 80) {
      return '#28A745'; // Green
    } else if (value >= 30) {
      return '#FFC107'; // Yellow
    } else {
      return '#DC3545'; // Red
    }
  }

  getProgressTextColor(meter: MeterData, meters: MeterData[]): string {
    // Match the progress text color to the meter's max-value emphasis.
    // 10% Accent - Green for max values (good KPIs)
    return this.isMaxValue(meter, meters) ? '#28A745' : '#000';
  }

  // Fix: Add Object reference for template
  get Object() {
    return Object;
  }

  trackByRegion(index: number, region: RegionData): string {
    // Track region cards by stable title to avoid unnecessary DOM recreation.
    return region.title;
  }

  formatRegionTitle(value: string): string {
    // Convert API region labels into readable headings while preserving region numbers.
    const raw = String(value ?? '').trim();
    if (!raw) return '';

    const regionMatch = raw.match(/^region\s*(\d+)/i);
    if (regionMatch) {
      const numberValue = Number(regionMatch[1]);
      return `Region ${Number.isFinite(numberValue) ? numberValue : regionMatch[1]}`;
    }

    return raw.charAt(0).toUpperCase() + raw.slice(1);
  }

  trackByMeter(index: number, meter: MeterData): string {
    // Track meters by area code so percentage refreshes preserve their DOM state.
    return meter.code;
  }

  openMeterDetails(region: RegionData, meter: MeterData): void {
    // Assemble the selected area's metadata, overall score, and KPI rows for the details modal.
    console.log('🎯 Opening meter details for:', { region: region.title, meter: meter.label, code: meter.code });
    const areaKey = this.normalizeArea(meter.code);
    const matchingRegion = this.regionRows.find((row) =>
      this.normalizeArea((row as any).leaCode ?? (row as any).LeaCode ?? '') === areaKey
    );
    console.log('📍 Matching region found:', matchingRegion);

    const networkEngineer = formatEngineerDisplay(
      matchingRegion?.networkEngineer,
      (matchingRegion as any)?.engName ?? (matchingRegion as any)?.EngName ?? (matchingRegion as any)?.engname
    );
    const province = matchingRegion?.province ?? '—';
    const regionName = matchingRegion?.region ?? region.title;
    const leaCode = matchingRegion?.leaCode ?? meter.code;

    const regionAreaAliases = new Set([
      ...getAreaLookupAliases(meter.code),
      ...getAreaLookupAliases(matchingRegion?.networkEngineer),
    ]);

    const rows = this.overallRows.filter((row) =>
      getAreaLookupAliases((row as any).areaCode ?? (row as any).AreaCode ?? '')
        .some((alias) => regionAreaAliases.has(alias))
    );

    const totalMaximumPoints = rows.reduce(
      (sum, row) => sum + Number((row as any).maximumPointsPerKpi ?? (row as any).MaximumPointsPerKpi ?? 0),
      0
    );

    const totalPointsAchieved = rows.reduce(
      (sum, row) => sum + Number((row as any).pointsAchieved ?? (row as any).PointsAchieved ?? 0),
      0
    );

    const overallPercentRaw = rows[0]?.overallKpiValuePercent;
    const overallPercent = Number.isFinite(Number(overallPercentRaw))
      ? Number(overallPercentRaw)
      : totalMaximumPoints > 0
        ? (totalPointsAchieved / totalMaximumPoints) * 100
        : 0;

    const kpiRows: MeterKpiRow[] = rows.map((row) => {
      const defId = Number((row as any).kpiDefinitionId ?? (row as any).KpiDefinitionId ?? 0);
      return {
        kpiDefinitionId: defId,
        kpiName: String((row as any).kpiName ?? (row as any).KpiName ?? ''),
        achievedKpi: Number((row as any).achievedKpi ?? (row as any).AchievedKpi ?? 0),
        maximumPointsPerKpi: Number((row as any).maximumPointsPerKpi ?? (row as any).MaximumPointsPerKpi ?? 0),
        pointsAchieved: Number((row as any).pointsAchieved ?? (row as any).PointsAchieved ?? 0),
        category: this.kpiCategoryMap.get(defId) ?? ''
      };
    });

    this.selectedRegionTitle = regionName;
    const key = this.normalizeArea(meter.code);
    const fullEngineer = meter.engineer || this.engineerLookup.get(key) || networkEngineer || '—';
    const areaDisplay = meter.label || meter.code;
    const compositeDisplay = areaDisplay;
    const networkEngineerValue = fullEngineer !== '—' ? fullEngineer : networkEngineer;
    this.selectedDetails = {
      region: regionName,
      province,
      networkEngineer: networkEngineerValue,
      leaCode,
      displayName: compositeDisplay,
      overallPercent: Number(overallPercent.toFixed(2)),
      totalMaximumPoints: Number(totalMaximumPoints.toFixed(4)),
      totalPointsAchieved: Number(totalPointsAchieved.toFixed(4)),
      kpiRows,
    };
    console.log('✅ Modal details set:', this.selectedDetails);
  }

  splitEngineerName(engineer: string): { code: string; name: string } {
    // Split the formatted engineer label into code and optional human-readable name parts.
    if (!engineer || engineer === '—') {
      return { code: '—', name: '' };
    }
    const parenIndex = engineer.indexOf('(');
    if (parenIndex !== -1) {
      const code = engineer.substring(0, parenIndex).trim();
      let name = engineer.substring(parenIndex + 1);
      const closeParenIndex = name.indexOf(')');
      if (closeParenIndex !== -1) {
        name = name.substring(0, closeParenIndex);
      }
      return { code, name: name.trim() };
    }
    return { code: engineer, name: '' };
  }

  closeDetails(): void {
    // Clear modal state and the selected region title.
    this.selectedDetails = null;
    this.selectedRegionTitle = '';
  }

  getKpiRowClass(row: any): string {
    // Assign category-specific styling to KPI rows in the details modal.
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
