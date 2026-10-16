import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { IpNwOpComponent } from './ip-nw-op.component';
import { IpNwOpKpiDto, IpNwOpService } from '../../../../services/ip-nw-op.service';
import { RegionService } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';

const regions = [
  { region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', leaCode: 'AD/PR' },
  { region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', leaCode: 'JA' },
  { region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', leaCode: 'KO/MLT/MB/VA' }
];

const kpis: IpNwOpKpiDto[] = [{
  id: 3, network_engineer_kpi: 'IP core availability', division: 'Div', section: '99.9', kpi_percent: 5,
  unavailable_minutes: { adipr: 60, ja: 0 },
  total_minutes: { adipr: 44640, ja: 44640 },
  total_nodes: { adipr: 1, ja: 1 }
}];

describe('IpNwOpComponent (RTOM AREA selector removed)', () => {
  const getAll = vi.fn((_m?: number, _y?: number, _area?: string) => of(structuredClone(kpis)));
  const upsertMetric = vi.fn(() => of({} as IpNwOpKpiDto));

  async function create() {
    getAll.mockClear();
    upsertMetric.mockClear();
    await TestBed.configureTestingModule({
      imports: [IpNwOpComponent],
      providers: [
        { provide: IpNwOpService, useValue: { getAll, upsertMetric } },
        { provide: RegionService, useValue: { getAll: () => of(regions) } },
        { provide: AuthService, useValue: { canEditPage: () => true } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(IpNwOpComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }

  it('loads every area without an area query parameter', async () => {
    const { component } = await create();
    expect(getAll).toHaveBeenCalledTimes(1);
    expect(getAll.mock.calls[0][2]).toBeUndefined();
    expect(component.areaKeys).toEqual(['adipr', 'ja', 'komltmbva']);
  });

  it('has no RTOM AREA selector and shows stored values per area', async () => {
    const { fixture, component } = await create();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.innerHTML).not.toContain('RTOM AREA');
    expect(el.querySelector('#dropdown4')).toBeNull();
    expect(component.getAreaMetric(component.data[0], 'unavailable_minutes', 'adipr')).toBe('60');
    expect(component.getAreaPercentage(component.data[0], 'ja')).toBe('100.00%');
    expect(component.getAreaMetric(component.data[0], 'total_nodes', 'komltmbva')).toBe('-');
  });

  it('engineer filters narrow area columns without reloading', async () => {
    const { component } = await create();
    component.onDropdownChange('dropdown1', 'R2');
    expect(component.areaKeys).toEqual(['ja', 'komltmbva']);
    expect(getAll).toHaveBeenCalledTimes(1);
  });

  it('saves to the edited area code only', async () => {
    const { component } = await create();
    component.startEdit(component.data[0], 'unavailable_minutes', 'ja');
    component.onEditInput('12');
    await component.doneEdit();
    expect(upsertMetric).toHaveBeenCalledWith(3, 'ja', component.selectedMonth, component.selectedYear, { unavailableMinutes: 12 });
    expect(component.getAreaMetric(component.data[0], 'unavailable_minutes', 'ja')).toBe('12');
    expect(component.getAreaMetric(component.data[0], 'unavailable_minutes', 'adipr')).toBe('60');
  });
});
