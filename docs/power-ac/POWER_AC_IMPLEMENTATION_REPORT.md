# Power & A/C KPI – Implementation Report

**Branch:** `feature/power-ac-kpi` (not yet committed)
**Base commit:** `2bc65c1`
**Business source:** `New KPI Calculation_November 2026_New Method-1 (3).xlsx` (sheets "Power & AC" and "KPI Calculation")
**Development database:** `NWKPI_SAFETY` (Docker container `nwkpi-sqlserver`)
**SQL Server test database:** `NWKPI_PAC_IT`

---

## 1. Summary

The old Power & A/C KPI measured only Preventive Maintenance: attended ÷ scheduled, with a fixed 90% target. It split points by scheduled count. The new implementation follows the workbook. It combines four components into one weighted score per engineer group. It then splits the KPI's points by an area allocation that engineers enter.

| Component | Data source | Target |
|---|---|---|
| Preventive Maintenance (PM) | Existing `dbo.PowerAndAC` table | 100% |
| Critical Safety | New monthly records (incident count) | 0 incidents |
| Corrective Action and Maintenance Record | New monthly records (applicable / completed, or %) | 100% |
| Power System Availability | New monthly records (applicable / available, or %) | 99% |

The module never shows a score unless every input is present and every rule is approved. Each group is shown in one of five readiness states, from "Not configured" to "Score-ready".

**Current state:** the software is complete and tested. No group can be scored yet because the business inputs are missing (section 9).

---

## 2. How the calculation works

### 2.1 Equations

| Step | Equation | Workbook reference |
|---|---|---|
| Component score | Depends on the approved scoring method (table 2.2) | `'Power & AC'!G6:G9` |
| Composite achievement | Σ (component score × component weight ÷ 100) | `'Power & AC'!G11` = G6×0.25 + G7×0.25 + G8×0.25 + G9×0.25 |
| Maximum points | Points Applicable × group allocation ÷ total allocation | `'KPI Calculation'!J19` = $H$19 × 'Power & AC'!D31 ÷ 'Power & AC'!D40 |
| Points achieved | IF(composite > target, maximum, maximum × composite ÷ target), target = 1 | `'KPI Calculation'!K19` |

- **Points Applicable** comes from KPI definition 2017, "O&M of Power & Aircondition", which is 5,000 points.
- **Weights** are configurable. The 25% each is only the initial default.

**Worked example (workbook WPC1 values).**
- Component scores: PM 0.9, Safety 1, Corrective 0.8, Availability 1, each weighted 25%.
- Composite: 0.9×0.25 + 1×0.25 + 0.8×0.25 + 1×0.25 = **0.925**.
- Maximum points, with allocation 7 out of a total of 10: 5000 × 7 ÷ 10 = **3500**.
- Points achieved: 3500 × 0.925 ÷ 1 = **3237.5**.

The automated test `Reproduces_workbook_composite_and_points` checks this example.

### 2.2 Scoring methods (choose and approve per component)

| Method | Equation | Used for |
|---|---|---|
| `RatioCapped` | min(completed ÷ applicable, 1). Over-achievement is capped and reported as a warning | PM, Corrective, Availability |
| `RatioStrict` | completed ÷ applicable. Completed above applicable makes the component Incomplete | PM, Corrective, Availability |
| `TargetScaled` | min((completed ÷ applicable) ÷ (target ÷ 100), 1) | PM, Corrective, Availability |
| `DirectPercent` | entered percentage ÷ 100 | Corrective, Availability |
| `ZeroIncidentsBinary` | 1 if incidents = 0, otherwise 0 | Critical Safety (option only) |
| `DeductionPerIncident` | max(0, 1 − incidents × parameter ÷ 100) | Critical Safety (option only) |
| `Pending` | Not scored | Any component without a business-approved rule |

Code: `PowerAcKpiCalculator.ScoreComponent`, `ScoreRatio`, `ScoreDirectPercent` and `ScoreIncidents` in [PowerAcKpiCalculator.cs](../../backend/Services/PowerAc/PowerAcKpiCalculator.cs).

### 2.3 Reporting periods

- **Quarters** are calendar quarters: January–March, April–June, July–September, October–December. The PowerAndAC cumulative columns reset in April, which is expected.
- **Monthly result.** PM uses quarter-to-date monthly Scheduled and Attended values, the existing convention. The other components use that month's record.
- **Quarterly result.** All three months are required for every component.
- **PM totals** are always summed from the monthly values. Cumulative snapshot columns are never summed. They are only cross-checked, with a warning if they disagree.

