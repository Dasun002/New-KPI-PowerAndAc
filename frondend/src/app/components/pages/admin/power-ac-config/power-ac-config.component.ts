/* File: power-ac-config.component.ts
   Admin screen: add, edit and delete the Power & A/C KPIs (KpiDefinition rows with Category PowerAndAC). */
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { PowerAcKpiInput, PowerAcKpiRow, PowerAcKpiService } from '../../../../services/power-ac-kpi.service';

// The four KPIs the calculation supports, by name, with their component code and unit.
const KNOWN_KPIS: { name: string; code: string; unit: string }[] = [
  { name: 'Preventive Maintenance', code: 'PM', unit: '%' },
  { name: 'Critical Safety', code: 'SAFETY', unit: 'Incidents' },
  { name: 'Corrective Action and Maintenance Record', code: 'CORRECTIVE', unit: '%' },
  { name: 'Power System Availability', code: 'AVAILABILITY', unit: '%' }
];

@Component({
  selector: 'app-power-ac-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './power-ac-config.component.html',
  styleUrls: ['./power-ac-config.component.scss']
})
export class PowerAcConfigComponent implements OnInit {
  readonly category = 'PowerAndAC';

  kpis: PowerAcKpiRow[] = [];
  editingId: number | null = null;
  form = { name: '', target: '0', weightage: 0, included: true };
  message = '';
  error = '';
  busy = false;

  constructor(private readonly api: PowerAcKpiService, private readonly cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.load();
  }

  get isEditing(): boolean {
    return this.editingId !== null;
  }

  // Known KPI names keep their component code. Any other name gets a code made from its letters and digits.
  codeFor(name: string): string {
    const known = KNOWN_KPIS.find(k => k.name.toLowerCase() === name.trim().toLowerCase());
    if (known) return known.code;
    return name.trim().toUpperCase().replace(/[^A-Z0-9]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 30);
  }

  // Code shown under the KPI name in the list.
  displayCode(row: PowerAcKpiRow): string {
    return row.code;
  }

  targetText(row: PowerAcKpiRow): string {
    return row.unit === 'Incidents' ? `${row.target} Incidents` : `${row.target} %`;
  }

  load(): void {
    this.api.listKpis().subscribe({
      next: rows => { this.kpis = rows; this.cdr.detectChanges(); },
      error: err => this.fail(err, 'load the Power & A/C KPIs')
    });
  }

  submit(): void {
    const name = this.form.name.trim();
    if (!name) {
      this.error = 'KPI name is required.';
      return;
    }
    const known = KNOWN_KPIS.find(k => k.name.toLowerCase() === name.toLowerCase());
    // An existing KPI keeps its code when it is edited, so its data column stays linked.
    const existing = this.editingId === null ? null : this.kpis.find(k => k.id === this.editingId) ?? null;
    const input: PowerAcKpiInput = {
      name: known?.name ?? name,
      code: existing?.code ?? this.codeFor(name),
      target: String(this.form.target ?? '').trim(),
      unit: existing?.unit ?? known?.unit ?? '%',
      weightage: Number(this.form.weightage),
      included: this.form.included
    };
    this.busy = true;
    this.error = '';
    const request = this.editingId === null
      ? this.api.addKpi(input)
      : this.api.updateKpi(this.editingId, input);
    request.subscribe({
      next: () => {
        this.busy = false;
        this.message = this.isEditing ? 'KPI updated.' : 'KPI added.';
        this.cancel();
        this.load();
      },
      error: err => { this.busy = false; this.fail(err, this.isEditing ? 'update the KPI' : 'add the KPI'); }
    });
  }

  edit(row: PowerAcKpiRow): void {
    this.editingId = row.id;
    this.form = { name: row.name, target: row.target, weightage: row.weightage, included: row.included };
    this.message = '';
    this.error = '';
  }

  cancel(): void {
    this.editingId = null;
    this.form = { name: '', target: '0', weightage: 0, included: true };
    this.error = '';
  }

  // Adds or removes a KPI from the scored set without changing anything else.
  toggleIncluded(row: PowerAcKpiRow): void {
    const input: PowerAcKpiInput = {
      name: row.name,
      code: row.code,
      target: row.target,
      unit: row.unit,
      weightage: row.weightage,
      included: !row.included
    };
    this.api.updateKpi(row.id, input).subscribe({
      next: () => { this.message = input.included ? 'KPI included in scoring.' : 'KPI excluded from scoring.'; this.load(); },
      error: err => this.fail(err, 'change the scoring setting')
    });
  }

  remove(row: PowerAcKpiRow): void {
    if (!confirm(`Delete the KPI "${row.name}"?`)) return;
    this.api.deleteKpi(row.id).subscribe({
      next: () => { this.message = 'KPI deleted.'; this.load(); },
      error: err => this.fail(err, 'delete the KPI')
    });
  }

  private fail(err: HttpErrorResponse, action: string): void {
    if (err.status === 401) this.error = 'Your session has expired. Sign in again.';
    else if (err.status === 403) this.error = `You are not authorized to ${action}.`;
    else this.error = (err.error as { message?: string } | null)?.message ?? `Could not ${action} (HTTP ${err.status}).`;
    this.cdr.detectChanges();
  }
}
