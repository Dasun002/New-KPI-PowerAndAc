import { CommonModule } from '@angular/common';
import { AuthService } from '../../../../services/auth.service';
import { Component, OnDestroy, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as ExcelJS from 'exceljs';
import { firstValueFrom, forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { OtnOp1Service, OtnOpKpi, OtnOp1Metric } from '../../../../services/otn-op1.service';
import { OtnOp2Service, OtnOp2Metric } from '../../../../services/otn-op2.service';
import { RegionService, Region } from '../../../../services/region.service';
import { FilterUtils } from '../../../../utils/filter.utils';
import { buildEngineerDisplayMap, mapRegionRecords } from '../../../../utils/region-display.utils';




type Dict<T = any> = Record<string, T>;

interface RegionRow {
	region?: string;
	province?: string;
	networkEngineer?: string;
	lea?: string;
	engName?: string;
}

interface BaseEntry {
	id: number;
	networkEngineerKpi: string;
	division: string;
	section: string;
	kpiPercent: number;
	formType: 'OtnOp1' | 'OtnOp2';
	isModified?: boolean;
}

interface MetricMeta {
	id?: number;
	site?: string;
	month?: number;
	year?: number;
}

interface OtnOp1Entry extends BaseEntry {
	formType: 'OtnOp1';
	unavailableMinutes?: Dict<any>;
	totalMinutes?: Dict<any>;
	totalNodes?: Dict<any>;
	metricMeta?: Dict<MetricMeta>;
}

interface OtnOp2Entry extends BaseEntry {
	formType: 'OtnOp2';
	totalFailedLinks?: Dict<any>;
	linksSlaNotViolated?: Dict<any>;
	metricMeta?: Dict<MetricMeta>;
}

interface EditCellState {
	rowId: number | null;
	parentKey: string | null;
	childKey: string | null;
	value: string;
	formType: 'OtnOp1' | 'OtnOp2' | null;
}

@Component({
	selector: 'app-otn-op',
	standalone: true,
	imports: [CommonModule, FormsModule],
	templateUrl: './otn-op.component.html',
	styleUrls: ['./otn-op.component.scss'],
})
export class OtnOpComponent implements OnInit, OnDestroy {
	pageTitle = 'OTN Operations';

	otnOp1Data: OtnOp1Entry[] = [];
	otnOp2Data: OtnOp2Entry[] = [];
	regionTable: RegionRow[] = [];

	loading = true;
	error: string | null = null;

	isEditingAllowed = false;
	devRoleOverride: 'padmin' | 'user' | null = null;
	saving = false;



	// daysInMonth must be derived from the reporting period or per-metric metadata.
	// Use getDaysInMonth(year, month) to compute month length when needed.

	private permissionTimer: ReturnType<typeof setInterval> | null = null;

	formValues = {
		dropdown1: '',
		dropdown2: '',
		dropdown3: '',
		dropdown4: '',
	};

	dropdown2Options: string[] = [];
	dropdown3Options: string[] = [];
	dropdown4Options: string[] = [];
	engineerNameMap: Record<string, string> = {};

	selectedYear: number = new Date().getFullYear();
	selectedMonth: number = new Date().getMonth() + 1;
	yearOptions: number[] = FilterUtils.generatePlatformYearOptions();
	get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }

	editCell: EditCellState = {
		rowId: null,
		parentKey: null,
		childKey: null,
		value: '',
		formType: null,
	};

	toasts: Array<{ id: number; type: 'success' | 'danger'; text: string }> = [];
	private toastId = 1;

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

	private friendlyToDbKey: Record<string, string> = {};
	private filtersInitialized = false;

	// Injects the required dependencies.
	constructor(
		private otnOp1Service: OtnOp1Service,
		private otnOp2Service: OtnOp2Service,
		private regionService: RegionService,
		private authService: AuthService,

		private cdr: ChangeDetectorRef
	) {}

ngOnInit(): void {
		this.buildFriendlyMap();
		
		// Set default to previous month
		const prevMonth = new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1);
		this.selectedYear = prevMonth.getFullYear();
		this.selectedMonth = prevMonth.getMonth() + 1;
		
		this.refreshEditPermission();
		this.loadRegionTable();
		this.initializeFilters();
		this.loadData();

		this.permissionTimer = setInterval(() => this.refreshEditPermission(), 60000);

	}

	ngOnDestroy(): void {
		if (this.permissionTimer) {
			clearInterval(this.permissionTimer);
			this.permissionTimer = null;
		}
	}

	get regions(): string[] {
		return Array.from(
			new Set(this.regionTable.map((row) => row.region).filter(Boolean) as string[])
		);
	}

	get selectedKey(): string {
		return this.formValues.dropdown4 ? this.toCanonicalSiteKey(this.formValues.dropdown4) : '';
	}

	get selectedLeaLabel(): string {
		if (!this.formValues.dropdown4) {
			return '';
		}
		const key = this.formValues.dropdown4;
		return this.optionMapping[key] || key.toUpperCase();
	}

	private collectAllAreaKeys(): string[] {
		const areaSet = new Set<string>();
		this.otnOp1Data.forEach((entry) => {
			Object.keys(entry.totalMinutes || {}).forEach((key) => key && areaSet.add(key));
			Object.keys(entry.unavailableMinutes || {}).forEach((key) => key && areaSet.add(key));
			Object.keys(entry.totalNodes || {}).forEach((key) => key && areaSet.add(key));
		});
		this.otnOp2Data.forEach((entry) => {
			Object.keys(entry.totalFailedLinks || {}).forEach((key) => key && areaSet.add(key));
			Object.keys(entry.linksSlaNotViolated || {}).forEach((key) => key && areaSet.add(key));
		});
		return Array.from(areaSet);
	}

	private getExportAreaKeys(): string[] {
		return this.selectedKey ? [this.selectedKey] : this.collectAllAreaKeys();
	}

	private resolveAreaHeader(areaKey: string): string {
		if (this.selectedKey && areaKey === this.selectedKey && this.selectedLeaLabel) {
			return this.selectedLeaLabel;
		}
		return this.optionMapping[areaKey] || areaKey.toUpperCase();
	}

	get canEditMetrics(): boolean {
		return this.isEditingAllowed && !!this.selectedKey;
	}

	get combinedData(): Array<OtnOp1Entry | OtnOp2Entry> {
		return [...this.otnOp1Data, ...this.otnOp2Data].sort((a, b) => a.id - b.id);
	}

	selectedPercentage(entry: OtnOp1Entry | OtnOp2Entry): string {
		if (!this.selectedKey) {
			return '';
		}

		if (entry.formType === 'OtnOp1') {
			const pct = this.calculatePercentageOtnOp1(
				(entry as OtnOp1Entry).totalMinutes?.[this.selectedKey],
				(entry as OtnOp1Entry).unavailableMinutes?.[this.selectedKey],
				(entry as OtnOp1Entry).totalNodes?.[this.selectedKey],
				(entry as OtnOp1Entry).metricMeta?.[this.selectedKey]
			);
			return pct === null ? '-' : `${pct.toFixed(2)}%`;
		}

		const pct = this.calculatePercentageOtnOp2(
			(entry as OtnOp2Entry).totalFailedLinks?.[this.selectedKey],
			(entry as OtnOp2Entry).linksSlaNotViolated?.[this.selectedKey]
		);
		return isNaN(pct) ? '' : `${pct.toFixed(2)}%`;
	}

	getTotalMinutesDisplay(entry: OtnOp1Entry | OtnOp2Entry): string {
		if (entry.formType !== 'OtnOp1' || !this.selectedKey) {
			return '';
		}

		const manual = Number((entry as OtnOp1Entry).totalMinutes?.[this.selectedKey]) || 0;
		const nodes = Number((entry as OtnOp1Entry).totalNodes?.[this.selectedKey]) || 0;
		const days = this.getDaysForEntry(entry as OtnOp1Entry, this.selectedKey);
		const computed = 24 * 60 * days * nodes;
		const value = manual || computed;
		return value ? String(value) : '';
	}

	getOtnOp1Value(
		entry: OtnOp1Entry | OtnOp2Entry,
		field: 'unavailableMinutes' | 'totalNodes'
	): string {
		if (entry.formType !== 'OtnOp1' || !this.selectedKey) {
			return '';
		}
		const payload = (entry as OtnOp1Entry)[field] || {};
		return payload[this.selectedKey] ?? '';
	}

	getOtnOp2Value(
		entry: OtnOp1Entry | OtnOp2Entry,
		field: 'totalFailedLinks' | 'linksSlaNotViolated'
	): string {
		if (entry.formType !== 'OtnOp2' || !this.selectedKey) {
			return '';
		}
		const payload = (entry as OtnOp2Entry)[field] || {};
		return payload[this.selectedKey] ?? '';
	}

	private norm(value: string | null | undefined): string {
		return value ? value.replace(/[^A-Za-z0-9]/g, '').toLowerCase() : '';
	}

	private getDaysInMonth(year: number, month: number): number {
		return new Date(year, month, 0).getDate();
	}

	private getDaysForEntry(entry: OtnOp1Entry | OtnOp2Entry, areaKey: string): number {
		const meta = (entry as any)?.metricMeta?.[areaKey];
		const year = meta?.year ?? this.selectedYear;
		const month = meta?.month ?? this.selectedMonth;
		return this.getDaysInMonth(year, month);
	}

	private toCanonicalSiteKey(value: string | null | undefined): string {
		const normalized = this.norm(value);
		if (!normalized) {
			return '';
		}
		return this.friendlyToDbKey[normalized] || normalized;
	}

	private buildFriendlyMap(): void {
		const out: Record<string, string> = {};
		Object.keys(this.optionMapping).forEach((dbKey) => {
			out[this.norm(this.optionMapping[dbKey])] = dbKey;
			out[this.norm(dbKey)] = dbKey;
		});
		this.friendlyToDbKey = out;
	}

	private showToast(type: 'success' | 'danger', text: string): void {
		const id = this.toastId++;
		this.toasts.push({ id, type, text });
		setTimeout(() => this.dismissToast(id), 3000);
	}

	dismissToast(id: number): void {
		this.toasts = this.toasts.filter((toast) => toast.id !== id);
	}

	private refreshEditPermission(): void {
		if (this.devRoleOverride) {
			this.isEditingAllowed = this.devRoleOverride === 'padmin';
			return;
		}
		this.isEditingAllowed = this.authService.canEditPage('OTN OP');
	}

	toggleRoleOverride(): void {
		if (!this.devRoleOverride) {
			this.devRoleOverride = 'padmin';
		} else if (this.devRoleOverride === 'padmin') {
			this.devRoleOverride = 'user';
		} else {
			this.devRoleOverride = null;
		}

		this.refreshEditPermission();
		const label = this.devRoleOverride ? this.devRoleOverride.toUpperCase() : 'LIVE ROLE';
		this.showToast('success', `Role override: ${label}`);
	}


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

	loadData(): void {
		this.cancelEdit();
		this.loading = true;
		this.error = null;

		console.log(`Loading OTN KPI metrics for Year: ${this.selectedYear}, Month: ${this.selectedMonth}`);

		forkJoin({
			otnOp1: this.otnOp1Service.getAllKpis(),
			otnOp2: this.otnOp2Service.getAllKpis(),
		}).subscribe({
			next: ({ otnOp1, otnOp2 }) => {
				const otnOp1Kpis = otnOp1 || [];
				const otnOp2Kpis = otnOp2 || [];

				const otnOp1Metrics$ = otnOp1Kpis.length
					? forkJoin(
							otnOp1Kpis.map((kpi) =>
								this.otnOp1Service
									.getMetrics(kpi.id, this.selectedYear, this.selectedMonth)
									.pipe(
										catchError((err) => {
											console.warn('Failed to fetch OtnOp1 metrics for id:', kpi.id, err);
											return of([]);
										}),
										map((metrics) => ({ kpiId: kpi.id, metrics }))
									)
							)
						)
					: of([]);

				const otnOp2Metrics$ = otnOp2Kpis.length
					? forkJoin(
							otnOp2Kpis.map((kpi) =>
								this.otnOp2Service
									.getMetrics(kpi.id, this.selectedYear, this.selectedMonth)
									.pipe(
										catchError((err) => {
											console.warn('Failed to fetch OtnOp2 metrics for id:', kpi.id, err);
											return of([]);
										}),
										map((metrics) => ({ kpiId: kpi.id, metrics }))
									)
							)
						)
					: of([]);

				forkJoin({ op1Metrics: otnOp1Metrics$, op2Metrics: otnOp2Metrics$ }).subscribe({
					next: ({ op1Metrics, op2Metrics }) => {
						try {
							const op1MetricMap = new Map();
							(op1Metrics || []).forEach((item: any) => {
								if (item?.kpiId && item?.metrics) {
									op1MetricMap.set(item.kpiId, item.metrics);
								}
							});

							const op2MetricMap = new Map();
							(op2Metrics || []).forEach((item: any) => {
								if (item?.kpiId && item?.metrics) {
									op2MetricMap.set(item.kpiId, item.metrics);
								}
							});

							this.otnOp1Data = this.transformOtnOp1Records(otnOp1Kpis, op1MetricMap);
							this.otnOp2Data = this.transformOtnOp2Records(otnOp2Kpis, op2MetricMap);
							this.loading = false;
							this.cdr.detectChanges();
						} catch (mappingError) {
							console.error('Failed to transform OTN KPI data:', mappingError);
							this.otnOp1Data = [];
							this.otnOp2Data = [];
							this.loading = false;
							this.error = 'Failed to prepare OTN KPI data.';
							this.cdr.detectChanges();
						}
					},
					error: (metricsErr) => {
						console.error('Failed to load OTN KPI metrics:', metricsErr);
						this.otnOp1Data = [];
						this.otnOp2Data = [];
						this.loading = false;
						this.error = 'Failed to load OTN KPI metrics.';
						this.cdr.detectChanges();
					},
				});
			},
			error: (err) => {
				console.error('Failed to load OTN KPI data:', err);
				this.otnOp1Data = [];
				this.otnOp2Data = [];
				this.loading = false;
				this.error = 'Failed to load OTN KPI data.';
				this.cdr.detectChanges();
			},
		});
	}

	private transformOtnOp1Records(records: OtnOpKpi[], metricMap: Map<number, any>): OtnOp1Entry[] {
		return (Array.isArray(records) ? records : [])
			.map((record) => this.mapOtnOp1Record(record, metricMap.get(record.id)))
			.filter((entry): entry is OtnOp1Entry => Boolean(entry))
			.sort((a, b) => a.id - b.id);
	}

	private transformOtnOp2Records(records: OtnOpKpi[], metricMap: Map<number, any>): OtnOp2Entry[] {
		return (Array.isArray(records) ? records : [])
			.map((record) => this.mapOtnOp2Record(record, metricMap.get(record.id)))
			.filter((entry): entry is OtnOp2Entry => Boolean(entry))
			.sort((a, b) => a.id - b.id);
	}

	// Maps otn op1 record.
	private mapOtnOp1Record(record: OtnOpKpi, metrics?: any): OtnOp1Entry {
		// Extract metrics into dictionaries keyed by area/site
		const totalMinutes: Dict<any> = {};
		const unavailableMinutes: Dict<any> = {};
		const totalNodes: Dict<any> = {};
		const metricMeta: Dict<MetricMeta> = {};

		if (Array.isArray(metrics)) {
			console.log(`OtnOp1 KPI ${record.id} metrics:`, metrics);
			metrics.forEach((metric: any) => {
				const siteKey = this.toCanonicalSiteKey(metric.site);
				if (siteKey) {
					totalMinutes[siteKey] = metric.totalMinutes ?? 0;
					unavailableMinutes[siteKey] = metric.unavailableMinutes ?? 0;
					totalNodes[siteKey] = metric.totalNodes ?? 0;
					metricMeta[siteKey] = {
						id: metric.id,
						site: metric.site,
					};
				}
			});
		} else if (metrics) {
			console.warn(`OtnOp1 KPI ${record.id} metrics is not an array:`, metrics);
		}

		const entry: OtnOp1Entry = {
			id: record.id,
			networkEngineerKpi: record.networkEngineerKpi || '—',
			division: record.division || '—',
			section: record.section || '—',
			kpiPercent: record.kpiPercent || 0,
			formType: 'OtnOp1',
			totalMinutes,
			unavailableMinutes,
			totalNodes,
			metricMeta,
		};
		return entry;
	}

	// Maps otn op2 record.
	private mapOtnOp2Record(record: OtnOpKpi, metrics?: any): OtnOp2Entry {
		// Extract metrics into dictionaries keyed by area/site
		const totalFailedLinks: Dict<any> = {};
		const linksSlaNotViolated: Dict<any> = {};
		const metricMeta: Dict<MetricMeta> = {};

		if (Array.isArray(metrics)) {
			console.log(`OtnOp2 KPI ${record.id} metrics:`, metrics);
			metrics.forEach((metric: any) => {
				const siteKey = this.toCanonicalSiteKey(metric.site);
				if (siteKey) {
					totalFailedLinks[siteKey] = metric.totalFailedLinks ?? 0;
					linksSlaNotViolated[siteKey] = metric.linksSlaNotViolated ?? 0;
					metricMeta[siteKey] = {
						id: metric.id,
						site: metric.site,
					};
				}
			});
		} else if (metrics) {
			console.warn(`OtnOp2 KPI ${record.id} metrics is not an array:`, metrics);
		}

		const entry: OtnOp2Entry = {
			id: record.id,
			networkEngineerKpi: record.networkEngineerKpi || '—',
			division: record.division || '—',
			section: record.section || '—',
			kpiPercent: record.kpiPercent || 0,
			formType: 'OtnOp2',
			totalFailedLinks,
			linksSlaNotViolated,
			metricMeta,
		};
		return entry;
	}

	private pickFirst(record: any, keys: string[], fallback: any = undefined): any {
		if (!record) {
			return fallback;
		}
		for (const key of keys) {
			if (record[key] !== undefined && record[key] !== null) {
				return record[key];
			}
		}
		return fallback;
	}

	private collectDict(record: any, baseKey: string): Dict<any> {
		if (!record) {
			return {};
		}
		const containers = [null, 'metrics', 'snapshot', 'payload', 'data', 'values', 'details'];
		const variants = this.buildKeyVariants(baseKey);
		for (const container of containers) {
			const rawContainer = container ? record?.[container] : record;
			const containerValue = this.parseContainer(rawContainer);
			if (!containerValue || typeof containerValue !== 'object') {
				continue;
			}
			for (const variant of variants) {
				if (Object.prototype.hasOwnProperty.call(containerValue, variant)) {
					const normalized = this.normalizeDict((containerValue as any)[variant]);
					if (Object.keys(normalized).length) {
						return normalized;
					}
				}
			}
		}
		return {};
	}

	private buildKeyVariants(baseKey: string): string[] {
		const cleaned = baseKey.replace(/[^A-Za-z0-9]/g, '').toLowerCase();
		const camel = baseKey.replace(/_([a-z])/g, (_, char: string) => char.toUpperCase());
		const pascal = camel.charAt(0).toUpperCase() + camel.slice(1);
		const snakePascal = baseKey.replace(/(^|_)([a-z])/g, (_, sep: string, char: string) => `${sep}${char.toUpperCase()}`);
		return Array.from(
			new Set([
				baseKey,
				baseKey.toLowerCase(),
				baseKey.toUpperCase(),
				snakePascal,
				camel,
				camel.toLowerCase(),
				pascal,
				pascal.toLowerCase(),
				cleaned,
				`${camel}Json`,
				`${pascal}Json`,
				`${snakePascal}Json`,
				`${baseKey}_json`,
				`${cleaned}Json`,
			])
		);
	}

	private parseContainer(value: any): any {
		if (value === undefined || value === null) {
			return null;
		}
		if (typeof value === 'string') {
			const trimmed = value.trim();
			if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
				try {
					return JSON.parse(trimmed);
				} catch (err) {
					console.warn('Failed to parse KPI snapshot container:', err);
					return null;
				}
			}
			return null;
		}
		return value;
	}

	private normalizeDict(value: any): Dict<any> {
		if (value === undefined || value === null || value === '') {
			return {};
		}
		let source = value;
		if (typeof source === 'string') {
			const trimmed = source.trim();
			if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
				try {
					source = JSON.parse(trimmed);
				} catch (err) {
					console.warn('Failed to parse KPI snapshot payload:', err);
					return {};
				}
			} else {
				return {};
			}
		}
		if (Array.isArray(source)) {
			return source.reduce((acc: Dict<any>, item: any) => {
				if (!item || typeof item !== 'object') {
					return acc;
				}
				const keyCandidate = item.key ?? item.area ?? item.label ?? item.name ?? item.code ?? item.id ?? '';
				const normalizedKey = this.norm(keyCandidate);
				if (!normalizedKey) {
					return acc;
				}
				const valueCandidate =
					item.value ??
					item.minutes ??
					item.total ??
					item.count ??
					item.amount ??
					item.metric ??
					item.data;
				if (valueCandidate !== undefined && valueCandidate !== null && valueCandidate !== '') {
					acc[normalizedKey] = valueCandidate;
				}
				return acc;
			}, {} as Dict<any>);
		}
		if (typeof source === 'object') {
			return Object.keys(source).reduce((acc: Dict<any>, key: string) => {
				const normalizedKey = this.norm(key);
				if (!normalizedKey) {
					return acc;
				}
				const metricValue = (source as any)[key];
				if (metricValue === undefined || metricValue === null || metricValue === '') {
					return acc;
				}
				acc[normalizedKey] = metricValue;
				return acc;
			}, {} as Dict<any>);
		}
		return {};
	}

	formatSnapshotValue(value: any): string {
		if (value === undefined || value === null || value === '') {
			return '—';
		}
		const numeric = Number(value);
		if (!Number.isNaN(numeric)) {
			return Number.isInteger(numeric) ? numeric.toString() : numeric.toFixed(2);
		}
		return String(value);
	}

	private updateDropdown2Options(region: string): void {
		if (!region) {
			this.dropdown2Options = [];
			return;
		}

		this.dropdown2Options = Array.from(
			new Set(
				this.regionTable
					.filter((row) => row.region === region)
					.map((row) => row.province)
					.filter(Boolean) as string[]
			)
		);
	}

	private updateDropdown3Options(province: string): void {
		if (!province || !this.formValues.dropdown1) {
			this.dropdown3Options = [];
			return;
		}

		this.dropdown3Options = Array.from(
			new Set(
				this.regionTable
					.filter(
						(row) => row.region === this.formValues.dropdown1 && row.province === province
					)
					.map((row) => row.networkEngineer)
					.filter(Boolean) as string[]
			)
		);
		
		this.engineerNameMap = buildEngineerDisplayMap(this.regionTable, this.dropdown3Options);
	}

	private updateDropdown4Options(engineer: string): void {
		if (!engineer || !this.formValues.dropdown1 || !this.formValues.dropdown2) {
			this.dropdown4Options = [];
			return;
		}

		const leas = this.regionTable
			.filter(
				(row) =>
					row.region === this.formValues.dropdown1 &&
					row.province === this.formValues.dropdown2 &&
					row.networkEngineer === engineer
			)
			.map((row) => this.toCanonicalSiteKey(row.lea))
			.filter(Boolean);

		this.dropdown4Options = Array.from(new Set(leas));
	}

	private initializeFilters(): void {
		if (this.filtersInitialized) return;
		
		this.formValues.dropdown1 = '';
		this.formValues.dropdown2 = '';
		this.formValues.dropdown3 = '';
		this.formValues.dropdown4 = '';
		
		this.filtersInitialized = true;
	}

	onPeriodChange(): void {
		this.cancelEdit();
		this.loadData();
	}


	onDropdownChange(
		name: 'dropdown1' | 'dropdown2' | 'dropdown3' | 'dropdown4',
		value: string
	): void {
		if (name === 'dropdown1') {
			this.formValues.dropdown1 = value;
			this.formValues.dropdown2 = '';
			this.formValues.dropdown3 = '';
			this.formValues.dropdown4 = '';
			this.updateDropdown2Options(value);
			this.dropdown3Options = [];
			this.dropdown4Options = [];
			this.cancelEdit();
			return;
		}

		if (name === 'dropdown2') {
			this.formValues.dropdown2 = value;
			this.formValues.dropdown3 = '';
			this.formValues.dropdown4 = '';
			this.updateDropdown3Options(value);
			this.dropdown4Options = [];
			this.cancelEdit();
			return;
		}

		if (name === 'dropdown3') {
			this.formValues.dropdown3 = value;
			this.formValues.dropdown4 = '';
			this.updateDropdown4Options(value);
			this.cancelEdit();
			return;
		}

	this.formValues.dropdown4 = value;
	this.cancelEdit();

	}

	calculatePercentageOtnOp1(
		totalMinutes: any,
		unavailableMinutes: any,
		totalNodes: any,
		meta?: { month?: number; year?: number }
	): number | null {
		const tm = Number(totalMinutes) || 0;
		const um = Number(unavailableMinutes) || 0;
		const tn = Number(totalNodes) || 0;

    const denominator = tm > 0 ? tm : (24 * 60 * this.getDaysInMonth(meta?.year ?? this.selectedYear, meta?.month ?? this.selectedMonth) * tn);
    if (denominator <= 0) {
      return null;
    }

    const numerator = denominator - um;
    const pct = (100 * numerator) / denominator;
    return Math.max(0, Math.min(100, pct));
  }

	calculatePercentageOtnOp2(totalFailed: any, slaNotViolated: any): number {
		const failed = Number(totalFailed) || 0;
		const ok = Number(slaNotViolated) || 0;
		if (failed === 0) {
			return 100;
		}
		const pct = (100 * ok) / failed;
		return Math.max(0, Math.min(100, pct));
	}

	startEdit(
		entry: OtnOp1Entry | OtnOp2Entry,
		parentKey:
			| 'totalMinutes'
			| 'unavailableMinutes'
			| 'totalNodes'
			| 'totalFailedLinks'
			| 'linksSlaNotViolated'
	): void {
		if (!this.canEditMetrics || this.saving || !this.selectedKey) {
			return;
		}

		const value = (entry as any)[parentKey]?.[this.selectedKey] ?? '';
		this.editCell = {
			rowId: entry.id,
			parentKey,
			childKey: this.selectedKey,
			value: value === undefined || value === null ? '' : String(value),
			formType: entry.formType,
		};
	}

	onEditInput(value: string | number | null | undefined): void {
		const normalizedValue = value === null || value === undefined ? '' : String(value);
		this.editCell = { ...this.editCell, value: normalizedValue };
	}

	async doneEdit(): Promise<void> {
		if (
			!this.editCell.rowId ||
			!this.editCell.parentKey ||
			!this.editCell.childKey ||
			!this.editCell.formType ||
			this.saving
		) {
			return;
		}

		const { rowId, parentKey, childKey, value, formType } = this.editCell;
		const trimmedValue = value?.toString().trim() ?? '';
		const numericValue = trimmedValue === '' ? null : Number(trimmedValue);
		const nextValue = numericValue === null || Number.isFinite(numericValue) ? numericValue : null;
		let updatedEntry: OtnOp1Entry | OtnOp2Entry | null = null;

		if (formType === 'OtnOp1') {
			this.otnOp1Data = this.otnOp1Data.map((entry) => {
				if (entry.id !== rowId) {
					return entry;
				}

				const parent = { ...(entry as any)[parentKey] };
				parent[childKey] = nextValue;

				const next: OtnOp1Entry = {
					...entry,
					[parentKey]: parent,
				} as OtnOp1Entry;

				if (parentKey === 'totalNodes') {
					const nodes = typeof nextValue === 'number' ? nextValue : 0;
					const days = this.getDaysForEntry(entry, childKey);
					const computed = 24 * 60 * days * nodes;
					next.totalMinutes = {
						...(entry.totalMinutes || {}),
						[childKey]: computed,
					};
				}

				updatedEntry = next;
				return next;
			});
		} else {
			this.otnOp2Data = this.otnOp2Data.map((entry) => {
				if (entry.id !== rowId) {
					return entry;
				}

				const parent = { ...(entry as any)[parentKey] };
				parent[childKey] = nextValue;

				const next: OtnOp2Entry = {
					...entry,
					[parentKey]: parent,
				} as OtnOp2Entry;

				updatedEntry = next;
				return next;
			});
		}

		const entryToPersist = updatedEntry;
		this.cancelEdit();

		if (!entryToPersist) {
			this.showToast('danger', 'Unable to locate the edited record. Please retry.');
			return;
		}

		await this.persistMetricChange(entryToPersist, childKey);
	}

	cancelEdit(): void {
		this.editCell = {
			rowId: null,
			parentKey: null,
			childKey: null,
			value: '',
			formType: null,
		};
	}

	isEditing(rowId: number, parentKey: string): boolean {
		return (
			this.editCell.rowId === rowId &&
			this.editCell.parentKey === parentKey &&
			this.editCell.childKey === this.selectedKey
		);
	}

	handleEditKey(event: KeyboardEvent): void {
		if (event.key === 'Enter') {
			event.preventDefault();
			this.doneEdit();
		} else if (event.key === 'Escape') {
			event.preventDefault();
			this.cancelEdit();
		}
	}







	private async persistMetricChange(entry: OtnOp1Entry | OtnOp2Entry, siteKey: string): Promise<void> {

		const meta = entry.metricMeta?.[siteKey];
		const siteLabel = this.resolveSiteLabel(siteKey, meta);

		this.saving = true;
		try {
			if (entry.formType === 'OtnOp1') {
				const payload: OtnOp1Metric = {
					id: meta?.id ?? 0,
					otnOp1Id: entry.id,
					site: siteLabel,
					unavailableMinutes: Number(entry.unavailableMinutes?.[siteKey]) || 0,
					totalMinutes: Number(entry.totalMinutes?.[siteKey]) || 0,
					totalNodes: Number(entry.totalNodes?.[siteKey]) || 0,
					year: this.selectedYear,
					month: this.selectedMonth,
				};
				await firstValueFrom(this.otnOp1Service.upsertMetrics(entry.id, [payload]));
			} else {
				const payload: OtnOp2Metric = {
					id: meta?.id ?? 0,
					otnOp2Id: entry.id,
					site: siteLabel,
					totalFailedLinks: Number(entry.totalFailedLinks?.[siteKey]) || 0,
					linksSlaNotViolated: Number(entry.linksSlaNotViolated?.[siteKey]) || 0,
					year: this.selectedYear,
					month: this.selectedMonth,
				};
				await firstValueFrom(this.otnOp2Service.upsertMetrics(entry.id, [payload]));
			}

			this.showToast('success', 'Metric updated successfully.');
		} catch (error) {
			console.error('Failed to persist metric change:', error);
			this.showToast('danger', 'Failed to save change. Reloading latest data.');
			this.loadData();
		} finally {
			this.saving = false;
		}
	}

	private resolveSiteLabel(siteKey: string, meta?: MetricMeta): string {
		if (meta?.site) {
			return meta.site;
		}
		const friendly = this.optionMapping[siteKey];
		if (friendly) {
			return friendly.replace(/[^A-Za-z0-9]/g, '').toUpperCase();
		}
		return siteKey.toUpperCase();
	}

	async exportToExcel(): Promise<void> {
		const workbook = new ExcelJS.Workbook();
		const worksheet = workbook.addWorksheet('OTN KPI');

		worksheet.addRow(['KPI (Fiber Failures Restoration & Network Availability)']);
		worksheet.addRow([`Generated Date: ${new Date().toISOString().split('T')[0]}`]);
		worksheet.addRow([]);

		const areas = this.getExportAreaKeys();

		const headers = [
			'ID',
			'Network Engineer KPI',
			'Division',
			'Section',
			'KPI Percent',
			...areas.map((area) => this.resolveAreaHeader(area)),
		];

		const headerRow = worksheet.addRow(headers);
		headerRow.eachCell((cell: ExcelJS.Cell) => {
			cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: '0070C0' } };
			cell.font = { bold: true, color: { argb: 'FFFFFF' } };
			cell.alignment = { vertical: 'middle', horizontal: 'center' };
			cell.border = {
				top: { style: 'thin' },
				left: { style: 'thin' },
				bottom: { style: 'thin' },
				right: { style: 'thin' },
			};
		});

		this.combinedData.forEach((entry) => {
			const baseRow: any[] = [
				entry.id,
				entry.networkEngineerKpi,
				entry.division,
				entry.section,
				entry.kpiPercent,
			];

			areas.forEach((area) => {
				if (entry.formType === 'OtnOp1') {
					const pct = this.calculatePercentageOtnOp1(
						(entry as OtnOp1Entry).totalMinutes?.[area],
						(entry as OtnOp1Entry).unavailableMinutes?.[area],
						(entry as OtnOp1Entry).totalNodes?.[area],
						(entry as OtnOp1Entry).metricMeta?.[area]
					);
					baseRow.push(pct === null ? '-' : `${pct.toFixed(2)}%`);
				} else {
					const pct = this.calculatePercentageOtnOp2(
						(entry as OtnOp2Entry).totalFailedLinks?.[area],
						(entry as OtnOp2Entry).linksSlaNotViolated?.[area]
					);
					baseRow.push(isNaN(pct) ? '' : `${pct.toFixed(2)}%`);
				}
			});

			const bodyRow = worksheet.addRow(baseRow);
			bodyRow.eachCell((cell: ExcelJS.Cell) => {
				cell.alignment = { vertical: 'middle', horizontal: 'center' };
				cell.border = {
					top: { style: 'thin' },
					left: { style: 'thin' },
					bottom: { style: 'thin' },
					right: { style: 'thin' },
				};
			});

			if (entry.formType === 'OtnOp1') {
				const totalMinutesRow: any[] = ['', 'Total Minutes', '', '', ''];
				const unavailableRow: any[] = ['', 'Unavailable Minutes', '', '', ''];
				const totalNodesRow: any[] = ['', 'Total Nodes', '', '', ''];

				areas.forEach((area) => {
					const nodes = Number((entry as OtnOp1Entry).totalNodes?.[area]) || 0;
					const manual = Number((entry as OtnOp1Entry).totalMinutes?.[area]) || 0;
					const days = this.getDaysForEntry(entry as OtnOp1Entry, area);
					const computed = 24 * 60 * days * nodes;
					totalMinutesRow.push(manual || computed || '');
					unavailableRow.push((entry as OtnOp1Entry).unavailableMinutes?.[area] ?? '');
					totalNodesRow.push((entry as OtnOp1Entry).totalNodes?.[area] ?? '');
				});

				[totalMinutesRow, unavailableRow, totalNodesRow].forEach((rowData) => {
					const row = worksheet.addRow(rowData);
					row.eachCell((cell: ExcelJS.Cell) => {
						cell.alignment = { vertical: 'middle', horizontal: 'center' };
						cell.border = {
							top: { style: 'thin' },
							left: { style: 'thin' },
							bottom: { style: 'thin' },
							right: { style: 'thin' },
						};
					});
				});
			} else {
				const totalFailedRow: any[] = ['', 'Total Failed Links', '', '', ''];
				const slaRow: any[] = ['', 'Links SLA Not Violated', '', '', ''];

				areas.forEach((area) => {
					totalFailedRow.push((entry as OtnOp2Entry).totalFailedLinks?.[area] ?? '');
					slaRow.push((entry as OtnOp2Entry).linksSlaNotViolated?.[area] ?? '');
				});

				[totalFailedRow, slaRow].forEach((rowData) => {
					const row = worksheet.addRow(rowData);
					row.eachCell((cell: ExcelJS.Cell) => {
						cell.alignment = { vertical: 'middle', horizontal: 'center' };
						cell.border = {
							top: { style: 'thin' },
							left: { style: 'thin' },
							bottom: { style: 'thin' },
							right: { style: 'thin' },
						};
					});
				});
			}
		});

		worksheet.columns.forEach((col) => {
			if (col) {
				col.width = 18;
			}
		});

		const buffer = await workbook.xlsx.writeBuffer();
		const blob = new Blob([buffer], {
			type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
		});

		const link = document.createElement('a');
		link.href = URL.createObjectURL(blob);
		link.download = `OTN_KPI_${new Date().toISOString().split('T')[0]}.xlsx`;
		link.click();
		URL.revokeObjectURL(link.href);
	}
}