Code: [PowerAcPeriod.cs](../../backend/Services/PowerAc/PowerAcPeriod.cs), and `BuildPmMeasurement` and `BuildComponentMeasurement` in [PowerAcKpiCalculationService.cs](../../backend/Services/PowerAc/PowerAcKpiCalculationService.cs).

### 2.4 When a group gets points

A group's status is **Complete** only when all of these are true:

1. Its engineer group mapping is verified.
2. The active component weights total exactly 100%.
3. Every weighted component has data for every required month. Missing data is never treated as zero.
4. That data is verified, when "only verified data counts" is on, which is the default.
5. Every component's scoring version for the period is approved, and not `Pending`.
6. The points formula is approved.
7. All 20 groups have an allocation, and the total is greater than 0.

Otherwise the status is **Incomplete** (data or configuration missing) or **Pending** (approval missing), and points are left empty.

### 2.5 Readiness states

Code: [PowerAcReadiness.cs](../../backend/Services/PowerAc/PowerAcReadiness.cs).

| State | Meaning | Who acts |
|---|---|---|
| Not configured | Mapping unverified, any allocation missing, weights not 100%, or no settings | Admin / engineers |
| Data incomplete | A required month of component data is missing | Engineers |
| Awaiting verification | Data entered but not verified | Verifiers |
| Awaiting approval | Data ready, but a scoring method or the points formula is not approved | Admin, after a business decision |
| Score-ready | Complete result with points | — |

### 2.6 History and reproducibility

- **Effective dating.** Weights, allocations, settings and scoring versions all have an effective month. A period uses the latest value on or before its last month.
- **Locked months.** A value cannot be changed for a month that already has saved results. A later effective month must be used, so earlier results stay reproducible.
- **Snapshots.** "Calculate & Save" stores one snapshot per group and period. Each snapshot records the weights, scores, the settings row and the exact scoring version of each component. Recalculating replaces the snapshot inside one database transaction.
- **Display.** The saved-results view shows what the snapshot used, not today's configuration.

### 2.7 Overall scorecard integration

- **Switch.** The setting `PowerAc:UseComponentKpiInOverall` in `appsettings.json` defaults to **false**. While it is false, the overall scorecard keeps the old Power & A/C calculation.
- **When switched on.** The overall calculation reads the **saved** monthly Power & A/C snapshot for KPI 2017. Only Complete groups get a row. Incomplete or Pending groups, and months without a saved snapshot, get no row (never a zero).

Code: [OverallKpiResultsController.cs](../../backend/Controllers/OverallKpiResultsController.cs).

---

## 3. Backend

### 3.1 Files created

| File | Contribution |
|---|---|
| `backend/Models/PowerAc/PowerAcEntities.cs` | Entities: component, weight, engineer group, group user assignment, area allocation, monthly component record, calculation setting, saved result and component score. Also the constants for codes, methods and statuses |
| `backend/Models/PowerAc/PowerAcVersioning.cs` | Effective-dated scoring version, audit log entry, and the audit and readiness state names |
| `backend/Data/PowerAcModelConfiguration.cs` | EF Core mappings: table names, decimal precision, keys, unique indexes and relationships |
| `backend/DTOs/PowerAc/PowerAcDtos.cs` | Request and response contracts, with validation ranges, for every Power & A/C endpoint |
| `backend/Services/PowerAc/PowerAcKpiCalculator.cs` | **The calculation engine.** Pure code with no database access: weight checks, allocation checks, component scoring, composite, maximum points and points achieved |
| `backend/Services/PowerAc/PowerAcKpiCalculationService.cs` | Loads configuration and data for a period, resolves effective versions, weights and allocations, builds the PM and component measurements, runs the calculator and saves snapshots in a transaction |
| `backend/Services/PowerAc/PowerAcPeriod.cs` | Month and quarter rules: months needed, quarter-to-date months, last month of the period |
| `backend/Services/PowerAc/PowerAcReadiness.cs` | Readiness report: state per group, per-component checks, and the list of decisions awaiting the business |
| `backend/Services/PowerAc/PowerAcAccessService.cs` | Permission rules: who can view, enter data per group, verify and calculate |
| `backend/Services/PowerAc/PowerAcRules.cs` | Shared validation: allowed scoring methods per component, required fields for submission, and the lock on months that have saved results |
| `backend/Services/PowerAc/PowerAcAudit.cs` | Writes audit log entries |
| `backend/Controllers/PowerAcConfigController.cs` | `api/power-ac/config`: components and scoring versions, weights, settings, engineer group mapping, data-entry assignments, audit log |
| `backend/Controllers/PowerAcDataController.cs` | `api/power-ac`: allocations, monthly component records, submit, verify or reject, record history |
| `backend/Controllers/PowerAcKpiController.cs` | `api/power-ac/kpi`: preview, calculate & save, saved results, readiness |

