/*
 File: bb-anw.component.ts
 Description: Broadband & Access Network KPI platform page
 Purpose: Display and manage BB ANW KPI metrics for assigned platforms.
 Features: Data visualization, export to Excel, node-level analytics
*/

import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, OnInit, ChangeDetectorRef } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../../../services/auth.service';
import { BbAnwDto, BbAnwService } from '../../../../services/bb-anw.service';
import { Region, RegionService } from '../../../../services/region.service';
import { FilterUtils } from '../../../../utils/filter.utils';
import { buildAreaLabels, buildEngineerDisplayMap, getScopedLeas, mapRegionRecords } from '../../../../utils/region-display.utils';
import * as ExcelJS from 'exceljs';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../../environments/environment';

/* ========== DATA TYPES ========== */

type Dict<T = any> = Record<string, T>;

type MetricKey = 'unavailableMinutes' | 'totalMinutes' | 'totalNodes';

interface RegionRow {
	region?: string;
	province?: string;
	networkEngineer?: string;
	lea?: string;
	engName?: string;
}

interface NodeMeta {
	month: number;
	year: number;
}

interface BbAnwEntry {
	id: number;
	order: number;
	networkEngineerKpi: string;
	division?: string | null;
	section?: string | null;
	kpiPercent?: number | null;
	totalMinutes: Dict<number | null>;
	unavailableMinutes: Dict<number | null>;
	totalNodes: Dict<number | null>;
	nodeMeta: Dict<NodeMeta>;
	nodeCodes: Dict<string>;
}

interface EditCellState {
	rowId: number | null;
	key: string | null;
	value: string;
}

interface RtomAreaLookup {
	areaCode?: string;
	displayName?: string;
	AreaCode?: string;
	DisplayName?: string;
}

@Component({
	selector: 'app-bb-anw',
	standalone: true,
	imports: [CommonModule, FormsModule],
	templateUrl: './bb-anw.component.html',
	styleUrls: ['./bb-anw.component.scss'],
})
export class BbAnwComponent implements OnInit, OnDestroy {

	pageTitle = 'Wireline Access NW';

	data: BbAnwEntry[] = [];
	private allEntries: BbAnwEntry[] = [];
	regionTable: RegionRow[] = [];
	adminRows: BbAnwDto[] = [];

	loading = true;
	error: string | null = null;
	cellSaving = false;

	isEditingAllowed = false;
	devRoleOverride: 'padmin' | 'user' | null = null;

	private permissionTimer: ReturnType<typeof setInterval> | null = null;

	formValues = {
		dropdown1: '',
		dropdown2: '',
		dropdown3: '',
	};

	dropdown2Options: string[] = [];
	dropdown3Options: string[] = [];
	engineerNameMap: Record<string, string> = {};

	// Database-backed area columns and their NW EE header groups.
	areaKeys: string[] = [];
	private areaLabels: Record<string, string> = {};
	areaEngineerGroups: { label: string; areaKeys: string[] }[] = [];

