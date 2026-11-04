import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { catchError, finalize } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';
import * as ExcelJS from 'exceljs';

/* ===================== TYPES ===================== */

type TowerDetail = {
  Column1: string;
  Column2?: number | string;
  Column3?: number | string;
  Column4?: number | string;
};

type TowerRecord = {
  month: string;
  details: TowerDetail[];
};

type KpiTowerRow = {
  _id?: string;
  no?: number;
  responsibility?: string;
  frequency?: string;
  weightage?: string | number;
  kpi?: string;
};

/* ===================== CONSTANTS ===================== */

const TOWER_COLUMNS = [
  'NW/CPN','NW/CPS','NW/EP','NW/NCP','NW/NP-1','NW/NP-2','NW/NWPE',
  'NW/NWPW','NW/SAB','NW/SPE','NW/SPW','NW/UVA','NW/WPC-1','NW/WPC-2',
  'NW/WPE','NW/WPN','NW/WPNE','NW/WPS','NW/WPSE','NW/WPSW'
];

/* ===================== COMPONENT ===================== */

@Component({
  selector: 'app-tower-mtce-achievement',
  standalone: true,
  imports: [CommonModule, HttpClientModule],
  templateUrl: './tower-mtce-achievement.component.html',
  styleUrls: ['./tower-mtce-achievement.component.scss']
})
export class TowerMtceAchievementComponent implements OnInit {

  private readonly http = inject(HttpClient);
  private readonly cdr = inject(ChangeDetectorRef);

  pageTitle = 'Tower Maintenance';
  heroSubtitle = 'Quarterly tower maintenance achievement across NW regions.';

  headers = [...TOWER_COLUMNS];
  tableData: TowerRecord[] = [];
  kpiTowerData: KpiTowerRow[] = [];

  calculatedValues: string[] = [];
  weightedRows: string[][] = [];

  loading = false;
  errorMessage = '';

  ngOnInit(): void {
    this.fetchData();
  }

  /* ===================== DATA FETCH ===================== */

  fetchData(): void {
    this.loading = true;
    this.errorMessage = '';

    forkJoin({
      distribution: this.http
        .get<TowerRecord[]>('/api/ProcessedDataFetch1')
        .pipe(catchError(() => of([]))),

      // ✅ STRONGLY TYPED + NO FALLBACK
      kpis: this.http
        .get<KpiTowerRow[]>('https://socapps.intranet.slt.com.lk/kpi/api/kpitower')
        .pipe(
          catchError(err => {
            console.error('❌ KPI API FAILED', err);
            return of([]);
          })
        )
    })
    .pipe(finalize(() => {
      this.loading = false;
      this.cdr.detectChanges();
    }))
    .subscribe(({ distribution, kpis }) => {

      /* ---- tower distribution ---- */
      this.tableData = Array.isArray(distribution) ? distribution : [];

      /* ---- KPI DATA (REAL BACKEND ONLY) ---- */
      if (kpis.length > 0) {
        this.kpiTowerData = kpis
          .map(k => ({
            _id: (k as any).id,   // backend uses `id`
            no: k.no,
            responsibility: k.responsibility,
            frequency: k.frequency,
            weightage: k.weightage,
            kpi: k.kpi
          }))
          .sort((a, b) => (a.no ?? 0) - (b.no ?? 0));
      } else {
        this.kpiTowerData = [];
        this.errorMessage = 'No KPI data returned from backend';
      }

      /* ---- calculations ---- */
      this.calculatedValues = this.calculateValues(this.tableData, this.headers);
      this.weightedRows = this.buildWeightedRows(this.kpiTowerData, this.calculatedValues);
    });
  }

  /* ===================== HELPERS ===================== */

  formatText(value?: string | number): string {
    return value === undefined || value === null || value === '' ? '-' : String(value);
  }

  formatWeightedValue(row: number, col: number): string {
    return this.weightedRows[row]?.[col]
      ? `${this.weightedRows[row][col]}%`
      : '-';
  }

  trackByHeader = (_: number, h: string) => h;
  trackByRow = (_: number, r: KpiTowerRow) => r._id ?? r.no ?? _;

  /* ===================== CALCULATIONS ===================== */

  private calculateValues(data: TowerRecord[], headers: string[]): string[] {
    if (!data.length) return headers.map(() => '0.00');

    const month = new Intl.DateTimeFormat('en-US', { month: 'long' }).format(new Date());
    const quarters: any = {
      March: ['January', 'February', 'March'],
      June: ['April', 'May', 'June'],
      September: ['July', 'August', 'September'],
      December: ['October', 'November', 'December']
    };

    if (!quarters[month]) return headers.map(() => '100.00');

    return headers.map(h => {
      let a = 0, d = 0;
      data.forEach(m => {
        if (!quarters[month].includes(m.month)) return;
        const c = m.details.find(x => x.Column1 === h);
        if (c) {
          a += this.toNumber(c.Column3);
          d += this.toNumber(c.Column2);
        }
      });
      return d ? ((a / d) * 100).toFixed(2) : '0.00';
    });
  }

  private buildWeightedRows(rows: KpiTowerRow[], values: string[]): string[][] {
    return rows.map(r => {
      const w = this.toNumber(r.weightage);
      return values.map(v => ((this.toNumber(v) * w) / 100).toFixed(2));
    });
  }

  private toNumber(v: any): number {
    const n = parseFloat(String(v).replace(/[^\d.-]/g, ''));
    return Number.isFinite(n) ? n : 0;
  }

  /* ===================== EXPORT ===================== */

  exportToExcel(): void {
    if (!this.kpiTowerData.length) return;

    const wb = new ExcelJS.Workbook();
    const ws = wb.addWorksheet('KPI Tower Maintenance');

    ws.addRow([
      'No','Responsibility','Frequency','Weightage','KPI',
      ...this.headers
    ]);

    this.kpiTowerData.forEach((r, i) => {
      ws.addRow([
        r.no,
        r.responsibility,
        r.frequency,
        r.weightage,
        r.kpi,
        ...this.weightedRows[i].map(v => `${v}%`)
      ]);
    });

    wb.xlsx.writeBuffer().then(b => {
      const blob = new Blob([b]);
      const a = document.createElement('a');
      a.href = URL.createObjectURL(blob);
      a.download = 'KPI_Tower_Maintenance.xlsx';
      a.click();
    });
  }
}