### 3.2 Files modified

| File | Change |
|---|---|
| `backend/Data/AppDbContext.cs` | Added 11 new tables and called the Power & A/C configuration |
| `backend/Program.cs` | Registered the calculation and access services |
| `backend/appsettings.json` | Added the `PowerAc` section: switch off, KPI id 2017. No passwords |
| `backend/Controllers/OverallKpiResultsController.cs` | Removed anonymous access from the two calculate endpoints, which now need a login. Added the opt-in Power & A/C branch that reads saved snapshots |

The existing PowerAndAC controller, service and table, and the Preventive Maintenance admin screen, were **not changed**.

### 3.3 API endpoints

| Method | Route | Who | Purpose |
|---|---|---|---|
| GET | /api/power-ac/config/components?year&month | Viewers | Components with the scoring version and weight effective that month |
| GET | /api/power-ac/config/components/{id}/versions | Viewers | Scoring version history |
| PUT | /api/power-ac/config/components/{id} | Admin | Create or update a scoring version from an effective month, and approve or withdraw it |
| GET / POST | /api/power-ac/config/weights | Viewers / Admin | Effective weights and history / save a new weight set (must total 100%) |
| GET / POST | /api/power-ac/config/settings | Viewers / Admin | Points formula, composite target, approval, verification policy |
| GET / PUT | /api/power-ac/config/engineer-groups[/{id}] | Viewers / Admin | 20 groups, regiondata link, PowerAndAC designation, verified flag |
| GET / POST / DELETE | /api/power-ac/config/assignments[/{id}] | Admin | Which users may enter data for which group |
| GET | /api/power-ac/config/audit | Admin | Audit log |
| GET / PUT | /api/power-ac/allocations | Viewers / group managers | Allocation per group for an effective month, total, missing count |
| GET | /api/power-ac/allocations/history?engineerGroupId | Viewers | All allocation versions of a group |
| GET / PUT | /api/power-ac/component-results | Viewers / group managers | Monthly Safety, Corrective and Availability records. Saving returns the record to Draft |
| POST | /api/power-ac/component-results/{id}/submit | Group managers | Draft or Rejected → Submitted |
| POST | /api/power-ac/component-results/{id}/verify | Verifiers | Submitted → Verified, or Rejected with a required comment. Never by the person who entered or submitted it |
| GET | /api/power-ac/component-results/{id}/history | Viewers | Every change to one record |
| GET | /api/power-ac/kpi/preview | Viewers | Live calculation, not saved |
| POST | /api/power-ac/kpi/calculate | Calculators | Calculate and replace the saved snapshot |
| GET | /api/power-ac/kpi/results | Viewers | Saved snapshot with per-component breakdown |
| GET | /api/power-ac/kpi/readiness | Viewers | Readiness per group |

**Roles:**
- **Viewers:** any logged-in user with Power & A/C page access (page 11).
- **Group managers:** Admin, SuperAdmin, or a PlatformAdmin with page-11 edit rights *and* an assignment to that group.
- **Verifiers:** SuperAdmin, or a PlatformAdmin with page-11 edit rights.
- **Calculators:** Admin, SuperAdmin, or a PlatformAdmin with page-11 edit rights.

The backend enforces all of these, not only the screens.

---

## 4. Database

### 4.1 Scripts

All scripts are in `database/power-ac/`. They are written for review and manual execution.

| Script | Purpose |
|---|---|
| `000_existing_powerandac_reference.sql` | Reference copy of the existing PowerAndAC table and trigger, which were not in source control before. **Do not run** |
| `001_power_ac_kpi_deploy.sql` | Creates 9 tables with keys, check constraints and indexes. Seeds the 4 components (all unapproved), 25% default weights from January 2026, one settings row (points formula unapproved) and the 20 engineer groups. Allocations are **not** seeded. Can be rerun |
| `001_power_ac_kpi_rollback.sql` | Drops only the Power & A/C tables, including the 003 objects if present |
| `002_power_ac_kpi_verify.sql` | Read-only checks after deployment |
| `003_power_ac_versions_audit_deploy.sql` | Adds scoring versions, the audit log and the snapshot version links. Copies one version per component from January 2026 with approval unchanged. Can be rerun |
| `003_power_ac_versions_audit_rollback.sql` | Removes only what 003 added |
| `004_power_ac_data_readiness_report.sql` | Read-only report: approvals, allocations, PM months present and verified, component records, saved snapshots |
| `README.md` | Technical notes and test-environment deployment steps |

