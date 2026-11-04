import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { PowerAcPlatformComponent } from './power-ac.component';
import { PowerAndACRecord, PowerAndACService } from '../../../../services/power-and-ac.service';
import { RegionService } from '../../../../services/region.service';
import { AuthService } from '../../../../services/auth.service';

const regions = [
  { id: 1, region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', leaCode: 'AD' },
  { id: 2, region: 'R1', province: 'P1', networkEngineer: 'NW/ENG1', leaCode: 'PR' },
  { id: 3, region: 'R2', province: 'P2', networkEngineer: 'NW/ENG2', leaCode: 'JA' }
];

const record = (id: number, designation: string, month = 3): PowerAndACRecord => ({
  id, designation, year: 2026, month, scheduled: 10, attended: 8,
  cumulative_Sched: 30, cumulative_Achieved: 24, isVerified: false
});

describe('PowerAcPlatformComponent (RTOM AREA selector removed)', () => {
  const records = [record(1, 'NW/AD'), record(2, 'NW/PR'), record(3, 'NW/JA'), record(4, 'NW/AD', 4)];
  const toggleVerified = vi.fn((id: number) => of({ id, isVerified: true }));
  const getByYear = vi.fn((year: number) => of(records.filter(r => r.year === year).map(r => ({ ...r }))));

  async function create() {
    await TestBed.configureTestingModule({
      imports: [PowerAcPlatformComponent],
      providers: [
        { provide: PowerAndACService, useValue: { getByYear, toggleVerified } },
        { provide: RegionService, useValue: { getAll: () => of(regions) } },
        { provide: AuthService, useValue: { canEditPage: () => true } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(PowerAcPlatformComponent);
    const component = fixture.componentInstance;
    component.selectedYear = 2026;
    component.selectedMonth = 3;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component };
  }

  it('does not render an RTOM AREA selector or chip', async () => {
    const { fixture } = await create();
    const html = (fixture.nativeElement as HTMLElement).innerHTML;
    expect(html).not.toContain('RTOM AREA');
    expect((fixture.nativeElement as HTMLElement).querySelector('#pac-area')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).querySelector('#pac-engineer')).not.toBeNull();
  });

  it('shows every area for the selected month without an area selection', async () => {
    const { component } = await create();
    expect(component.filteredRecords.map(r => r.designation)).toEqual(['NW/AD', 'NW/PR', 'NW/JA']);
  });

  it('loads records for the selected year from the database API', async () => {
    await create();
    expect(getByYear).toHaveBeenCalledWith(2026);
  });

  it('keeps region, province and engineer filters working', async () => {
    const { component } = await create();
    component.selectedRegion = 'R1';
    expect(component.filteredRecords.map(r => r.designation)).toEqual(['NW/AD', 'NW/PR']);
    component.selectedRegion = 'R2';
    component.selectedProvince = 'P2';
    component.selectedEngineer = 'NW/ENG2';
    expect(component.filteredRecords.map(r => r.designation)).toEqual(['NW/JA']);
  });

  it('still toggles verification for the clicked record', async () => {
    const { component } = await create();
    const target = component.filteredRecords[1];
    component.toggleVerified(target);
    expect(toggleVerified).toHaveBeenCalledWith(2);
    expect(target.isVerified).toBe(true);
  });
});
