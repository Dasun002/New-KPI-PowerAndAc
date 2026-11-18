import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToastrService } from 'ngx-toastr';
import { OtherOperatorComponent } from './other-operator.component';
import {
  OtherMetricDto,
  OtherOperatorKpiPlatformService,
  UpsertOtherMetricRequest
} from '../../../../services/other-operator-kpi-platform.service';
import { RegionService } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';

const regions = [
  { region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', engName: 'Engineer One', leaCode: 'AD/PR' },
  { region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', engName: 'Engineer Two', leaCode: 'JA' }
];

const rtomAreas = [
  { areaCode: 'ADPR', displayName: 'AD / PR' },
  { areaCode: 'JA', displayName: 'JA' }
];

// Metric `id` is the metric row id; the KPI is identified by otherOperatorKpiId.
const metrics: OtherMetricDto[] = [
  { id: 501, otherKpiId: 1, site: 'AD/PR', kpiValue: 88, target: '90', kpiPercent: 10, month: 3, year: 2026 } as OtherMetricDto,
  { id: 502, otherKpiId: 1, site: 'JA', kpiValue: 93, target: '95', kpiPercent: 10, month: 3, year: 2026 } as OtherMetricDto
];

describe('OtherOperatorComponent', () => {
  const getMetrics = vi.fn((_m: number, _y: number, _site?: string) => of(structuredClone(metrics)));
  const upsertMetric = vi.fn((_req: UpsertOtherMetricRequest) => of({} as OtherMetricDto));

  async function create() {
    getMetrics.mockClear();
    upsertMetric.mockClear();
    vi.useFakeTimers();
    await TestBed.configureTestingModule({
      imports: [OtherOperatorComponent],
      providers: [
        { provide: HttpClient, useValue: { get: () => of(rtomAreas) } },
        {
          provide: OtherOperatorKpiPlatformService, useValue: {
            getAll: () => of([
              { id: 1, networkEngineerKpi: 'Tower uptime', division: 'Div', section: '90', kpiPercent: 10 },
              { id: 2, networkEngineerKpi: 'Tower PM', division: 'Div', section: '100', kpiPercent: 5 }
            ]),
            getMetrics,
            upsertMetric
          }
        },
        { provide: RegionService, useValue: { getAll: () => of(regions) } },
        { provide: AuthService, useValue: { canEditPage: () => true, getRole: () => 'User' } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(OtherOperatorComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }

  afterEach(() => vi.useRealTimers());

  it('loads all sites without a site query parameter', async () => {
    await create();
    expect(getMetrics).toHaveBeenCalled();
    expect(getMetrics.mock.calls.every(call => call[2] === undefined)).toBe(true);
  });

  it('uses database area labels and shows every area value grouped by NW EE', async () => {
    const { fixture, component } = await create();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.innerHTML).not.toContain('RTOM AREA');
    expect(el.querySelector('#dropdown4')).toBeNull();
    expect(component.areaKeys).toEqual(['ADPR', 'JA']);
    expect(component.areaEngineerGroups.map(group => group.areaKeys)).toEqual([['ADPR'], ['JA']]);
    expect(el.textContent).toContain('NW/ENG1 - Engineer One');
    expect(el.textContent).toContain('NW/ENG2 - Engineer Two');
    expect(el.textContent).toContain('AD / PR');
    const kpi1 = component.data.find(r => r.kpiId === 1)!;
    expect(component.getAreaValue(kpi1, 'ADPR')).toBe(88);
    expect(component.getAreaTarget(kpi1, 'JA')).toBe('95');
    const kpi2 = component.data.find(r => r.kpiId === 2)!;
    expect(kpi2.section).toBe('100');
  });

  it('saves with the stored site string of an existing area record', async () => {
    const { component } = await create();
    const kpi1 = component.data.find(r => r.kpiId === 1)!;
    component.enterAreaEdit(kpi1, 'ADPR');
    component.areaEdit.kpiValue = '89';
    component.saveArea(kpi1, 'ADPR');
    expect(upsertMetric).toHaveBeenCalledTimes(1);
    expect(upsertMetric.mock.calls[0][0]).toEqual({
      otherKpiId: 1, site: 'AD/PR', kpiValue: 89, target: '90',
      month: component.selectedMonth, year: component.selectedYear
    });
  });

  it('saves a new area value using the resolved area code', async () => {
    const { component } = await create();
    const kpi2 = component.data.find(r => r.kpiId === 2)!;
    component.enterAreaEdit(kpi2, 'JA');
    component.areaEdit = { kpiValue: '100', target: '100' };
    component.saveArea(kpi2, 'JA');
    expect(upsertMetric.mock.calls[0][0]).toMatchObject({ otherKpiId: 2, site: 'JA', kpiValue: 100, target: '100' });
  });

  it('engineer filters narrow area columns', async () => {
    const { component } = await create();
    component.onDropdownChange('dropdown1', 'R1');
    expect(component.areaKeys).toEqual(['ADPR']);
    component.onDropdownChange('dropdown2', 'P1');
    component.onDropdownChange('dropdown3', 'NW/ENG1');
    expect(component.areaKeys).toEqual(['ADPR']);
    expect(component.areaEngineerGroups.map(group => group.label)).toEqual(['NW/ENG1 - Engineer One']);
  });
});