**Why SQL scripts, not EF migrations.** The database's migration history does not match the repository's migrations. Running `dotnet ef database update` would try to re-apply 13 migrations to a live schema, so it must not be used.

### 4.2 Tables

| Table | Holds |
|---|---|
| PowerAcComponent | The 4 components (code, name, data source, display order) |
| PowerAcComponentVersion | Method, target, parameter and approval per component, effective from a month |
| PowerAcComponentWeight | Weight % per component, effective from a month |
| PowerAcCalculationSetting | Points formula, composite target, approval, verification policy, KPI id, effective from a month |
| PowerAcEngineerGroup | The 20 workbook rows (C19:C38), linked to `regiondata.id` and the exact PowerAndAC designation |
| PowerAcGroupUserAssignment | User ↔ engineer group data-entry rights |
| PowerAcAreaAllocation | Allocation per group (D19:D38), effective from a month |
| PowerAcComponentResult | Monthly Safety, Corrective and Availability records with status and who/when |
| PowerAcKpiResult / PowerAcKpiComponentScore | Saved snapshots per group and period, with the versions used |
| PowerAcAuditLog | Every change: before and after status, who, when, details |

Unchanged existing tables: `PowerAndAC` (PM source data), `KpiDefinition` (Points Applicable), `regiondata`, `OverallKpiResult`.

### 4.3 Engineer group mapping (`'Power & AC'!C19:C38`)

- **19 groups are verified.** For these, the label and the `'KPI Calculation'` row 19 formula agree, or the mapping was confirmed.
- **D19 "E/NW/ EPS" → EP is unverified.** The label doesn't exactly match.
- **Two workbook defects are recorded:**
  - the WP S-E column (V19) references D36 instead of D37;
  - the WPS column (BC19) is `#REF!`.

---

## 5. Frontend

### 5.1 Files created

| File | Contribution |
|---|---|
| `frondend/src/app/services/power-ac-kpi.service.ts` | API client and TypeScript types for all endpoints, plus weight-total and allocation validation helpers |
| `.../platform/power-ac-kpi/power-ac-kpi.component.ts / .html / .scss` | Platform page: readiness, results overview, component detail tables, allocation entry, component data entry, review and history. Uses the Tower/Routine Maintenance page theme |
| `.../admin/power-ac-config/power-ac-config.component.ts / .html / .scss` | Admin configuration: weights, components and scoring versions, points and verification, engineer groups, data-entry access. Uses the Enterprise KPI admin theme |
| `*.spec.ts` for both pages | Unit tests for the screens |

### 5.2 Files modified

| File | Change |
|---|---|
| `frondend/src/app/app.routes.ts` | Routes `platform/power-ac-kpi` (logged-in users) and `admin/power-ac-config` (Admin, SuperAdmin) |
| `frondend/src/app/page-config.ts` | Menu items "Power & A/C KPI" (Platform KPI) and "Power & A/C KPI Config" (Admin) |

---

## 6. How to use the screens

### 6.1 Platform KPI → Power & A/C KPI

Use the section buttons at the top. Filters (Period, Month or Quarter, Year) are in each card's blue title bar.

**Results** (everyone)
1. **Scoring Readiness** shows one ✓/✗ check per engineer for: mapping, allocation, each component's data, each component's verification, and approvals. The last row gives the readiness state. "Awaiting business decision" lists what only the business can resolve.
2. **Power & A/C KPI Overview** has KPI rows and engineer columns. Component scores carry ✓/✗ for scored or not. Below them are the weighted KPI, allocation, maximum points, points achieved and status. Hover a cell to see the reason.
3. **Detail tables:** Preventive Maintenance (Scheduled / Attended), Critical Safety (Incidents / Status), Corrective (Applicable / Completed), Availability (Applicable / Available), by month. Users with page-11 edit rights can click a PM ✓/✗ to verify that row.
4. **Calculation Notes** explains why each group is Incomplete or Pending.
5. **Calculate & Save** (Admins, and Platform Admins with page-11 edit rights) stores the snapshot for the period. Without it, the page shows a live preview.