	selectedYear: number = new Date().getFullYear();
	selectedMonth: number = new Date().getMonth() + 1;
	yearOptions: number[] = FilterUtils.generatePlatformYearOptions();
	get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }

	editCell: EditCellState = {
		rowId: null,
		key: null,
		value: '',
	};

	toasts: Array<{ id: number; type: 'success' | 'danger'; text: string }> = [];
	private toastId = 1;

	private metricLabelMap: Record<MetricKey, string> = {
		unavailableMinutes: 'Unavailable minutes',
		totalMinutes: 'Total minutes',
		totalNodes: 'Total nodes',
	};

	private databaseAreaKeyByCode: Record<string, string> = {};
	private databaseAreaKeysByLabel: Record<string, string[]> = {};
	private databaseAreaCodeByKey: Record<string, string> = {};
	private databaseAreaLabelByKey: Record<string, string> = {};
	private filtersInitialized = false;

	// Injects the required dependencies.
	constructor(
		private http: HttpClient,
		private regionService: RegionService,
		private bbAnwService: BbAnwService,
		private authService: AuthService,
		private cdr: ChangeDetectorRef
	) {}


	ngOnInit(): void {
		this.initializePeriodDefaults();
		this.refreshEditPermission();
		this.loadRegionTable();
		this.loadRtomAreaLookup();
		this.initializeFilters();
		this.loadData();

		this.permissionTimer = setInterval(() => this.refreshEditPermission(), 60000);
	}



	// Cleans up resources such as the permission timer.
	ngOnDestroy(): void {
		if (this.permissionTimer) {
			clearInterval(this.permissionTimer);
			this.permissionTimer = null;
		}
	}

	// Gets unique region names from the region table.
	get regions(): string[] {
		return Array.from(new Set(this.regionTable.map((row) => row.region).filter(Boolean) as string[]));
	}

	// Gets the display label for an area key (disambiguated when two keys share a label).
	areaLabel(key: string): string {
		return this.areaLabels[key] ?? this.baseAreaLabel(key);
	}

	get groupedAreaKeys(): string[] {
		return this.areaEngineerGroups.flatMap((group) => group.areaKeys);
	}

	private baseAreaLabel(key: string): string {
		return this.databaseAreaLabelByKey[key] ?? key.toUpperCase();
	}

	// Gets the label for the selected month.
	get selectedMonthLabel(): string {
		return this.monthOptions.find((month) => month.value === this.selectedMonth)?.label ?? 'Month';
	}

	// Calculates and returns an area's availability percentage.
	getAreaAvailability(entry: BbAnwEntry, key: string): string {
		if (!key) return '--';

		const unavailable = entry.unavailableMinutes?.[key];
		const total = entry.totalMinutes?.[key];
		const nodes = entry.totalNodes?.[key];
		const hasData = [unavailable, total, nodes].some((value) => value !== undefined && value !== null);
		if (!hasData) return '-';

		const meta = entry.nodeMeta?.[key];
		const pct = this.calculatePercentage(total, unavailable, nodes, meta);
		return pct === null ? '-' : `${pct.toFixed(2)}%`;
	}

	// Gets the display string for a specific metric in an area.
	getMetricDisplay(entry: BbAnwEntry, metric: MetricKey, key: string): string {
		if (!key) return '--';
		const source = (entry as any)[metric] ?? {};
		const rawValue = source[key];
		if (rawValue === undefined || rawValue === null || rawValue === '') return '--';
		return typeof rawValue === 'number' ? String(rawValue) : `${rawValue}`;
	}

	// Normalizes a string by removing non-alphanumeric characters and converting to lowercase.
	private norm(value: string | null | undefined): string {
		return value ? value.replace(/[^A-Za-z0-9]/g, '').toLowerCase() : '';
	}

	private resolveAreaKeys(value: string | null | undefined): string[] {
		const normalized = this.norm(value);
		if (!normalized) return [];
		const codeKey = this.databaseAreaKeyByCode[normalized];
		if (codeKey) return [codeKey];
		return this.databaseAreaKeysByLabel[normalized] ?? [normalized];
	}

	private resolveMetricAreaKey(value: string | null | undefined): string {
		const normalized = this.norm(value);
		if (!normalized) return '';
		const codeKey = this.databaseAreaKeyByCode[normalized];
		if (codeKey) return codeKey;
		const labelKeys = this.databaseAreaKeysByLabel[normalized];
		return labelKeys?.length === 1 ? labelKeys[0] : normalized;
	}

	private loadRtomAreaLookup(): void {
		this.http.get<RtomAreaLookup[]>(`${environment.apiUrl}/rtom-areas`).subscribe({
			next: (areas) => {
				this.databaseAreaKeyByCode = {};
				this.databaseAreaKeysByLabel = {};
				this.databaseAreaCodeByKey = {};
				this.databaseAreaLabelByKey = {};

				(areas ?? []).forEach((area) => {
					const code = (area.areaCode ?? area.AreaCode)?.trim();
					if (!code) return;
					const key = this.norm(code);
					if (!key) return;
					this.databaseAreaKeyByCode[key] = key;
					this.databaseAreaCodeByKey[key] = code;

					const label = (area.displayName ?? area.DisplayName)?.trim();
					if (label) {
						const normalizedLabel = this.norm(label);
						if (normalizedLabel) {
							const aliases = this.databaseAreaKeysByLabel[normalizedLabel] ?? [];
							if (!aliases.includes(key)) aliases.push(key);
							this.databaseAreaKeysByLabel[normalizedLabel] = aliases;
						}
						this.databaseAreaLabelByKey[key] = label;
					}
				});

				if (!areas?.length) {
					console.error('RTOM area lookup returned no database records.');
					this.showToast('danger', 'RTOM area lookup is empty; using region LEA codes and stored NodeCodes.');
				}
				if (this.adminRows.length) {
					this.allEntries = this.adminRows.map((row, index) => this.mapDtoToEntry(row, index + 1));
					this.applyPeriodFilter();
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

	// Rebuild columns and NW EE groups using region data and stored NodeCodes.
	private refreshAreaKeys(): void {
		const { dropdown1, dropdown2, dropdown3 } = this.formValues;
		const scopedRegions = this.regionTable.filter((region) =>
			(!dropdown1 || region.region === dropdown1) &&
			(!dropdown2 || region.province === dropdown2) &&
			(!dropdown3 || region.networkEngineer === dropdown3)
		);
		const labelsByAreaKey = new Map<string, string>();
		this.regionTable.forEach((region) => {
			this.resolveAreaKeys(region.lea).forEach((key) => {
				if (key && !labelsByAreaKey.has(key)) {
					labelsByAreaKey.set(key, this.databaseAreaLabelByKey[key] || region.lea?.trim() || key);
				}
			});
		});
		const keys = new Set(
			getScopedLeas(this.regionTable, { region: dropdown1, province: dropdown2, engineer: dropdown3 })
				.flatMap((lea) => this.resolveAreaKeys(lea))
				.filter(Boolean)
		);
		if (!dropdown1) {
			this.data.forEach((entry) => this.collectNodeCodes(entry).forEach((code) => keys.add(code)));
		}
		const labelOf = (key: string) => labelsByAreaKey.get(key) || this.baseAreaLabel(key);
		this.areaKeys = Array.from(keys).sort((a, b) => labelOf(a).localeCompare(labelOf(b)));
		this.areaLabels = buildAreaLabels(this.areaKeys, labelOf);

		const groups = new Map<string, Set<string>>();
		const assignedAreaKeys = new Set<string>();
		scopedRegions.forEach((region) => {
			const engineer = region.networkEngineer?.trim() || 'Unassigned NW EE';
			this.resolveAreaKeys(region.lea).forEach((key) => {
				if (!keys.has(key) || assignedAreaKeys.has(key)) return;
				if (!groups.has(engineer)) groups.set(engineer, new Set<string>());
				groups.get(engineer)!.add(key);
				assignedAreaKeys.add(key);
			});
		});
		const groupedKeys = new Set(Array.from(groups.values()).flatMap((areaKeys) => Array.from(areaKeys)));
		const unassignedKeys = this.areaKeys.filter((key) => !groupedKeys.has(key));
		if (unassignedKeys.length) groups.set('Unassigned NW EE', new Set(unassignedKeys));
		this.areaEngineerGroups = Array.from(groups.entries())
			.map(([engineer, areaKeys]) => ({
				label: this.engineerNameMap[engineer] || engineer,
				areaKeys: this.areaKeys.filter((key) => areaKeys.has(key))
			}))
			.filter((group) => group.areaKeys.length > 0)
			.sort((a, b) => a.label.localeCompare(b.label));
	}

	// Displays a toast notification.
	private showToast(type: 'success' | 'danger', text: string): void {
		const id = this.toastId++;
		this.toasts.push({ id, type, text });
		setTimeout(() => this.dismissToast(id), 2800);
	}

	// Dismisses a specific toast notification.
	dismissToast(id: number): void {
		this.toasts = this.toasts.filter((toast) => toast.id !== id);
	}

	// Refreshes the user's edit permission status.
	private refreshEditPermission(): void {
		if (this.devRoleOverride) {
			this.isEditingAllowed = this.devRoleOverride === 'padmin';
			return;
		}
		this.isEditingAllowed = this.authService.canEditPage('Wireline Access NW');
	}

	// Toggles the developer role override for testing edit permissions.
	toggleRoleOverride(): void {
		if (!this.devRoleOverride) this.devRoleOverride = 'padmin';
		else if (this.devRoleOverride === 'padmin') this.devRoleOverride = 'user';
		else this.devRoleOverride = null;

		this.refreshEditPermission();
		const label = this.devRoleOverride ? this.devRoleOverride.toUpperCase() : 'LIVE ROLE';
		this.showToast('success', `Role override: ${label}`);
	}

	// Loads the region table from the region service.
	loadRegionTable(): void {
		this.regionService.getAll().subscribe({
			next: (res: Region[] | any[]) => {
				this.regionTable = mapRegionRecords(res) as RegionRow[];
				this.engineerNameMap = buildEngineerDisplayMap(this.regionTable);
				this.initializeFilters();
			},
			error: (err) => {
				console.error('Failed to fetch region table from API:', err);
				this.regionTable = [];
				this.showToast('danger', 'Failed to load region data. Filters are unavailable until regiondata is reachable.');
				this.initializeFilters();
			},
		});
	}

	// Loads broadband and access network KPI data for the selected period.
	loadData(): void {
		this.loading = true;
		this.error = null;

		this.bbAnwService.getAll(this.selectedMonth, this.selectedYear).subscribe({
			next: (rows) => {
				const list = Array.isArray(rows) ? rows : [];
				this.adminRows = list;
				this.allEntries = list.map((row, index) => this.mapDtoToEntry(row, index + 1));
				this.applyPeriodFilter();
				this.loading = false;
				this.cdr.detectChanges();
			},
			error: (err) => {
				console.error('Failed to load BB & ANW admin data:', err);
				this.adminRows = [];
				this.data = [];
				this.loading = false;
				this.error = 'Failed to load BB & ANW KPI data.';
				this.cdr.detectChanges();
			},
		});
	}

	// Maps dto to entry.
	private mapDtoToEntry(dto: BbAnwDto, order = 0): BbAnwEntry {
		const entry: BbAnwEntry = {
			id: dto.id ?? 0,
			order,
			networkEngineerKpi: dto.networkEngineerKpi,
			division: dto.division ?? null,
			section: dto.section ?? null,
			kpiPercent: dto.kpiPercent ?? null,
			totalMinutes: {},
			unavailableMinutes: {},
			totalNodes: {},
			nodeMeta: {},
			nodeCodes: {},
		};

		(dto.nodes ?? []).forEach((node) => {
			const code = this.resolveMetricAreaKey(node.nodeCode);
			if (!code) return;
			const meta = this.normalizeNodeMeta(node?.month, node?.year);
			if (meta.month !== this.selectedMonth || meta.year !== this.selectedYear) return;
			entry.nodeCodes[code] = node.nodeCode;
			entry.unavailableMinutes[code] = node.unavailableMinutes ?? null;
			entry.totalMinutes[code] = node.totalMinutes ?? null;
			entry.totalNodes[code] = node.totalNodes ?? null;
			entry.nodeMeta[code] = meta;
		});

		return entry;
	}

	// Builds a DTO from a local entry object.
	private buildDtoFromEntry(entry: BbAnwEntry): BbAnwDto {
		const codes = this.collectNodeCodes(entry);
		const nodes = codes
			.map((code) => {
				const meta = this.ensureNodeMeta(entry, code);
				return {
					nodeCode: entry.nodeCodes[code] ?? this.databaseAreaCodeByKey[code] ?? code,
					unavailableMinutes: this.toNullableNumber(entry.unavailableMinutes[code]),
					totalMinutes: this.toNullableNumber(entry.totalMinutes[code]),
					totalNodes: this.toNullableNumber(entry.totalNodes[code]),
					month: meta.month,
					year: meta.year,
				};
			})
			.filter((node) => node.unavailableMinutes != null || node.totalNodes != null || node.totalMinutes != null);

		return {
			id: entry.id,
			networkEngineerKpi: entry.networkEngineerKpi,
			division: entry.division ?? null,
			section: entry.section ?? null,
			kpiPercent: entry.kpiPercent ?? null,
			nodes,
		};
	}

	// Collects all unique node codes from the entry.
	private collectNodeCodes(entry: BbAnwEntry): string[] {
		const codes = new Set<string>();
		Object.keys(entry.unavailableMinutes || {}).forEach((key) => codes.add(key));
		Object.keys(entry.totalMinutes || {}).forEach((key) => codes.add(key));
		Object.keys(entry.totalNodes || {}).forEach((key) => codes.add(key));
		Object.keys(entry.nodeMeta || {}).forEach((key) => codes.add(key));
		return Array.from(codes).filter((code): code is string => Boolean(code));
	}

	// Ensures node metadata exists and returns it.
	private ensureNodeMeta(entry: BbAnwEntry, key: string): NodeMeta {
		if (!entry.nodeMeta[key]) entry.nodeMeta[key] = this.getDefaultNodeMeta();
		return entry.nodeMeta[key];
	}

	// Gets the default node metadata for the selected period.
	private getDefaultNodeMeta(): NodeMeta {
		return this.normalizeNodeMeta(this.selectedMonth, this.selectedYear);
	}

	// Normalizes node metadata with fallback to current selected period.
	private normalizeNodeMeta(month?: number | null, year?: number | null): NodeMeta {
		const now = new Date();
		const fallbackMonth = typeof this.selectedMonth === 'number' ? this.selectedMonth : now.getMonth() + 1;
		const fallbackYear = typeof this.selectedYear === 'number' ? this.selectedYear : now.getFullYear();
		const safeMonth = typeof month === 'number' && month >= 1 && month <= 12 ? month : fallbackMonth;
		const safeYear = typeof year === 'number' && year >= 1900 ? year : fallbackYear;
		return { month: safeMonth, year: safeYear };
	}

	// Calculates the number of days in the specified month and year.
	private getDaysInMonth(month?: number | null, year?: number | null): number {
		const meta = this.normalizeNodeMeta(month, year);
		return new Date(meta.year, meta.month, 0).getDate();
	}

	// Converts a value to a number, returning null if invalid.
	private toNullableNumber(value: any): number | null {
		if (value === undefined || value === null || value === '') return null;
		const numeric = typeof value === 'number' ? value : Number(value);
		return Number.isFinite(numeric) ? numeric : null;
	}

	// Updates the province dropdown options based on the selected region.
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

	// Updates the engineer dropdown options based on the selected province.
private updateDropdown3Options(province: string): void {

		if (!province || !this.formValues.dropdown1) {
			this.dropdown3Options = [];
			return;
		}
		this.dropdown3Options = Array.from(
			new Set(
				this.regionTable
					.filter((row) => row.region === this.formValues.dropdown1 && row.province === province)
					.map((row) => row.networkEngineer)
					.filter(Boolean) as string[]
			)
		);
		
		this.engineerNameMap = buildEngineerDisplayMap(this.regionTable, this.dropdown3Options);
	}

	// Initializes all filter dropdowns.
	private initializeFilters(): void {
		this.formValues = { dropdown1: '', dropdown2: '', dropdown3: '' };
		this.dropdown2Options = [];
		this.dropdown3Options = [];
		this.filtersInitialized = true;
		this.refreshAreaKeys();
	}

	// Initializes default selected month and year to the previous month.
	private initializePeriodDefaults(): void {
		const now = new Date();
		const currentYear = now.getFullYear();
		const previous = new Date(currentYear, now.getMonth() - 1, 1);
		this.selectedYear = previous.getFullYear();
		this.selectedMonth = previous.getMonth() + 1;
		this.applyPeriodFilter();
	}

	// Applies period filtering to the allEntries list.
	private applyPeriodFilter(): void {
		if (!this.allEntries.length) {
			this.data = [];
			this.refreshAreaKeys();
			return;
		}

		const month = this.selectedMonth;
		const year = this.selectedYear;
		this.data = this.allEntries.map((entry) => this.createPeriodScopedEntry(entry, month, year));
		this.refreshAreaKeys();
	}

	// Creates a new entry scoped to the specified period.
	private createPeriodScopedEntry(entry: BbAnwEntry, month: number, year: number): BbAnwEntry {
		const filteredUnavailable: Dict<number | null> = {};
		const filteredTotal: Dict<number | null> = {};
		const filteredNodes: Dict<number | null> = {};
		const filteredMeta: Dict<NodeMeta> = {};
		const filteredCodes: Dict<string> = {};
		const codes = this.collectNodeCodes(entry);

		codes.forEach((code) => {
			const rawMeta = entry.nodeMeta?.[code];
			const normalized = rawMeta ? this.normalizeNodeMeta(rawMeta.month, rawMeta.year) : { month, year };
			const matchesPeriod = normalized.month === month && normalized.year === year;
			if (matchesPeriod) {
				filteredUnavailable[code] = entry.unavailableMinutes?.[code] ?? null;
				filteredTotal[code] = entry.totalMinutes?.[code] ?? null;
				filteredNodes[code] = entry.totalNodes?.[code] ?? null;
				filteredMeta[code] = normalized;
				filteredCodes[code] = entry.nodeCodes?.[code] ?? this.databaseAreaCodeByKey[code] ?? code;
			} else {
				filteredUnavailable[code] = null;
				filteredTotal[code] = null;
				filteredNodes[code] = null;
				filteredMeta[code] = { month, year };
				filteredCodes[code] = entry.nodeCodes?.[code] ?? this.databaseAreaCodeByKey[code] ?? code;
			}
		});

		return {
			...entry,
			unavailableMinutes: filteredUnavailable,
			totalMinutes: filteredTotal,
			totalNodes: filteredNodes,
			nodeMeta: filteredMeta,
			nodeCodes: filteredCodes,
		};
	}

	// Handles changes to any of the dropdown filters.
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
		this.refreshAreaKeys();
		this.cancelEdit();
	}

	// Handles changes to the selected period by reloading data.
	onPeriodChange(): void {
		this.cancelEdit();
		this.loadData();
	}

	// Calculates the availability percentage.
	private calculatePercentage(totalMinutes: any, unavailableMinutes: any, totalNodes: any, meta?: NodeMeta): number | null {
		const tm = Number(totalMinutes) || 0;
		const um = Number(unavailableMinutes) || 0;
		const tn = Number(totalNodes) || 0;

    const days = this.getDaysInMonth(meta?.month, meta?.year);
    const denominator = tm > 0
      ? tm
      : 24 * 60 * days * tn;
    if (denominator <= 0) return null;

    const numerator = denominator - um;
    const pct = (100 * numerator) / denominator;
    return Math.max(0, Math.min(100, pct));
  }

	// Starts editing a specific metric cell for one area.
	startEdit(entry: BbAnwEntry, key: MetricKey, areaKey: string): void {
		if (key === 'totalMinutes') return; // computed
		if (!this.isEditingAllowed || !areaKey || this.cellSaving) return;

		const nestedKey = `${key}.${areaKey}`;
		const value = (entry as any)[key]?.[areaKey];
		this.editCell = {
			rowId: entry.id,
			key: nestedKey,
			value: value === undefined || value === null ? '' : String(value),
		};
	}

	// Handles input changes while editing a cell.
	onEditInput(value: string): void {
		this.editCell = { ...this.editCell, value };
	}

	// Finalizes and saves the edited cell value.
	async doneEdit(): Promise<void> {
		if (this.editCell.rowId === null || !this.editCell.key) return;

		const [rawParentKey, childKey] = this.editCell.key.split('.');
		const parentKey = rawParentKey as MetricKey;
		if (!childKey || !parentKey) {
			this.cancelEdit();
			return;
		}

		const newValue = this.editCell.value;
		const updatedEntry = this.findAndUpdateEntry(parentKey, childKey, newValue);
		if (!updatedEntry || !updatedEntry.id) {
			this.cancelEdit();
			return;
		}

		this.cellSaving = true;
		try {
			await firstValueFrom(this.bbAnwService.update(updatedEntry.id, this.buildDtoFromEntry(updatedEntry)));
			const metricLabel = this.metricLabelMap[parentKey] || 'KPI metric';
			this.showToast('success', `${metricLabel} saved.`);
		} catch (error) {
			console.error('Failed to save KPI metric:', error);
			this.showToast('danger', 'Failed to save metric. Please try again.');
		} finally {
			this.cellSaving = false;
			this.cancelEdit();
		}
	}

	// Finds an entry and updates its metric value in the local state.
	private findAndUpdateEntry(parentKey: MetricKey, childKey: string, newValue: string): BbAnwEntry | null {
		if (parentKey === 'totalMinutes') return null;
		let filteredEntry: BbAnwEntry | null = null;

		this.data = this.data.map((entry) => {
			if (entry.id !== this.editCell.rowId) return entry;

			const next: BbAnwEntry = this.createPeriodScopedEntry(entry, this.selectedMonth, this.selectedYear);
			(next as any)[parentKey][childKey] = this.toNullableNumber(newValue);

			(next as any).nodeMeta = { ...(next as any).nodeMeta };
			(next as any).nodeMeta[childKey] = { month: this.selectedMonth, year: this.selectedYear };

			if (parentKey === 'totalNodes') {
				const nodeVal = (next as any).totalNodes?.[childKey] ?? null;
				if (nodeVal == null) {
					next.totalMinutes = { ...(next.totalMinutes || {}), [childKey]: null };
				} else {
					const days = this.getDaysInMonth(this.selectedMonth, this.selectedYear);
					next.totalMinutes = { ...(next.totalMinutes || {}), [childKey]: 24 * 60 * days * nodeVal };
				}
			}

			filteredEntry = next;
			return next;
		});

		const targetEntry = filteredEntry as BbAnwEntry | null;
		if (!targetEntry) return null;
		const targetId = targetEntry.id;

		this.allEntries = this.allEntries.map((entry) => {
			if (entry.id !== targetId) return entry;

			const next: BbAnwEntry = this.createPeriodScopedEntry(entry, this.selectedMonth, this.selectedYear);
			(next as any)[parentKey][childKey] = this.toNullableNumber(newValue);

			(next as any).nodeMeta = { ...(next as any).nodeMeta };
			(next as any).nodeMeta[childKey] = { month: this.selectedMonth, year: this.selectedYear };

			if (parentKey === 'totalNodes') {
				const nodeVal = next.totalNodes?.[childKey] ?? null;
				if (nodeVal == null) {
					next.totalMinutes = { ...next.totalMinutes, [childKey]: null };
				} else {
					const days = this.getDaysInMonth(this.selectedMonth, this.selectedYear);
					next.totalMinutes = { ...next.totalMinutes, [childKey]: 24 * 60 * days * nodeVal };
				}
			}

			return next;
		});

		return this.allEntries.find((entry) => entry.id === targetId) ?? null;
	}

	// Checks if a specific cell is currently being edited.
	isEditingCell(entry: BbAnwEntry, key: MetricKey, areaKey: string): boolean {
		return this.editCell.rowId === entry.id && this.editCell.key === `${key}.${areaKey}`;
	}

	// Cancels the current cell edit.
	cancelEdit(): void {
		this.editCell = { rowId: null, key: null, value: '' };
	}

	// Generates and exports the KPI data to an Excel file.
	async exportToExcel(): Promise<void> {
		const workbook = new ExcelJS.Workbook();
		const worksheet = workbook.addWorksheet('BB & ANW KPI');
		const exportEntries: BbAnwEntry[] = this.data;
		const areaKeys: string[] = this.groupedAreaKeys;

		worksheet.addRow(['KPI (MSAN / OLT / IP Core - Network Availability)']);
		worksheet.addRow([`Generated Date: ${new Date().toISOString().split('T')[0]}`]);
		worksheet.addRow([]);

		const headers = [
			'No',
			'Network Engineer KPI',
			'Division',
			'Section',
			'KPI Percent',
			...areaKeys.flatMap((key) => {
				const label = this.areaLabel(key);
				const engineer = this.areaEngineerGroups.find((group) => group.areaKeys.includes(key))?.label;
				const heading = engineer ? `${engineer} - ${label}` : label;
				return [`${heading} Availability (%)`, `${heading} Unavailable Minutes`, `${heading} Total Minutes`, `${heading} Total Nodes`];
			}),
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

		exportEntries.forEach((entry) => {
			const row: any[] = [entry.order, entry.networkEngineerKpi, entry.division, entry.section, entry.kpiPercent];

			areaKeys.forEach((key) => {
				const pct = this.calculatePercentage(entry.totalMinutes?.[key], entry.unavailableMinutes?.[key], entry.totalNodes?.[key], entry.nodeMeta?.[key]);
				row.push(pct === null ? '-' : `${pct.toFixed(2)}%`);
				row.push(entry.unavailableMinutes?.[key] ?? '');
				row.push(entry.totalMinutes?.[key] ?? '');
				row.push(entry.totalNodes?.[key] ?? '');
			});

			const bodyRow = worksheet.addRow(row);
			bodyRow.eachCell((cell: ExcelJS.Cell) => {
				cell.alignment = { vertical: 'middle', horizontal: 'center' };
				cell.border = {
					top: { style: 'thin' },
					left: { style: 'thin' },
					bottom: { style: 'thin' },
					right: { style: 'thin' },
				};
			});
		});

		worksheet.columns.forEach((column) => {
			if (column) column.width = 18;
		});

		const buffer = await workbook.xlsx.writeBuffer();
		const blob = new Blob([buffer], {
			type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
		});

		const link = document.createElement('a');
		link.href = URL.createObjectURL(blob);
		link.download = `BB_ANW_KPI_${new Date().toISOString().split('T')[0]}.xlsx`;
		link.click();
		URL.revokeObjectURL(link.href);
	}
}
