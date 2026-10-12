import { Component, OnInit, ChangeDetectorRef, ViewChildren, QueryList, ElementRef, OnDestroy, AfterViewInit, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { RegionService } from '../../../services/region.service';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import * as ExcelJS from 'exceljs';
import { environment } from '../../../../environments/environment';
import { Subscription, forkJoin } from 'rxjs';

export interface Region {
  id: number;
  region: string;
  province: string;
  networkEngineer: string;
  lea: string;
}

export interface KpiMetric {
  achieved: number | string;
  maximumPoints: number | string;
  pointsAchieved: number | string;
}

export interface KpiRow {
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

export type KpiDefinition = {
  id: number;
  perspectives: string;
  category?: string;
  strategicObjectives: string;
  keyPerformanceIndicators: string;
  unit: string;
  descriptionOfKPI: string;
  weightage: number;
  pointsApplicable: number;
};

@Component({
  selector: 'app-q1',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule],
  templateUrl: './2026-q1.component.html',
  styleUrls: ['./2026-q1.component.scss']
})
export class Q1Component implements OnInit, AfterViewInit, OnDestroy {
  currentMonth: string;
  currentYear: number;
  
  selectedMonth: number;
  selectedYear: number;
  monthOptions: { value: number; label: string }[] = [];
  yearOptions: number[] = [];
  
  activeView: 'monthly' | 'summary' = 'monthly';

  engineersCount = 0;
  loading = false;
  noOverallResults = false;

  isExcelView = false;
  excelHtmlContent: SafeHtml | string = '';

  regionGroups: { region: string; provinces: { province: string; engineers: Region[] }[]; totalEngineers: number }[] = [];
  engineersFlat: Region[] = [];
  kpiRows: KpiRow[] = [];
  hoveredRowIndex: number | null = null;
  totalPointsApplicable = 0;
  totalPointsAchievedByRegion: number[] = [];
  totalPointsNormalized: number[] = [];
  noDefinitions = false;

  @ViewChildren('leftRowRef', { read: ElementRef })
  private leftRowElements!: QueryList<ElementRef<HTMLTableRowElement>>;

  @ViewChildren('rightRowRef', { read: ElementRef })
  private rightRowElements!: QueryList<ElementRef<HTMLTableRowElement>>;

  private readonly rowChangesSub = new Subscription();
  private pendingFrame: number | null = null;

  private readonly apiBase = `${environment.apiUrl}/kpi-definitions`;

  // Injects the required dependencies.
  constructor(
    private regionService: RegionService,
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
    private sanitizer: DomSanitizer
  ) {
    const now = new Date();
    this.currentYear = 2026; 
    this.currentMonth = now.toLocaleString('en-US', { month: 'long' });
    
    const currentMonthIndex = now.getMonth() + 1;
    this.selectedMonth = currentMonthIndex <= 3 ? currentMonthIndex : 1;
    this.selectedYear = 2026;

    this.monthOptions = [
      { value: 1, label: 'January' },
      { value: 2, label: 'February' },
      { value: 3, label: 'March' }
    ];

    this.yearOptions = [2026];

    this.syncDisplayedPeriod();
  }

  ngAfterViewInit(): void {
    this.scheduleRowSync();
    this.rowChangesSub.add(this.leftRowElements?.changes.subscribe(() => this.scheduleRowSync()));
    this.rowChangesSub.add(this.rightRowElements?.changes.subscribe(() => this.scheduleRowSync()));
  }

  ngOnDestroy(): void {
    this.rowChangesSub.unsubscribe();
    if (this.pendingFrame !== null) cancelAnimationFrame(this.pendingFrame);
  }

  @HostListener('window:resize')
  onWindowResize(): void {
    this.scheduleRowSync();
  }

  ngOnInit(): void {
    this.loadRegions();
  }

  setActiveView(view: 'monthly' | 'summary'): void {
    this.activeView = view;
  }

  calculateSummary(): void {
    this.loadSummaryAverages();
  }

  async exportSummaryToExcel(): Promise<void> {
    const ExcelJSLib = ExcelJS;
    const workbook = new ExcelJSLib.Workbook();
    const worksheet = workbook.addWorksheet('Q1 Summary');

    const headerBgColor = '0057A6';
    const headerTextColor = 'FFFFFF';
    const borderColor = 'D1D5DB';
    const totalRowBgColor = '02B28C';

    // Left header columns
    const leftHeaders = ['Perspectives', 'Strategic Objectives (KRA)', 'Category', 'KPI'];
    leftHeaders.forEach((h, i) => {
      const cell = worksheet.getCell(1, i + 1);
      cell.value = h;
      cell.font = { bold: true, color: { argb: headerTextColor } };
      cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
      cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
    });
    worksheet.getColumn(1).width = 16;
    worksheet.getColumn(2).width = 28;
    worksheet.getColumn(3).width = 20;
    worksheet.getColumn(4).width = 32;

    // Right headers: one group of 3 per engineer
    let col = 5;
    for (const eng of this.engineersFlat) {
      worksheet.getCell(1, col).value = `${eng.networkEngineer} (${eng.lea})`;
      worksheet.getCell(1, col).font = { bold: true, color: { argb: headerTextColor } };
      worksheet.getCell(1, col).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
      worksheet.getCell(1, col).alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
      worksheet.mergeCells(1, col, 1, col + 2);
      ['Achieved KPI', 'Points Applicable', 'Points Achieved'].forEach((sub, si) => {
        const c = worksheet.getCell(2, col + si);
        c.value = sub;
        c.font = { bold: true, color: { argb: headerTextColor } };
        c.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: headerBgColor } };
        c.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
        worksheet.getColumn(col + si).width = 14;
      });
      col += 3;
    }

    // Data rows
    this.kpiRows.forEach((row, rowIdx) => {
      const r = rowIdx + 3;
      worksheet.getCell(r, 1).value = row.perspectives;
      worksheet.getCell(r, 2).value = row.strategicObjectives;
      worksheet.getCell(r, 3).value = row.category;
      worksheet.getCell(r, 4).value = row.kpi;
      worksheet.getCell(r, 4).font = { bold: true };
      let c = 5;
      row.metrics.forEach(metric => {
        worksheet.getCell(r, c).value = metric.achieved === '-' ? '-' : Number(metric.achieved);
        worksheet.getCell(r, c + 1).value = metric.maximumPoints === '-' ? '-' : Number(metric.maximumPoints);
        worksheet.getCell(r, c + 2).value = metric.pointsAchieved === '-' ? '-' : Number(metric.pointsAchieved);
        [c, c + 1, c + 2].forEach(ci => {
          worksheet.getCell(r, ci).alignment = { horizontal: 'center', vertical: 'middle' };
          worksheet.getCell(r, ci).border = {
            top: { style: 'thin', color: { argb: borderColor } },
            bottom: { style: 'thin', color: { argb: borderColor } },
            left: { style: 'thin', color: { argb: borderColor } },
            right: { style: 'thin', color: { argb: borderColor } }
          };
        });
        c += 3;
      });
    });

    // Total row
    const totalRow = this.kpiRows.length + 3;
    worksheet.getCell(totalRow, 1).value = 'Q1 Average';
    worksheet.getCell(totalRow, 1).font = { bold: true, color: { argb: headerTextColor } };
    worksheet.mergeCells(totalRow, 1, totalRow, 4);
    worksheet.getCell(totalRow, 1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: totalRowBgColor } };
    worksheet.getCell(totalRow, 1).alignment = { horizontal: 'right', vertical: 'middle' };

    const buffer = await workbook.xlsx.writeBuffer();
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'Q1_Summary_2026.xlsx';
    a.click();
    URL.revokeObjectURL(a.href);
  }

  onMonthChange(month: number): void {
    this.selectedMonth = Number(month);
    this.syncDisplayedPeriod();
    this.loadLeftTableFromApi();
  }

  onYearChange(year: number): void {
    this.selectedYear = Number(year);
    this.syncDisplayedPeriod();
    this.loadLeftTableFromApi();
  }

  private getMonthLabel(month: number): string {
    return this.monthOptions.find((m) => m.value === month)?.label ?? '';
  }

  private syncDisplayedPeriod(): void {
    this.currentMonth = this.getMonthLabel(this.selectedMonth);
    this.currentYear = this.selectedYear;
  }

  private loadRegions(): void {
    this.loading = true;
    this.regionService.getAll().subscribe({
      next: (res) => {
        const regions = res || [];
        const regionMap = new Map<string, Map<string, any[]>>();

        regions.forEach((item: any) => {
          const provinceMap = regionMap.get(item.region) ?? new Map<string, any[]>();
          const engineers = provinceMap.get(item.province) ?? [];
          engineers.push(item);
          provinceMap.set(item.province, engineers);
          regionMap.set(item.region, provinceMap);
        });

        this.regionGroups = Array.from(regionMap.entries()).map(([region, provinceMap]) => {
          const provinces = Array.from(provinceMap.entries()).map(([province, engineers]) => ({
            province,
            engineers: engineers.map((r: any) => ({
                id: r.id,
                region: r.region,
                province: r.province,
                networkEngineer: r.networkEngineer || r.networkengineer || r.network_engineer || '—',
                lea: r.lea || r.leaCode || r.leacode || r.lea_code || '—'
            }))
          }));
          const totalEngineers = provinces.reduce((sum, p) => sum + p.engineers.length, 0);
          return { region, provinces, totalEngineers };
        });

        this.engineersFlat = this.regionGroups.flatMap((g) =>
          g.provinces.flatMap((p) => p.engineers)
        );

        this.engineersCount = this.engineersFlat.length;
        
        this.totalPointsAchievedByRegion = new Array(this.engineersCount).fill(0);
        this.totalPointsNormalized = new Array(this.engineersCount).fill(0);

        this.loadLeftTableFromApi();
      },
      error: (err) => {
        console.error('Failed loading regions:', err);
        this.engineersCount = 0;
        this.loading = false;
      },
    });
  }

  private loadLeftTableFromApi(): void {
    this.loading = true;
    const month = this.selectedMonth;
    const year = this.selectedYear;
    const url = `${this.apiBase}?month=${month}&year=${year}`;

    this.http.get<KpiDefinition[]>(url).subscribe({
      next: (res) => {
        const definitions = res || [];
        if (definitions.length === 0) {
          this.noDefinitions = true;
        } else {
          this.noDefinitions = false;
          const list = definitions.sort((a, b) => a.id - b.id);
          this.kpiRows = list.map((row, rowIndex) => ({
             id: row.id,
             number: rowIndex + 1,
             perspectives: row.perspectives,
             category: row.category ?? '',
             strategicObjectives: (row.strategicObjectives ?? '').replace(/service assurance/gi, 'SA'),
             kpi: row.keyPerformanceIndicators,
             target: row.descriptionOfKPI,
             weightage: row.weightage,
             pointsApplicable: row.pointsApplicable ?? 0,
             metrics: this.engineersFlat.map(() => ({ achieved: '-', maximumPoints: '-', pointsAchieved: '-' }))
          }));
          this.totalPointsApplicable = this.kpiRows.reduce((sum, row) => sum + (row.pointsApplicable ?? 0), 0);
        }

        // Definitions loaded as required
        this.loadOverallResultsFromExcel(month, year);
      },
      error: (err) => {
        console.error('Failed loading KPI definitions:', err);
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  private loadOverallResultsFromExcel(month: number, year: number): void {
    this.noOverallResults = false;
    this.isExcelView = true;
    const monthStr = month < 10 ? `0${month}` : `${month}`;
    const url = `assets/kpi-sheets/overall_kpi_2026_${monthStr}.xlsx`;

    this.http.get(url, { responseType: 'arraybuffer' }).subscribe({
      next: async (data: ArrayBuffer) => {
        try {
          const workbook = new ExcelJS.Workbook();
          await workbook.xlsx.load(data);
          const worksheet = workbook.getWorksheet('Current Month KPI') || workbook.worksheets[0];

          if (!worksheet) {
            console.error('Worksheet not found in Excel file');
            this.noOverallResults = true;
            this.loading = false;
            this.cdr.detectChanges();
            return;
          }
          const merges: string[] = ((worksheet.model as any).merges) || [];
          //const merges = worksheet.model.merges || [];
          const mergeMap = new Map<string, { rowspan: number; colspan: number }>();
          const hiddenCells = new Set<string>();

          const parseColRef = (colStr: string): number => {
            let col = 0;
            for (let i = 0; i < colStr.length; i++) {
              col = col * 26 + (colStr.charCodeAt(i) - 64);
            }
            return col;
          };

          const parseCellRef = (ref: string): { row: number; col: number } => {
            const match = /^([A-Z]+)([0-9]+)$/.exec(ref);
            if (!match) return { row: 0, col: 0 };
            return {
              row: parseInt(match[2], 10),
              col: parseColRef(match[1])
            };
          };

         /* merges.forEach(rangeStr => {
            const parts = rangeStr.split(':');
            if (parts.length === 2) {
              const start = parseCellRef(parts[0]);
              const end = parseCellRef(parts[1]);

              const rowspan = end.row - start.row + 1;
              const colspan = end.col - start.col + 1;

              mergeMap.set(`${start.row}:${start.col}`, { rowspan, colspan });

              for (let r = start.row; r <= end.row; r++) {
                for (let c = start.col; c <= end.col; c++) {
                  if (r !== start.row || c !== start.col) {
                    hiddenCells.add(`${r}:${c}`);
                  }
                }
              }
            }
          });*/
          merges.forEach(rangeStr => {
            const parts = rangeStr.split(':');
            if (parts.length === 2) {
              const start = parseCellRef(parts[0]);
              const end = parseCellRef(parts[1]);

              const rowspan = end.row - start.row + 1;
              const colspan = end.col - start.col + 1;

              mergeMap.set(`${start.row}:${start.col}`, { rowspan, colspan });

              for (let r = start.row; r <= end.row; r++) {
                for (let c = start.col; c <= end.col; c++) {
                  if (r !== start.row || c !== start.col) {
                    hiddenCells.add(`${r}:${c}`);
                  }
                }
              }
            }
          });

          const applyTint = (hex: string, tint: number | undefined | null): string => {
            if (tint === undefined || tint === null || tint === 0) return hex;

            let r = parseInt(hex.substring(1, 3), 16);
            let g = parseInt(hex.substring(3, 5), 16);
            let b = parseInt(hex.substring(5, 7), 16);

            if (tint < 0) {
              r = Math.round(r * (1 + tint));
              g = Math.round(g * (1 + tint));
              b = Math.round(b * (1 + tint));
            } else {
              r = Math.round(r * (1 - tint) + 255 * tint);
              g = Math.round(g * (1 - tint) + 255 * tint);
              b = Math.round(b * (1 - tint) + 255 * tint);
            }

            const rs = Math.min(255, Math.max(0, r)).toString(16).padStart(2, '0');
            const gs = Math.min(255, Math.max(0, g)).toString(16).padStart(2, '0');
            const bs = Math.min(255, Math.max(0, b)).toString(16).padStart(2, '0');

            return `#${rs}${gs}${bs}`;
          };

          const parseExcelColor = (color: any): string | null => {
            if (!color) return null;
            let hex: string | null = null;

            if (color.argb) {
              const argb = String(color.argb);
              hex = argb.length === 8 ? `#${argb.substring(2)}` : `#${argb}`;
            } else if (color.theme !== undefined) {
              const themeColors = [
                '#FFFFFF', '#000000', '#E7E6E6', '#44546A', '#5B9BD5', 
                '#ED7D31', '#A5A5A5', '#FFC000', '#4472C4', '#70AD47'
              ];
              hex = themeColors[color.theme] || null;
            } else if (color.indexed !== undefined) {
              const indexedColors: { [key: number]: string } = {
                8: '#000000', 9: '#FFFFFF', 10: '#FF0000', 11: '#00FF00', 12: '#0000FF',
                13: '#FFFF00', 14: '#FF00FF', 15: '#00FFFF', 16: '#800000', 17: '#008000',
                18: '#000080', 19: '#808000', 20: '#800080', 21: '#008080', 22: '#C0C0C0',
                23: '#808080', 64: '#FFFFFF'
              };
              hex = indexedColors[color.indexed] || null;
            }

            if (hex) {
              return applyTint(hex, color.tint);
            }
            return null;
          };

          let html = '<table style="border-collapse: collapse; width: max-content; min-width: 100%; font-family: Segoe UI, sans-serif; border: 1px solid #cbd5e1; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05);">';

          worksheet.eachRow({ includeEmpty: true }, (row, rowIdx) => {
            html += '<tr>';

            for (let colIdx = 1; colIdx <= worksheet.columnCount; colIdx++) {
              const cellKey = `${rowIdx}:${colIdx}`;
              if (hiddenCells.has(cellKey)) {
                continue;
              }

              const cell = row.getCell(colIdx);
              const mergeInfo = mergeMap.get(cellKey);
              const rowspanAttr = mergeInfo ? ` rowspan="${mergeInfo.rowspan}"` : '';
              const colspanAttr = mergeInfo ? ` colspan="${mergeInfo.colspan}"` : '';

              const colWidth = worksheet.getColumn(colIdx).width || 12;
              const widthPx = Math.round(colWidth * 9);
              let styles = `border: 1px solid #cbd5e1; padding: 10px 14px; font-size: 13px; line-height: 1.3; vertical-align: middle; white-space: normal; word-break: break-word; min-width: ${widthPx}px; width: ${widthPx}px;`;

              if (cell.fill && cell.fill.type === 'pattern' && cell.fill.fgColor) {
                const color = parseExcelColor(cell.fill.fgColor);
                if (color) {
                  styles += ` background-color: ${color} !important;`;
                }
              }

              if (cell.font) {
                if (cell.font.bold) styles += ' font-weight: bold;';
                if (cell.font.italic) styles += ' font-style: italic;';
                if (cell.font.size) styles += ` font-size: ${cell.font.size}pt;`;
                if (cell.font.name) styles += ` font-family: ${cell.font.name}, sans-serif;`;
                const fontColor = parseExcelColor(cell.font.color);
                if (fontColor) {
                  styles += ` color: ${fontColor};`;
                }
              }

              if (cell.alignment) {
                if (cell.alignment.horizontal) {
                  styles += ` text-align: ${cell.alignment.horizontal};`;
                }
                if (cell.alignment.vertical) {
                  styles += ` vertical-align: ${cell.alignment.vertical};`;
                }
              }

              let cellValue = '';
              if (cell.value !== null && cell.value !== undefined) {
                if (typeof cell.value === 'object') {
                  if ((cell.value as any).result !== undefined) {
                    cellValue = String((cell.value as any).result);
                  } else if ((cell.value as any).richText) {
                    cellValue = (cell.value as any).richText.map((rt: any) => rt.text).join('');
                  } else {
                    cellValue = String(cell.value);
                  }
                } else {
                  cellValue = String(cell.value);
                }
              }

              if (typeof cell.value === 'number') {
                if (cell.numFmt && cell.numFmt.includes('%')) {
                  const val = Number(cell.value);
                  if (val > 1) {
                    cellValue = `${val.toFixed(2)}%`;
                  } else {
                    cellValue = `${(val * 100).toFixed(2)}%`;
                  }
                } else if (cell.numFmt && cell.numFmt.includes('0.0000')) {
                  cellValue = Number(cell.value).toFixed(4);
                } else if (cell.numFmt && cell.numFmt.includes('0.00')) {
                  cellValue = Number(cell.value).toFixed(2);
                }
              }

              cellValue = cellValue.replace(/\n/g, '<br>');

              html += `<td style="${styles}"${rowspanAttr}${colspanAttr}>${cellValue}</td>`;
            }

            html += '</tr>';
          });

          html += '</table>';
          this.excelHtmlContent = this.sanitizer.bypassSecurityTrustHtml(html);

          this.loading = false;
          this.cdr.detectChanges();
        } catch (e) {
          console.error('Error rendering excel', e);
          this.noOverallResults = true;
          this.loading = false;
          this.cdr.detectChanges();
        }
      },
      error: (err) => {
        console.error('Failed loading excel file:', err);
        this.noOverallResults = true;
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  exportToExcel(): void {
    const monthStr = this.selectedMonth < 10 ? `0${this.selectedMonth}` : `${this.selectedMonth}`;
    const url = `assets/kpi-sheets/overall_kpi_2026_${monthStr}.xlsx`;
    const fileName = `overall_kpi_2026_${monthStr}.xlsx`;

    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
  }

  // --- Summary View Helpers ---

  setHoveredRowIndex(index: number | null): void {
    this.hoveredRowIndex = index;
    this.cdr.detectChanges();
  }

  getComputedWeightage(row: KpiRow): string {
    if (this.totalPointsApplicable <= 0) return '0.00%';
    const weightage = (Number(row.pointsApplicable ?? 0) / this.totalPointsApplicable) * 100;
    return `${weightage.toFixed(2)}%`;
  }

  getKpiRowClass(row: KpiRow): string {
    const cat = (row.category ?? '').toLowerCase();
    if (cat.includes('enterprise')) return 'category-enterprise';
    if (cat.includes('operator')) return 'category-other-operator';
    if (cat.includes('assurance')) return 'category-assurance';
    if (cat.includes('fulfillment')) return 'category-fulfillment';
    return '';
  }

  getAchievedCellClass(metric: KpiMetric): string {
    if (metric.achieved === '-' || metric.maximumPoints === '-') return '';
    return Number(metric.pointsAchieved) >= Number(metric.maximumPoints) ? 'target-achieved' : 'target-failed';
  }

  formatHeaderLabel(value: string | null | undefined): string {
    if (!value) return '';
    const withSpaces = value.replace(/([a-zA-Z])([0-9])/g, '$1 $2');
    return withSpaces.split(/\s+/).map((part) => {
      const isAllCaps = part === part.toUpperCase();
      if (isAllCaps && part.length <= 4) return part;
      return part.charAt(0).toUpperCase() + part.slice(1).toLowerCase();
    }).join(' ');
  }

  private normalizeKpiKey(name: string): string {
    return name
      .toLowerCase()
      .trim()
      .replace(/\s+/g, ' ')          // collapse multiple spaces
      .replace(/\s*\(\s*/g, '(')     // remove spaces around opening paren
      .replace(/\s*\)\s*/g, ')')     // remove spaces around closing paren
      .replace(/\s*<\s*/g, '<')      // remove spaces around <
      .replace(/\s*>\s*/g, '>');     // remove spaces around >
  }

  private readonly KPI_NAME_ALIASES: Record<string, string> = {
    'routine maintenance - slbn/sdh': 'routine maintenance - slbn',
    'routine maintenance - msan/olte': 'routine maintenance - msan/olt',
    'fiber failure restoration(large scale<pole damages etc>):<8 hrs': 'fiber failures restoration(large scale<pole damages etc>):<8 hrs'
  };

  loadSummaryAverages(): void {
    this.loading = true;
    const urls = [
      'assets/kpi-sheets/overall_kpi_2026_01.xlsx',
      'assets/kpi-sheets/overall_kpi_2026_02.xlsx',
      'assets/kpi-sheets/overall_kpi_2026_03.xlsx'
    ];

    forkJoin(urls.map(url => this.http.get(url, { responseType: 'arraybuffer' }))).subscribe({
      next: async (buffers: ArrayBuffer[]) => {
        try {
          // Parse each workbook into a map: kpiName -> leaCode -> { achieved, maximumPoints, pointsAchieved }
          type EngMetric = { achieved: number | null; maximumPoints: number | null; pointsAchieved: number | null };
          type SheetData = Map<string, Map<string, EngMetric>>;

          const getCellNum = (cell: ExcelJS.Cell): number | null => {
            const v = cell.value;
            if (v === null || v === undefined || v === '') return null;
            if (typeof v === 'number') return v;
            if (typeof v === 'object' && (v as any).result !== undefined) {
              const r = (v as any).result;
              if (typeof r === 'number') return r;
              if (typeof r === 'string') {
                const n = parseFloat(r.replace(/[%\s]/g, ''));
                return isNaN(n) ? null : n;
              }
              return null;
            }
            if (typeof v === 'string') {
              const n = parseFloat(v.replace(/[%\s]/g, ''));
              return isNaN(n) ? null : n;
            }
            const n = parseFloat(String(v));
            return isNaN(n) ? null : n;
          };

          const parseSheet = async (buffer: ArrayBuffer): Promise<SheetData> => {
            const wb = new ExcelJS.Workbook();
            await wb.xlsx.load(buffer);

            const getCellText = (cell: ExcelJS.Cell): string => {
              const v = cell.value;
              if (v === null || v === undefined || v === '') return '';
              if (typeof v === 'object' && (v as any).richText) {
                return (v as any).richText.map((x: any) => x.text).join('');
              }
              if (typeof v === 'object' && (v as any).result !== undefined) {
                return String((v as any).result);
              }
              return String(v);
            };

            const ws = wb.getWorksheet('KPI Calculation') ?? wb.worksheets.find((candidate) => {
              for (let rowIndex = 1; rowIndex <= Math.min(candidate.rowCount, 15); rowIndex++) {
                const row = candidate.getRow(rowIndex);
                for (let colIndex = 1; colIndex <= Math.min(candidate.columnCount, 20); colIndex++) {
                  const text = getCellText(row.getCell(colIndex)).trim().toLowerCase();
                  if (text.includes('key performance indicators (kpi)')) {
                    return true;
                  }
                }
              }
              return false;
            }) ?? wb.worksheets[0];

            const data: SheetData = new Map();

            let headerRowIndex = 0;
            let kpiColumnIndex = -1;
            for (let rowIndex = 1; rowIndex <= Math.min(ws.rowCount, 20); rowIndex++) {
              const row = ws.getRow(rowIndex);
              for (let colIndex = 1; colIndex <= Math.min(ws.columnCount, 20); colIndex++) {
                const text = getCellText(row.getCell(colIndex)).trim().toLowerCase();
                if (text === 'key performance indicators (kpi)') {
                  headerRowIndex = rowIndex;
                  kpiColumnIndex = colIndex;
                  break;
                }
              }
              if (headerRowIndex > 0) break;
            }

            if (headerRowIndex <= 0 || kpiColumnIndex <= 0) {
              return data;
            }

            const engineerRow = ws.getRow(headerRowIndex - 1);
            const leaColMap: { lea: string; col: number }[] = [];
            for (let colIndex = 1; colIndex <= ws.columnCount; colIndex++) {
              const lea = getCellText(engineerRow.getCell(colIndex)).trim();
              if (!lea) continue;
              const nextCol = colIndex + 1;
              const nextNextCol = colIndex + 2;
              if (getCellText(engineerRow.getCell(nextCol)).trim() === lea && getCellText(engineerRow.getCell(nextNextCol)).trim() === lea) {
                leaColMap.push({ lea, col: colIndex });
                colIndex += 2;
              }
            }

            for (let rowIndex = headerRowIndex + 1; rowIndex <= ws.rowCount; rowIndex++) {
              const row = ws.getRow(rowIndex);
              const rawKpi = getCellText(row.getCell(kpiColumnIndex)).trim();
              if (!rawKpi) continue;

              const hasMetricValues = leaColMap.some(({ col }) => {
                return [col, col + 1, col + 2].some((metricCol) => getCellNum(row.getCell(metricCol)) !== null);
              });
              if (!hasMetricValues) continue;

              const kpiKey = this.normalizeKpiKey(rawKpi);

              const engMap = new Map<string, EngMetric>();
              for (const { lea, col } of leaColMap) {
                engMap.set(lea.toLowerCase(), {
                  achieved:       getCellNum(row.getCell(col)),
                  maximumPoints:  getCellNum(row.getCell(col + 1)),
                  pointsAchieved: getCellNum(row.getCell(col + 2))
                });
              }

              data.set(kpiKey, engMap);
            }

            return data;
          };

          const [jan, feb, mar] = await Promise.all(buffers.map(buf => parseSheet(buf)));

          const avg = (a: number | null, b: number | null, c: number | null): number | string => {
            const vals = [a, b, c].filter((v): v is number => v !== null);
            if (vals.length === 0) return '-';
            return parseFloat((vals.reduce((s, v) => s + v, 0) / 3).toFixed(2));
          };

          for (const kpiRow of this.kpiRows) {
            const normalized = this.normalizeKpiKey(kpiRow.kpi);
            const kpiKey = this.KPI_NAME_ALIASES[normalized] ?? normalized;
            const janKpi = jan.get(kpiKey);
            const febKpi = feb.get(kpiKey);
            const marKpi = mar.get(kpiKey);

            this.engineersFlat.forEach((eng, idx) => {
              const leaKey = eng.lea.trim().toLowerCase();
              const j = janKpi?.get(leaKey);
              const f = febKpi?.get(leaKey);
              const m = marKpi?.get(leaKey);
              kpiRow.metrics[idx] = {
                achieved:       avg(j?.achieved       ?? null, f?.achieved       ?? null, m?.achieved       ?? null),
                maximumPoints:  avg(j?.maximumPoints  ?? null, f?.maximumPoints  ?? null, m?.maximumPoints  ?? null),
                pointsAchieved: avg(j?.pointsAchieved ?? null, f?.pointsAchieved ?? null, m?.pointsAchieved ?? null)
              };
            });
          }

          this.loading = false;
          this.cdr.detectChanges();
          this.scheduleRowSync();
        } catch (e) {
          console.error('Error loading summary averages:', e);
          this.loading = false;
          this.cdr.detectChanges();
        }
      },
      error: (err) => {
        console.error('Failed loading Excel files for summary:', err);
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private scheduleRowSync(): void {
    if (!this.leftRowElements || !this.rightRowElements) return;
    if (this.pendingFrame !== null) cancelAnimationFrame(this.pendingFrame);
    this.pendingFrame = requestAnimationFrame(() => {
      this.pendingFrame = null;
      this.syncRowHeights();
    });
  }

  private syncRowHeights(): void {
    if (!this.leftRowElements || !this.rightRowElements) return;
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
}