**Area Allocation** (assigned engineers, Admins)
1. Choose the month the values take effect from.
2. Enter a value (0 to 1,000,000, up to 4 decimals) for your groups.
3. Click **Save n change(s)**.
- The header shows the saved total, the total with changes, and the missing count.

**Component Data** (assigned engineers, Admins)
1. Choose the month and component: Critical Safety, Corrective Action or Power System Availability.
2. Enter values per group:
   - Safety: the incident count;
   - Corrective and Availability: applicable and completed counts, or a percentage.
3. Click **Save**. The record is saved as Draft.
4. Click **Submit** when it's ready.
5. Click **History** to see every change.
- Preventive Maintenance is still entered on **Admin → Power & A/C**.

**Review** (SuperAdmin, and Platform Admins with page-11 edit rights)
- Click **Verify**, or click **Reject** with a comment, for each submitted record.
- You cannot verify a record you entered or submitted yourself.

### 6.2 Admin → Power & A/C KPI Config (Admin, SuperAdmin)

| Section | What to do |
|---|---|
| Weights | Choose the effective year and month, enter one weight per component (total must show 100%), then Save. The history table lists every weight set |
| Components & Scoring | Click **Edit** on a component. Set the effective month, target, scoring method and (for DeductionPerIncident) the deduction per incident. Tick "Scoring method approved" only after the business confirms the rule, and add the decision reference as the approval note. Click Update. The version history table shows every version, with who approved it and when |
| Points & Verification | Set the effective month, KPI id (2017), points formula (TargetScaled), composite target (1), "Points formula approved" and "Only verified data counts", then Save |
| Engineer Groups | Click **Edit** to link a workbook row to its regiondata record and PowerAndAC designation, then tick Verified once the business confirms it. D19 needs this |
| Data-entry Access | Choose an engineer group and a user, then Add. The user also needs Power & A/C page edit rights (page 11) from the existing user management screen |

### 6.3 First-time setup order

1. Admin: give the responsible users page 11 access, then add their engineer group assignments.
2. Business confirms D19; Admin edits and verifies it.
3. Engineers enter all 20 allocations.
4. Business confirms the scoring rules; Admin approves each component, and the points formula.
5. Each month: an Admin enters PM data, engineers enter and submit Safety, Corrective and Availability data, and verifiers verify all of it.
6. Admin clicks **Calculate & Save**. Readiness should show Score-ready.
7. After a parallel run against the workbook, decide whether to switch on `PowerAc:UseComponentKpiInOverall`.

---

## 7. Testing

| Suite | Result |
|---|---|
| Backend build | 0 errors |
| Backend tests, in-memory | 95 passed, 6 skipped (the SQL Server tests skip when no test database is configured) |
| Backend tests against SQL Server (`NWKPI_PAC_IT`) | **101 of 101 passed** |
| Frontend build | Succeeded |
| Frontend tests | 42 of 43 passed. The one failure, `app.spec.ts` "should render title", also fails on the original code |
| SQL scripts on `NWKPI_PAC_IT` | 001 and 003: deploy, rerun, rollback and redeploy all succeeded |

### Test files

| File | Covers |
|---|---|
| `backend.Tests/PowerAc/PowerAcKpiCalculatorTests.cs` | Workbook example, configurable weights, weight total, missing or zero allocations, missing data, Pending and unapproved methods, each scoring method, over-achievement, verification, points formulas |
| `backend.Tests/PowerAc/PowerAcPreventiveMaintenanceTests.cs` | Q1 and Q2 totals, April reset, quarter-to-date months, cumulative snapshots never summed, missing months, period rules |
| `backend.Tests/PowerAc/PowerAcServiceAndApiTests.cs` | End-to-end calculation, effective-dated history, snapshot replacement, quarters, roles, endpoint authorization, workflow, weight locking, saved-results weights fix |
| `backend.Tests/PowerAc/PowerAcReadinessWorkflowTests.cs` | Allocation rules for all 20 groups, four approval states, version history and approval audit, Draft → Submitted → Rejected → Verified with audit, self-verification and cross-group blocks, five readiness states, snapshot versions, overall scorecard integration |
| `backend.Tests/PowerAc/PowerAcSqlServerTests.cs` | Real SQL Server: script idempotency, no approvals or allocations seeded, check constraints, unique keys and filtered indexes, decimal precision, transactional calculation on real data, workflow and audit |
| `backend.Tests/PowerAc/PowerAcTestHost.cs` | Test setup with the real authorization policies; in-memory or SQL Server |
| `backend.Tests/backend.Tests.csproj` | xUnit test project (new) |

