import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RegionService, Region } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';
import { AgedNetworkFailureService } from '../../../../services/aged-network-failure.service';
import { FilterUtils } from '../../../../utils/filter.utils';

interface AreaRow {
  areaCode: string; // lowercase, e.g. 'adipr'
  friendlyName: string; // e.g. 'AD / PR'
  region: string;
  province: string;
  engineer: string;
}

@Component({
  selector: 'app-node-failures',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './node-failures.component.html',
  styleUrls: ['./node-failures.component.scss'],
})
export class NodeFailuresComponent implements OnInit, OnDestroy {
  pageTitle = 'Aged Network Failures';

  loading = false;
  error: string | null = null;

  selectedMonth: number = new Date().getMonth() + 1;
  selectedYear: number = new Date().getFullYear();
  yearOptions: number[] = FilterUtils.generatePlatformYearOptions();

  // Filter state
  formValues = {
    region: '', // R-GM (project uses region names)
  };

  regionOptions: string[] = [];

  // All distinct areas loaded from RegionService
  allAreas: AreaRow[] = [];

  // Rows displayed in the table (merged with DB state)
  tableRows: Array<AreaRow & {
    percentage: number;
    remarks: string;
    saving?: boolean;
    isEditing?: boolean;
  }> = [];

  // Toast notifications state
  toasts: Array<{ id: number; type: 'success' | 'danger'; text: string }> = [];
  private toastId = 1;

  private permissionTimer: ReturnType<typeof setInterval> | null = null;

  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }

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

  constructor(
    private regionService: RegionService,
    private authService: AuthService,
    private agedFailureService: AgedNetworkFailureService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadRegions();

    // Periodically refresh edit permission if needed
    this.permissionTimer = setInterval(() => {
      // no-op for now
    }, 60000);
  }

  ngOnDestroy(): void {
    if (this.permissionTimer) {
      clearInterval(this.permissionTimer);
      this.permissionTimer = null;
    }
  }

  get isEditingAllowed(): boolean {
    return this.authService.canEditPage('AGED NETWORK FAILURES');
  }

  get monthLabel(): string {
    const labels = [
      'January',
      'February',
      'March',
      'April',
      'May',
      'June',
      'July',
      'August',
      'September',
      'October',
      'November',
      'December',
    ];
    return labels[this.selectedMonth - 1] || 'Month';
  }

  onMonthYearChange(): void {
    this.refresh();
  }

  onPeriodChange(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading = true;
    this.error = null;
    this.tableRows = [];
    this.cdr.detectChanges();

    // Fetch all aged failure metrics for selected month/year
    this.agedFailureService
      .get('', this.selectedMonth, this.selectedYear)
      .subscribe({
        next: (rows) => {
          const dbMap = new Map<string, { percentage: number; remarks: string }>();
          (rows || []).forEach((r) => {
            dbMap.set(this.norm(r.areaCode), { percentage: r.percentage, remarks: r.remarks ?? '' });
          });

          let mappedRows = this.allAreas.map((area) => {
            const dbVal = dbMap.get(area.areaCode);
            return {
              ...area,
              percentage: dbVal ? dbVal.percentage : 0,
              remarks: dbVal ? dbVal.remarks : '',
              saving: false,
              isEditing: false,
            };
          });

          // Filter by R-GM if selected
          if (this.formValues.region) {
            mappedRows = mappedRows.filter((r) => r.region === this.formValues.region);
          }

          this.tableRows = mappedRows;
        },
        error: (e) => {
          this.tableRows = [];
          this.error = 'Failed to load Aged Network Failure data.';
          console.error(e);
        },
        complete: () => {
          this.loading = false;
          this.cdr.detectChanges();
        },
      });
  }

  saveAgedFailure(row: any): void {
    if (!this.isEditingAllowed) return;

    row.saving = true;
    this.cdr.detectChanges();

    const dto = {
      areaCode: row.areaCode,
      percentage: Number(row.percentage) || 0,
      remarks: row.remarks || '',
      month: this.selectedMonth,
      year: this.selectedYear,
    };

    this.agedFailureService.upsert(dto).subscribe({
      next: () => {
        row.saving = false;
        row.isEditing = false;
        this.showToast('success', `Saved status for ${row.friendlyName} successfully.`);
        this.refresh();
      },
      error: (err) => {
        row.saving = false;
        this.showToast('danger', `Failed to save status for ${row.friendlyName}.`);
        console.error(err);
        this.cdr.detectChanges();
      },
    });
  }

  showToast(type: 'success' | 'danger', text: string): void {
    const id = this.toastId++;
    this.toasts.push({ id, type, text });
    setTimeout(() => this.dismissToast(id), 2600);
    this.cdr.detectChanges();
  }

  dismissToast(id: number): void {
    this.toasts = this.toasts.filter((t) => t.id !== id);
    this.cdr.detectChanges();
  }

  private loadRegions(): void {
    this.loading = true;
    this.regionService.getAll().subscribe({
      next: (res: Region[] | any[]) => {
        const list = Array.isArray(res) ? res : [];

        // Extract unique region names for filter dropdown
        const mappedRegions = list
          .map((x: any) => x.region ?? x.Region ?? '')
          .filter(Boolean);
        this.regionOptions = Array.from(new Set(mappedRegions));

        // Map regions into distinct areas
        const tempAreas: Record<string, AreaRow> = {};
        list.forEach((item: any) => {
          const rawLea = item.leaCode ?? item.leacode ?? item.lea ?? item.LEA ?? '';
          if (!rawLea) return;
          const areaCode = this.norm(rawLea);
          if (!tempAreas[areaCode]) {
            tempAreas[areaCode] = {
              areaCode: areaCode,
              friendlyName: this.optionMapping[areaCode] || rawLea.toUpperCase(),
              region: item.region ?? item.Region ?? '',
              province: item.province ?? item.Province ?? '',
              engineer: item.networkEngineer ?? item.networkengineer ?? item.NetworkEngineer ?? '',
            };
          }
        });
        this.allAreas = Object.values(tempAreas);

        this.refresh();
      },
      error: (err) => {
        console.error('Failed to load region list:', err);
        this.error = 'Failed to load region list.';
        this.loading = false;
        this.cdr.detectChanges();
      },
    });
  }

  getEngineerNameOnly(engineer: string): string {
    if (!engineer) return '';
    const match = engineer.match(/\(([^)]+)\)/);
    if (match && match[1]) {
      return match[1];
    }
    return engineer.replace(/^NW\//i, '').replace(/^WPS/i, '');
  }

  private norm(s: any): string {
    return s ? String(s).replace(/[^A-Za-z0-9]/g, '').toLowerCase() : '';
  }
}
