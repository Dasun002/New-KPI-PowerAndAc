import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as ExcelJS from 'exceljs';
import { saveAs } from 'file-saver';
import { PowerAndACRecord, PowerAndACService } from '../../../../services/power-and-ac.service';
import { AuthService } from '../../../../services/auth.service';
import { Region, RegionService } from '../../../../services/region.service';
import { FilterUtils } from '../../../../utils/filter.utils';
import {
  formatEngineerDisplay,
  getAreaLookupAliases,
  mapRegionRecords,
  normalizeLookupKey,
  RegionLookupRow
} from '../../../../utils/region-display.utils';

@Component({
  selector: 'app-power-ac-platform',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './power-ac.component.html',
  styleUrls: ['./power-ac.component.scss']
})
export class PowerAcPlatformComponent implements OnInit {
  selectedYear = new Date().getFullYear();
  selectedMonth = new Date().getMonth() + 1;
  readonly defaultYearOptions = FilterUtils.generatePlatformYearOptions();
  readonly monthOptions = Array.from({ length: 12 }, (_, index) => ({
    label: new Date(2000, index, 1).toLocaleString('en', { month: 'long' }),
    value: index + 1
  }));
  records: PowerAndACRecord[] = [];
  regionRows: RegionLookupRow[] = [];
  selectedRegion = '';
  selectedProvince = '';
  selectedEngineer = '';
  loading = false;
  loadError = '';
  actionError = '';
  isEditingAllowed = false;

  get yearOptions(): number[] {
    return Array.from(new Set([
      ...this.defaultYearOptions,
      ...this.records.map(record => record.year),
      this.selectedYear
    ])).sort((a, b) => a - b);
  }

  get regions(): string[] {
    return this.uniqueValues(this.regionRows.map(row => row.region));
  }

  get provinces(): string[] {
    return this.uniqueValues(this.regionRows
      .filter(row => !this.selectedRegion || row.region === this.selectedRegion)
      .map(row => row.province));
  }

  get engineers(): string[] {
    return this.uniqueValues(this.regionRows
      .filter(row => (!this.selectedRegion || row.region === this.selectedRegion)
        && (!this.selectedProvince || row.province === this.selectedProvince))
      .map(row => row.networkEngineer));
  }

  get filteredRecords(): PowerAndACRecord[] {
    return this.records.filter(record => {
      if (record.month !== this.selectedMonth) return false;
      const region = this.getRegionForRecord(record);
      if (this.selectedRegion && region?.region !== this.selectedRegion) return false;
      if (this.selectedProvince && region?.province !== this.selectedProvince) return false;
      if (this.selectedEngineer && region?.networkEngineer !== this.selectedEngineer) return false;
      return true;
    });
  }

  constructor(
    private readonly service: PowerAndACService,
    private readonly regionService: RegionService,
    private readonly authService: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.isEditingAllowed = this.authService.canEditPage('Power & A/C');
    this.loadRegions();
    this.loadRecords();
  }

  private loadRegions(): void {
    this.regionService.getAll().subscribe({
      next: (regions: Region[]) => {
        this.regionRows = mapRegionRecords(regions);
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadError = 'Failed to load region filters.';
        this.cdr.detectChanges();
      }
    });
  }

  loadRecords(): void {
    this.loading = true;
    this.loadError = '';
    this.service.getByYear(this.selectedYear).subscribe({
      next: records => {
        this.records = records;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.loadError = 'Failed to load Power & A/C data.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onRegionChange(): void {
    this.selectedProvince = '';
    this.selectedEngineer = '';
  }

  onProvinceChange(): void {
    this.selectedEngineer = '';
  }

  toggleVerified(record: PowerAndACRecord): void {
    if (!this.isEditingAllowed) return;

    this.actionError = '';
    this.service.toggleVerified(record.id).subscribe({
      next: result => {
        record.isVerified = result.isVerified;
        this.cdr.detectChanges();
      },
      error: () => {
        this.actionError = `Failed to update verification for ${record.designation}, ${record.year}-${record.month}.`;
        this.cdr.detectChanges();
      }
    });
  }

  monthName(month: number): string {
    return new Date(2000, month - 1, 1).toLocaleString('en', { month: 'long' });
  }

  engineerLabel(engineer: string): string {
    const row = this.regionRows.find(item => item.networkEngineer === engineer);
    return formatEngineerDisplay(engineer, row?.engName);
  }

  async exportToExcel(): Promise<void> {
    if (!this.filteredRecords.length) return;

    const workbook = new ExcelJS.Workbook();
    const sheet = workbook.addWorksheet('Power & A-C');
    sheet.columns = [
      { header: 'Month', key: 'month', width: 18 },
      { header: 'Designation', key: 'designation', width: 24 },
      { header: 'Scheduled', key: 'scheduled', width: 16 },
      { header: 'Attended', key: 'attended', width: 16 },
      { header: 'Cumulative Scheduled', key: 'cumulativeSched', width: 24 },
      { header: 'Cumulative Achieved', key: 'cumulativeAchieved', width: 24 },
      { header: 'Verified', key: 'verified', width: 14 }
    ];
    sheet.getRow(1).font = { bold: true, color: { argb: 'FFFFFFFF' } };
    sheet.getRow(1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF0057A6' } };
    this.filteredRecords.forEach(record => sheet.addRow({
      month: `${this.monthName(record.month)} ${record.year}`,
      designation: record.designation,
      scheduled: record.scheduled,
      attended: record.attended,
      cumulativeSched: record.cumulative_Sched,
      cumulativeAchieved: record.cumulative_Achieved,
      verified: record.isVerified ? 'Yes' : 'No'
    }));

    const buffer = await workbook.xlsx.writeBuffer();
    saveAs(
      new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }),
      `Power-AC-${this.selectedYear}-${this.selectedMonth}.xlsx`
    );
  }

  private getRegionForRecord(record: PowerAndACRecord): RegionLookupRow | undefined {
    const aliases = new Set(getAreaLookupAliases(record.designation));
    return this.regionRows.find(row =>
      aliases.has(normalizeLookupKey(row.networkEngineer))
      || aliases.has(normalizeLookupKey(row.lea))
    );
  }

  private uniqueValues(values: Array<string | undefined>): string[] {
    return Array.from(new Set(values.map(value => value?.trim()).filter((value): value is string => Boolean(value))))
      .sort((a, b) => a.localeCompare(b));
  }
}
