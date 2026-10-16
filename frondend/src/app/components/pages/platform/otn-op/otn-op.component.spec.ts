import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { OtnOpComponent } from './otn-op.component';
import { OtnOp1Metric, OtnOp1Service } from '../../../../services/otn-op1.service';
import { OtnOp2Metric, OtnOp2Service } from '../../../../services/otn-op2.service';
import { RegionService } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';

const regions = [
  { region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', leaCode: 'AD/PR' },
  { region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', leaCode: 'JA' }
];

describe('OtnOpComponent (RTOM AREA selector removed)', () => {
  const op1Upsert = vi.fn((_id: number, _m: OtnOp1Metric[]) => of({}));
  const op2Upsert = vi.fn((_id: number, _m: OtnOp2Metric[]) => of({}));

  async function create() {
    op1Upsert.mockClear();
    op2Upsert.mockClear();
    await TestBed.configureTestingModule({
      imports: [OtnOpComponent],
      providers: [
        {
          provide: OtnOp1Service, useValue: {
            getAllKpis: () => of([{ id: 1, networkEngineerKpi: 'OTN availability', kpiPercent: 5 }]),
            getMetrics: () => of([{ id: 11, otnOp1Id: 1, site: 'ADIPR', unavailableMinutes: 30, totalMinutes: 44640, totalNodes: 1, year: 2026, month: 1 }]),
            upsertMetrics: op1Upsert
          }
        },
        {
          provide: OtnOp2Service, useValue: {
            getAllKpis: () => of([{ id: 2, networkEngineerKpi: 'Fiber restoration', kpiPercent: 5 }]),
            getMetrics: () => of([{ id: 21, otnOp2Id: 2, site: 'JA', totalFailedLinks: 4, linksSlaNotViolated: 3, year: 2026, month: 1 }]),
            upsertMetrics: op2Upsert
          }
        },
        { provide: RegionService, useValue: { getAll: () => of(regions) } },
        { provide: AuthService, useValue: { canEditPage: () => true } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(OtnOpComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }

  it('has no RTOM AREA selector and shows every site as a column', async () => {
    const { fixture, component } = await create();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.innerHTML).not.toContain('RTOM AREA');
    expect(el.querySelector('#dropdown4')).toBeNull();
    expect(component.areaKeys).toEqual(['adipr', 'ja']);
    const [op1, op2] = component.combinedData;
    expect(component.getOtnOp1Value(op1, 'unavailableMinutes', 'adipr')).toBe(30);
    expect(component.areaPercentage(op2, 'ja')).toBe('75.00%');
    expect(component.canEditMetrics).toBe(true);
  });

  it('narrows site columns with the engineer filters', async () => {
    const { component } = await create();
    component.onDropdownChange('dropdown1', 'R1');
    expect(component.areaKeys).toEqual(['adipr']);
  });

  it('saves to the existing site record of the edited column', async () => {
    const { component } = await create();
    const op1 = component.combinedData[0];
    component.startEdit(op1, 'unavailableMinutes', 'adipr');
    component.onEditInput('45');
    await component.doneEdit();
    const [id, [payload]] = op1Upsert.mock.calls[0];
    expect(id).toBe(1);
    expect(payload).toMatchObject({ id: 11, otnOp1Id: 1, site: 'ADIPR', unavailableMinutes: 45 });
  });

  it('saves a new area value with the same site label the old selector produced', async () => {
    const { component } = await create();
    const op2 = component.combinedData[1];
    component.startEdit(op2, 'totalFailedLinks', 'adipr');
    component.onEditInput('2');
    await component.doneEdit();
    const [id, [payload]] = op2Upsert.mock.calls[0];
    expect(id).toBe(2);
    expect(payload).toMatchObject({ id: 0, otnOp2Id: 2, site: 'ADPR', totalFailedLinks: 2 });
  });
});
