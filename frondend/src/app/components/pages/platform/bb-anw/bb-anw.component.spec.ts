import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { BbAnwComponent } from './bb-anw.component';
import { BbAnwDto, BbAnwService } from '../../../../services/bb-anw.service';
import { RegionService } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';

// The component defaults to the previous calendar month.
const prev = new Date(new Date().getFullYear(), new Date().getMonth() - 1, 1);
const month = prev.getMonth() + 1;
const year = prev.getFullYear();

const regions = [
  { region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', engName: 'Engineer One', leaCode: 'AD/PR' },
  { region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', engName: 'Engineer Two', leaCode: 'JA' }
];

const rows: BbAnwDto[] = [{
  id: 7, networkEngineerKpi: 'MSAN availability', division: 'Div', section: '99', kpiPercent: 10,
  nodes: [
    { nodeCode: 'AD/PR', unavailableMinutes: 100, totalMinutes: 44640, totalNodes: 1, month, year },
    { nodeCode: 'Ja', unavailableMinutes: 0, totalMinutes: 89280, totalNodes: 2, month, year }
  ]
}];

const rtomAreas = [
  { areaCode: 'adipr', displayName: 'AD / PR' },
  { areaCode: 'ja', displayName: 'JA' }
];

describe('BbAnwComponent', () => {
  const update = vi.fn((_id: number, _dto: BbAnwDto) => of({}));

  async function create() {
    update.mockClear();
    await TestBed.configureTestingModule({
      imports: [BbAnwComponent],
      providers: [
        { provide: HttpClient, useValue: { get: () => of(rtomAreas) } },
        { provide: BbAnwService, useValue: { getAll: () => of(structuredClone(rows)), update } },
        { provide: RegionService, useValue: { getAll: () => of(regions) } },
        { provide: AuthService, useValue: { canEditPage: () => true } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(BbAnwComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }

  it('uses database area labels and shows every NW EE group and area column', async () => {
    const { fixture, component } = await create();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.innerHTML).not.toContain('RTOM AREA');
    expect(el.querySelector('#dropdown4')).toBeNull();
    expect(component.areaKeys).toEqual(['adipr', 'ja']);
    const headers = Array.from(el.querySelectorAll('thead th')).map(th => th.textContent?.trim());
    expect(headers).toContain('AD / PR (Achievement %)');
    expect(headers).toContain('JA (Achievement %)');
    expect(headers).toContain('NW/ENG1 - Engineer One');
    expect(headers).toContain('NW/ENG2 - Engineer Two');
    expect(component.areaEngineerGroups.map(group => group.areaKeys)).toEqual([['adipr'], ['ja']]);
    expect(component.getMetricDisplay(component.data[0], 'unavailableMinutes', 'adipr')).toBe('100');
    expect(component.getMetricDisplay(component.data[0], 'totalNodes', 'ja')).toBe('2');
  });

  it('narrows area columns with the engineer filters', async () => {
    const { component } = await create();
    component.onDropdownChange('dropdown1', 'R2');
    expect(component.areaKeys).toEqual(['ja']);
    component.onDropdownChange('dropdown1', '');
    expect(component.areaKeys).toEqual(['adipr', 'ja']);
  });

  it('saves an edit against the edited area NodeCode and keeps other areas intact', async () => {
    const { component } = await create();
    component.startEdit(component.data[0], 'unavailableMinutes', 'ja');
    component.onEditInput('15');
    await component.doneEdit();

    expect(update).toHaveBeenCalledTimes(1);
    const [id, dto] = update.mock.calls[0];
    expect(id).toBe(7);
    const byCode = Object.fromEntries((dto.nodes ?? []).map(n => [n.nodeCode, n]));
    expect(byCode['Ja'].unavailableMinutes).toBe(15);
    expect(byCode['AD/PR'].unavailableMinutes).toBe(100);
    expect(Object.keys(byCode).sort()).toEqual(['AD/PR', 'Ja']);
  });
});
