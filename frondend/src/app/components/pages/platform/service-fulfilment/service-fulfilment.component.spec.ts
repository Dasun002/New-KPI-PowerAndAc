import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToastrService } from 'ngx-toastr';
import { ServiceFulfilmentComponent } from './service-fulfilment.component';
import {
  ServiceFulfilmentKpiService,
  ServiceFulfilmentMetricDto,
  UpsertServiceFulfilmentMetricRequest
} from '../../../../services/service-fulfilment-kpi.service';
import { RegionService } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';

const regions = [
  { region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', leaCode: 'AD/PR' },
  { region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', leaCode: 'JA' }
];

const kpiRow = { id: 4, kpi: 'Fiber delivery', target: '95%', calculation: 'calc', platform: 'P', responsibleDgm: 'D', weightage: 10, dataSources: 'S', month: 3, year: 2026 };

const metrics: ServiceFulfilmentMetricDto[] = [
  { id: 4, kpi: 'Fiber delivery', target: '95%', platform: 'P', responsibleDgm: 'D', weightage: 10, area: 'AD/PR', kpiValue: 91, month: 3, year: 2026 },
  { id: 4, kpi: 'Fiber delivery', target: '95%', platform: 'P', responsibleDgm: 'D', weightage: 10, area: 'JA', kpiValue: 97, month: 3, year: 2026 }
];

describe('ServiceFulfilmentComponent (RTOM AREA selector removed)', () => {
  const getMetrics = vi.fn((_m: number, _y: number, _area?: string) => of(structuredClone(metrics)));
  const upsertMetric = vi.fn((_req: UpsertServiceFulfilmentMetricRequest) => of({} as ServiceFulfilmentMetricDto));

  async function create() {
    getMetrics.mockClear();
    upsertMetric.mockClear();
    vi.useFakeTimers();
    await TestBed.configureTestingModule({
      imports: [ServiceFulfilmentComponent],
      providers: [
        { provide: ServiceFulfilmentKpiService, useValue: { getAll: () => of([kpiRow]), getMetrics, upsertMetric } },
        { provide: RegionService, useValue: { getAll: () => of(regions) } },
        { provide: AuthService, useValue: { canEditPage: () => true, getRole: () => 'User' } },
        { provide: ToastrService, useValue: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(ServiceFulfilmentComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }

  afterEach(() => vi.useRealTimers());

  it('loads all areas without an area query parameter', async () => {
    await create();
    expect(getMetrics).toHaveBeenCalled();
    expect(getMetrics.mock.calls.every(call => call[2] === undefined)).toBe(true);
  });

  it('has no RTOM AREA selector and shows one KPI value column per area', async () => {
    const { fixture, component } = await create();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.innerHTML).not.toContain('RTOM AREA');
    expect(el.querySelector('#dropdown4')).toBeNull();
    expect(component.areaKeys).toEqual(['ADPR', 'JA']);
    expect(component.getColumnsToRender()).toEqual(['no', 'kpi', 'target', 'calculation', 'weightage', 'ADPR', 'JA']);
    expect(component.getCellValue(component.data[0], 'ADPR')).toBe(91);
    expect(component.getCellValue(component.data[0], 'JA')).toBe(97);
    expect(component.getColumnHeader('JA')).toBe('JA KPI Value');
  });

  it('saves the edited area with its stored area code', async () => {
    const { component } = await create();
    const row = component.data[0];
    component.startEdit(row, 'ADPR');
    component.activeEditValue = '92.5';
    component.saveEdit(row, 'ADPR');
    expect(upsertMetric).toHaveBeenCalledTimes(1);
    expect(upsertMetric.mock.calls[0][0]).toEqual({
      serviceFulfilmentKpiId: 4, areaCode: 'AD/PR', kpiValue: 92.5,
      month: component.selectedMonth, year: component.selectedYear
    });
  });

  it('refuses to save a non-area column instead of inventing an area', async () => {
    const { component } = await create();
    component.saveEdit(component.data[0], 'kpiValue');
    expect(upsertMetric).not.toHaveBeenCalled();
  });

  it('engineer filters narrow area columns', async () => {
    const { component } = await create();
    component.onDropdownChange('dropdown1', 'R2');
    expect(component.areaKeys).toEqual(['JA']);
  });
});
