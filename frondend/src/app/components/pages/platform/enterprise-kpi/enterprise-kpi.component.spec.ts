import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToastrService } from 'ngx-toastr';
import { EnterpriseKpiComponent } from './enterprise-kpi.component';
import {
  EnterpriseKpiPlatformService,
  EnterpriseMetricDto,
  UpsertEnterpriseMetricRequest
} from '../../../../services/enterprise-kpi-platform.service';
import { RegionService } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';

const regions = [
  { region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', leaCode: 'CEN/HK' },
  { region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', leaCode: 'JA' }
];

const metric = (id: number, site: string, kpiValue: number, target: string): EnterpriseMetricDto => ({
  id, networkEngineerKpi: `KPI ${id}`, division: 'Div', section: null, target, kpiPercent: 10,
  site, kpiValue, month: 3, year: 2026
});

const metricsBySite: Record<string, EnterpriseMetricDto[]> = {
  CENHK: [metric(1, 'CENHK', 91, '95'), metric(2, 'CENHK', 80, '90')],
  JA: [metric(1, 'JA', 97, '95')]
};

describe('EnterpriseKpiComponent (RTOM AREA selector removed)', () => {
  const getMetrics = vi.fn((_m: number, _y: number, site?: string) => of(structuredClone(metricsBySite[site ?? ''] ?? [])));
  const upsertMetric = vi.fn((_req: UpsertEnterpriseMetricRequest) => of({ isNew: false } as EnterpriseMetricDto));

  async function create() {
    getMetrics.mockClear();
    upsertMetric.mockClear();
    vi.useFakeTimers();
    await TestBed.configureTestingModule({
      imports: [EnterpriseKpiComponent],
      providers: [
        {
          provide: EnterpriseKpiPlatformService, useValue: {
            getAll: () => of([
              { id: 1, networkEngineerKpi: 'KPI 1', division: 'Div', kpiPercent: 10 },
              { id: 2, networkEngineerKpi: 'KPI 2', division: 'Div', kpiPercent: 10 }
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
    const fixture = TestBed.createComponent(EnterpriseKpiComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }

  afterEach(() => vi.useRealTimers());

  it('loads metrics for every RTOM area code, one real site per request', async () => {
    await create();
    const sites = new Set(getMetrics.mock.calls.map(call => call[2]));
    expect(sites).toEqual(new Set(['CENHK', 'JA']));
    expect(getMetrics.mock.calls.every(call => !!call[2])).toBe(true);
  });

  it('has no RTOM AREA selector and shows each area value and target', async () => {
    const { fixture, component } = await create();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.innerHTML).not.toContain('RTOM AREA');
    expect(el.querySelector('#dropdown4')).toBeNull();
    expect(component.areaKeys).toEqual(['CENHK', 'JA']);
    const kpi1 = component.data.find(r => r.kpiId === 1)!;
    expect(component.getAreaValue(kpi1, 'CENHK')).toBe(91);
    expect(component.getAreaValue(kpi1, 'JA')).toBe(97);
    expect(component.getAreaTarget(kpi1, 'JA')).toBe('95');
    expect(kpi1.section).toBe('95');
    const kpi2 = component.data.find(r => r.kpiId === 2)!;
    expect(component.getAreaValue(kpi2, 'JA')).toBeNull();
  });

  it('engineer filters narrow area columns', async () => {
    const { component } = await create();
    component.onDropdownChange('dropdown1', 'R2');
    expect(component.areaKeys).toEqual(['JA']);
  });

  it('saves one KPI for one area with that area site and its own target', async () => {
    const { component } = await create();
    const kpi1 = component.data.find(r => r.kpiId === 1)!;
    component.enterAreaEdit(kpi1, 'JA');
    expect(component.areaEdit).toEqual({ kpiValue: '97', target: '95' });
    component.areaEdit.kpiValue = '98.5';
    component.saveArea(kpi1, 'JA');
    expect(upsertMetric).toHaveBeenCalledTimes(1);
    expect(upsertMetric.mock.calls[0][0]).toEqual({
      enterpriseKpiId: 1, site: 'JA', kpiValue: 98.5, target: '95',
      month: component.selectedMonth, year: component.selectedYear
    });
  });

  it('saves a new area value using the resolved area code', async () => {
    const { component } = await create();
    const kpi2 = component.data.find(r => r.kpiId === 2)!;
    component.enterAreaEdit(kpi2, 'JA');
    component.areaEdit = { kpiValue: '70', target: '' };
    component.saveArea(kpi2, 'JA');
    expect(upsertMetric.mock.calls[0][0]).toMatchObject({ enterpriseKpiId: 2, site: 'JA', kpiValue: 70, target: undefined });
  });
});