### Running the tests

```
cd backend.Tests && dotnet test          # in-memory; SQL Server tests skip
export POWERAC_SQL_TEST_CONNECTION="Server=...;Database=NWKPI_PAC_IT;User Id=...;Password=<secret>;Encrypt=False;TrustServerCertificate=True"
dotnet test                              # adds the SQL Server tests (database name must contain _IT or TEST)
cd ../frondend && npx ng test --watch=false
```

---

## 8. Current data status (NWKPI_SAFETY)

| Item | Status |
|---|---|
| Area allocations | 0 of 20 entered |
| Scoring methods approved | 0 of 4 (Critical Safety has no rule) |
| Engineer group mappings verified | 19 of 20 (D19 pending) |
| Safety / Corrective / Availability records | None for any month |
| Preventive Maintenance 2026 | January–June only, 120 rows. 5 verified (EP months 3–4, CPN month 1, CPS months 1 and 3). 17 groups have no verified month |
| PM rows with Attended > Scheduled | 25 group-months (shown as warnings, not changed) |
| Data-entry assignments | None |
| Saved snapshots | November 2026: 20 groups, all Incomplete |
| Readiness | All 20 groups "Not configured" |

---

## 9. What we have to do / decide

### Business decisions

| # | Decision | Why it blocks |
|---|---|---|
| 1 | **Critical Safety scoring rule** for 0, 1 and several incidents. The workbook has only a typed value of 1, and its formula divides by the 0 target | Safety stays Pending, so no group can be scored |
| 2 | **Approve or change the PM, Corrective and Availability methods.** For example, RatioCapped vs RatioStrict when attended exceeds scheduled, and TargetScaled vs a raw ratio for the 99% availability target | Unapproved components stay Pending |
| 3 | **Corrective Action definition:** what are "applicable" and "completed" (records due vs submitted, or actions raised vs closed)? | Engineers can't enter consistent data |
| 4 | **Power System Availability measure and source:** site counts, outage minutes, or an alarm/NMS feed | Same |
| 5 | **Allocation meaning and values** for the 20 groups (D19:D38) | No points without all 20 |
| 6 | **Confirm D19 "E/NW/ EPS" = NW/EP** | EP group stays unverified |
| 7 | **Confirm the points formula** (workbook K19) and the "only verified data counts" policy | Points stay Pending |
| 8 | **Monthly vs quarter-to-date** for monthly PM results, and how the yearly result is formed | Affects reported values |
| 9 | Should **Platform Admins enter PM data**? The existing PowerAndAC entry is Admin-only | Process ownership |

### Operational tasks

1. Assign page 11 access and Power & A/C group assignments to the responsible users.
2. Enter PM data for July onwards, and verify the existing January–June rows.
3. Enter monthly Safety, Corrective and Availability data, and have it verified.
4. On NWKPI_SAFETY, an Admin should review the January 2026 settings row, which shows the points formula as approved. An earlier deploy script set that, and has since been corrected. Either confirm it or save new unapproved settings.
5. Run a parallel month against the workbook before switching on `PowerAc:UseComponentKpiInOverall`.

### Technical tasks

1. Commit the branch and raise a pull request (not done yet).
2. Production deployment:
   1. take a full backup;
   2. run 001 and 003 on a restored copy, then 002 and 004, then the integration tests;
   3. run the same scripts on production;
   4. deploy the backend and frontend.
3. Do not run `dotnet ef database update`.

### Known risks (outside this work, not changed)

- **Startup writes.** Every backend start writes to the connected database: seeding, `ALTER TABLE` and backfills in `Program.cs`. Point it only at the intended database.
- **Login validation.** `verify-azure-login` issues a login token from a typed email and Service ID without validating an Azure token.
- **Telemetry endpoints** have no login requirement.
- **PM self-verification.** The existing PowerAndAC table doesn't record who entered a row, so self-verification of PM data can't be blocked by the backend.
- **Other KPIs' points formula.** The shared formula in `OverallKpiResultsController` uses achieved ÷ 100 instead of achieved ÷ target.
- **Area codes.** `OverallKpiResult` stores some areas under two different codes, for example `adpr` and `adipr`.
