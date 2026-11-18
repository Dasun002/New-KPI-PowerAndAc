import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { PowerAcKpiComponent } from './power-ac-kpi.component';
import { AuthService } from '../../../../services/auth.service';
import { PowerAcKpiService } from '../../../../services/power-ac-kpi.service';

describe('PowerAcKpiComponent', () => {
  let fixture: ComponentFixture<PowerAcKpiComponent>;

  beforeEach(async () => {
    const api = {
      getData: () => of([]),
      getResults: () => of({ year: 2026, month: 11, pointsApplicable: 5000, totalAllocation: null, completeCount: 0, incompleteCount: 0,
        totalMaximumPoints: null, totalPointsAchieved: null, components: [], groups: [] }),
      saveData: () => of({ saved: 0, message: '' }),
      verify: () => of({ verified: 0, skipped: 0, message: '' }),
      getMonthly: () => of([]),
      toggleVerified: vi.fn((_year: number, _month: number, _designation: string) =>
        of({ designation: 'NW/CPN', isVerified: true })
      )
    };
    const auth = { getRole: () => 'SuperAdmin', canEditPage: () => false };
    await TestBed.configureTestingModule({
      imports: [PowerAcKpiComponent],
      providers: [
        { provide: PowerAcKpiService, useValue: api },
        { provide: AuthService, useValue: auth }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(PowerAcKpiComponent);
    fixture.detectChanges();
  });

  it('creates and loads the period', () => {
    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.componentInstance.canVerify).toBe(true);
  });

  it('updates the monthly achievement verification state when toggled', () => {
    const component = fixture.componentInstance;
    component.monthly.set('1|NW/CPN', {
      scheduled: 8,
      attended: 7,
      hasRow: true,
      isVerified: false
    });

    component.toggleMonthlyVerified(1, 'NW/CPN');

    expect(TestBed.inject(PowerAcKpiService).toggleVerified)
      .toHaveBeenCalledWith(component.year, 1, 'NW/CPN');
    expect(component.monthlyVerified(1, 'NW/CPN')).toBe(true);
  });

  it('shows a verification button in every achievement cell and keeps monthly values read-only', () => {
    const component = fixture.componentInstance;
    component.rows = [{
      designation: 'NW/CPN',
      engineerGroupCode: 'CPN',
      label: 'E/NW/CPN',
      achievements: {},
      areaAllocation: null,
      isVerified: false,
      enteredBy: null,
      verifiedBy: null
    }];
    component.monthly.set('1|NW/CPN', {
      scheduled: 8,
      attended: 7,
      hasRow: true,
      isVerified: false
    });
    fixture.detectChanges();

    const verifyButton = fixture.nativeElement.querySelector('tbody tr:first-child td:nth-child(3) button');
    const verifyButtons = fixture.nativeElement.querySelectorAll('tbody tr button.approval-icon');
    const monthlyInputs = fixture.nativeElement.querySelectorAll('.monthly-readonly');
    expect(verifyButton).not.toBeNull();
    expect(verifyButton.disabled).toBe(false);
    expect(verifyButton.querySelector('.bi-x')).not.toBeNull();
    expect(verifyButtons.length).toBe(12);
    expect(verifyButtons[1].disabled).toBe(true);
    expect(monthlyInputs.length).toBe(24);
    expect(monthlyInputs[0].readOnly).toBe(true);
  });
});
