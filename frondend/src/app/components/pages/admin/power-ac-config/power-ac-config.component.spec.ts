import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { PowerAcConfigComponent } from './power-ac-config.component';
import { PowerAcKpiService } from '../../../../services/power-ac-kpi.service';
import { RegionService } from '../../../../services/region.service';
import { UserService } from '../../../../services/user.service';

const components = ['PM', 'SAFETY', 'CORRECTIVE', 'AVAILABILITY'].map((code, i) => ({
  id: i + 1, code, name: code, targetValue: 100, targetUnit: '%', dataSource: 'ComponentResult', scoringMethod: 'Pending',
  scoringParameter: null, scoringApproved: false, sortOrder: i + 1, isActive: true, currentWeightPercent: 25,
  allowedScoringMethods: ['Pending', 'RatioCapped']
}));

describe('PowerAcConfigComponent', () => {
  async function create() {
    const api = {
      getComponents: vi.fn(() => of(components)),
      getWeights: vi.fn(() => of({
        year: 2026, month: 11, total: 100, isValid: true,
        effective: components.map(c => ({ id: c.id, componentId: c.id, componentCode: c.code, effectiveYear: 2026, effectiveMonth: 1, weightPercent: 25, createdAt: '' })),
        history: []
      })),
      saveWeights: vi.fn(() => of({})),
      updateComponent: vi.fn((id: number, body: object) => of({ ...components[id - 1], ...body })),
      getComponentVersions: vi.fn((id: number) => of([
        { id: id * 10, componentId: id, componentCode: components[id - 1].code, effectiveYear: 2026, effectiveMonth: 1, targetValue: 100,
          scoringMethod: 'Pending', scoringParameter: null, scoringApproved: false, createdAt: '2026-11-01T00:00:00Z', createdBy: 'deploy-003' }
      ])),
      getSettings: vi.fn(() => of([])),
      saveSetting: vi.fn(() => of({})),
      getEngineerGroups: vi.fn(() => of([])),
      getAssignments: vi.fn(() => of([]))
    };
    await TestBed.configureTestingModule({
      imports: [PowerAcConfigComponent],
      providers: [
        { provide: PowerAcKpiService, useValue: api },
        { provide: RegionService, useValue: { getAll: () => of([]) } },
        { provide: UserService, useValue: { getAllUsers: () => of([]) } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(PowerAcConfigComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api, el: fixture.nativeElement as HTMLElement };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('loads the effective weights and shows a 100% total', async () => {
    const { component, el } = await create();
    expect(component.weightInputs.map(w => w.weightPercent)).toEqual([25, 25, 25, 25]);
    expect(component.weightsValid).toBe(true);
    expect(el.querySelector('#weight-total')?.textContent).toContain('100%');
    // Same structure as the other KPI admin screens (form card + kpi-table).
    expect(el.querySelector('.page-container .page-header h1')).not.toBeNull();
    expect(el.querySelector('.dashboard-container .form-container .form-header .btn-submit')).not.toBeNull();
    expect(el.querySelector('.table-responsive table.kpi-table')).not.toBeNull();
  });

  it('blocks saving when weights do not total 100%', async () => {
    const { component, fixture, el, api } = await create();
    component.weightInputs[0].weightPercent = 30;
    fixture.componentRef.changeDetectorRef.markForCheck();
    fixture.detectChanges();

    expect(component.weightTotal).toBe(105);
    expect(component.weightsValid).toBe(false);
    expect((el.querySelector('#save-weights') as HTMLButtonElement).disabled).toBe(true);
    expect(el.querySelector('#weight-error')).not.toBeNull();

    component.saveWeights();
    expect(api.saveWeights).not.toHaveBeenCalled();
    expect(component.error).toContain('105');
  });

  it('saves a valid weight set for the chosen effective month', async () => {
    const { component, api } = await create();
    component.weightYear = 2026;
    component.weightMonth = 12;
    component.weightInputs.forEach((w, i) => (w.weightPercent = [40, 20, 20, 20][i]));
    component.saveWeights();
    expect(api.saveWeights).toHaveBeenCalledWith(2026, 12, [
      { componentId: 1, weightPercent: 40 },
      { componentId: 2, weightPercent: 20 },
      { componentId: 3, weightPercent: 20 },
      { componentId: 4, weightPercent: 20 }
    ]);
  });

  it('edits a component through the form above a read-only table', async () => {
    const { component, fixture, el, api } = await create();
    component.setTab('components');
    fixture.componentRef.changeDetectorRef.markForCheck();
    fixture.detectChanges();

    const editButtons = el.querySelectorAll('table.kpi-table .actions-col .btn-edit');
    expect(editButtons.length).toBe(4);
    expect(el.querySelector('table.kpi-table input')).toBeNull(); // rows are read-only
    expect(component.componentForm).toBeNull();

    (editButtons[2] as HTMLButtonElement).click();
    fixture.componentRef.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    expect(component.componentForm?.code).toBe('CORRECTIVE');
    expect(el.querySelector('.form-container .form-header h2')?.textContent).toContain('CORRECTIVE');

    component.componentForm!.scoringMethod = 'RatioCapped';
    component.componentForm!.scoringApproved = true;
    component.saveComponent(component.componentForm!);
    expect(api.updateComponent).toHaveBeenCalledWith(3, expect.objectContaining({
      scoringMethod: 'RatioCapped', scoringApproved: true,
      effectiveYear: new Date().getFullYear(), effectiveMonth: new Date().getMonth() + 1
    }));
    expect(component.componentForm).toBeNull();
    expect(component.components[2].scoringApproved).toBe(true);
  });

  it('shows the scoring version history for every component', async () => {
    const { component, fixture, el, api } = await create();
    component.setTab('components');
    fixture.componentRef.changeDetectorRef.markForCheck();
    fixture.detectChanges();
    expect(api.getComponentVersions).toHaveBeenCalledTimes(4);
    expect(component.versions.length).toBe(4);
    const rows = el.querySelectorAll('#version-history tbody tr');
    expect(rows.length).toBe(4);
    expect(rows[0].textContent).toContain('January 2026');
  });

  it('refuses to approve a Pending scoring method', async () => {
    const { component, api } = await create();
    component.setTab('components');
    const safety = component.components[1];
    safety.scoringApproved = true;
    component.saveComponent(safety);
    expect(safety.error).toContain('Pending');
    expect(api.updateComponent).not.toHaveBeenCalled();
  });
});
