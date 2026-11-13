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
      verify: () => of({ verified: 0, skipped: 0, message: '' })
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
});
