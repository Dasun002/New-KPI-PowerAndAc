# Power & AC KPI – Implementation Notes

Branch: `feature/power-ac-kpi`. Source workbook: `New KPI Calculation_November 2026_New Method-1 (3).xlsx`.

## 1. What was built

The Power & AC KPI is now calculated from four configurable components per engineer group:

| Code | Component | Data source | Target | Seeded scoring method | Seeded approval |
|---|---|---|---|---|---|
| PM | Preventive Maintenance | existing `dbo.PowerAndAC` (Scheduled / Attended) | 100 % | RatioCapped | Not approved |
| SAFETY | Critical Safety | `dbo.PowerAcComponentResult.IncidentCount` | 0 incidents | **Pending** | Not approved |
| CORRECTIVE | Corrective Action and Maintenance Record | `dbo.PowerAcComponentResult` counts or % | 100 % | RatioCapped | Not approved |
| AVAILABILITY | Power System Availability | `dbo.PowerAcComponentResult` counts or % | 99 % | RatioCapped | Not approved |

Every scoring method starts **unapproved**. An Admin approves each one on the configuration screen after business confirmation. Until then, groups show a **Pending** status and receive no points.

The legacy Power & AC calculation in `OverallKpiResultsController` is **unchanged by default**. Set `PowerAc:UseComponentKpiInOverall` to `true` to feed Complete component results into `OverallKpiResult` for KPI definition `PowerAc:OverallKpiDefinitionId` (2017).

## 2. Formulas

Workbook references are from `'KPI Calculation'` row 19 and `'Power & AC'`.

```
component score     RatioCapped        = min(completed / applicable, 1)
                    RatioStrict        = completed / applicable; completed > applicable -> Incomplete
                    TargetScaled       = min((completed / applicable) / (target / 100), 1)
                    DirectPercent      = achievementPercent / 100
                    ZeroIncidentsBinary= 1 if incidents = 0, else 0               (option only; not approved)
                    DeductionPerIncident = max(0, 1 - incidents x parameter / 100) (option only; not approved)

composite           = sum(score x weight / 100)                            'Power & AC'!G11
maximum points      = PointsApplicable x allocation / total allocation      'KPI Calculation'!J19 = $H$19*D31/D40
points achieved     = IF(composite > target, max, max x composite / target), target 1   'KPI Calculation'!K19
```

**Worked example (workbook WPC1 values):**
- The component scores are PM 0.9, Safety 1, Corrective 0.8 and Availability 1, each weighted 25 %.
- Composite = 0.9×0.25 + 1×0.25 + 0.8×0.25 + 1×0.25 = **0.925**.
- With an allocation of 7 out of a total of 10, and 5000 Points Applicable, maximum points = 5000 × 7 / 10 = **3500**.
- Points achieved = 3500 × 0.925 / 1 = **3237.5**.

The backend tests reproduce this example (`Reproduces_workbook_composite_and_points`).

**Periods:**
- A **monthly** result uses quarter-to-date Preventive Maintenance (the existing convention). The other components use that month's record only.
- A **quarterly** result needs all three months of every component.
- Quarters are calendar quarters, so the PowerAndAC cumulative reset in April is expected.
- PM totals always come from the monthly Scheduled and Attended values. Cumulative snapshots are never summed; they are only cross-checked and reported as a warning if they disagree.

**A group is Complete only when all of these hold:**
- its mapping is verified;
- the weights total 100 %;
- every weighted component is scored from verified data (when verification is required);
- every scoring method and the points formula are approved;
- every active group has an allocation and the total is above 0.

Missing data is never treated as zero.

**History:**
- Weights, allocations and settings are effective-dated: the latest row on or before the period end applies.
- A version cannot be changed once results exist for that month or later. Use a later effective month instead.
- Each calculation is stored as a snapshot, with per-component scores, in `PowerAcKpiResult` and `PowerAcKpiComponentScore`.

## 3. Database changes

| Script | Purpose |
|---|---|
| `000_existing_powerandac_reference.sql` | Reference copy of the existing `PowerAndAC` table and trigger. **Do not run.** |
| `001_power_ac_kpi_deploy.sql` | Creates the 9 new tables, keys, checks and indexes, and seeds components, weights, settings and the 20 engineer groups. Re-runnable. |
| `001_power_ac_kpi_rollback.sql` | Drops only the 9 new tables. |
| `002_power_ac_kpi_verify.sql` | Read-only checks after deployment. |
| `003_power_ac_versions_audit_deploy.sql` / `_rollback.sql` | Scoring versions, audit log and snapshot version links (section 9). |
| `004_power_ac_data_readiness_report.sql` | Read-only report of missing allocations, data and verification. |

New tables:
- `PowerAcComponent`
- `PowerAcComponentWeight`
- `PowerAcEngineerGroup`
- `PowerAcGroupUserAssignment`
- `PowerAcAreaAllocation`
- `PowerAcComponentResult`
- `PowerAcCalculationSetting`
- `PowerAcKpiResult`
- `PowerAcKpiComponentScore`

The scripts don't change existing tables or rows. Area allocations are **not** seeded.

**Why SQL scripts and not EF migrations:** the database's `__EFMigrationsHistory` holds only `20260708000000_InitialCreate`, which is not in the repository. The repository's 13 migrations were never recorded against the database. Running `dotnet ef database update` would try to apply all of them. No migration was added.

### Engineer group mapping (`'Power & AC'!C19:C38`)

| Cell | Label | regiondata.network_engineer | PowerAndAC designation | Seeded as verified | Evidence |
|---|---|---|---|---|---|
| D19 | E/NW/ EPS | EP | NW/EP | **No** | Label is not an exact match. `'KPI Calculation'!BF19` (NW/EP column) references D19. Needs confirmation |
| D20 | E/NW/CPN | CPN | NW/CPN | Yes | Label + AK19 |
| D21 | E/NW/CPS | CPS | NW/CPS | Yes | Label + AN19 |
| D22 | E/NW/NCP | NCP | NW/NCP | Yes | Label + BO19 |
| D23 | E/NW/NP2 | NP-2 | NW/NP-2 | Yes | Label + BL19 |
| D24 | E/NW/NP1 | NP-1 | NW/NP-1 | Yes | Label + BI19 |
| D25 | E/NW/NWPE | NWPE | NW/NWPE | Yes | Confirmed mapping + AE19 |
| D26 | E/NW/NWPW | NWPW | NW/NWPW | Yes | Label + AH19 |
| D27 | E/NW/SAB | SAB | NW/SAB | Yes | Label + AT19 |
| D28 | E/NW/SPE | SPE | NW/SPE | Yes | Label + AW19 |
| D29 | E/NW/SPW | SPW | NW/SPW | Yes | Label + AZ19 |
| D30 | E/NW/UVA | UVA | NW/UVA | Yes | Label + AQ19 |
| D31 | E/NW/WPC1 | WPC-1 | NW/WPC-1 | Yes | Label + J19 |
| D32 | E/NW/WPC2 | WPC-2 | NW/WPC-2 | Yes | Label + M19 |
| D33 | E/NW/WPE | WPE | NW/WPE | Yes | Label + P19 |
| D34 | E/NW/WPN | WPN | NW/WPN | Yes | Label + Y19 |
| D35 | E/NW/WPNE | WPNE | NW/WPNE | Yes | Label + AB19 |
| D36 | E/NW/WPS | WPS | NW/WPS | Yes | Label only. Workbook BC19 is `#REF!` |
| D37 | E/NW/WPSE | WPSE | NW/WPSE | Yes | Confirmed mapping. Workbook V19 wrongly references D36 |
| D38 | E/NW/WPSW | WPSW | NW/WPSW | Yes | Confirmed mapping + S19 |

The script links each group to `regiondata` by looking up `network_engineer`, and stores the id. It marks a mapping verified only when that row and the PowerAndAC designation actually exist.

## 4. API

All routes need a logged-in user. None are anonymous. "View" means `ViewPagePolicy` for page 11 (POWER_AC).

| Method | Route | Who | Purpose |
|---|---|---|---|
| GET | /api/power-ac/config/components?year&month | View | Components with effective weight and allowed methods |
| PUT | /api/power-ac/config/components/{id} | AdminOnly | Target, scoring method, parameter, approval, active |
| GET | /api/power-ac/config/weights?year&month | View | Effective weights, total, history |
| POST | /api/power-ac/config/weights | AdminOnly | New weight set `{effectiveYear, effectiveMonth, weights[]}`. Must total 100 |
| GET | /api/power-ac/config/settings | View | Settings history with Points Applicable |
| POST | /api/power-ac/config/settings | AdminOnly | Points formula, target, approval, verification policy |
| GET | /api/power-ac/config/engineer-groups | View | 20 groups, mapping, and whether the caller can manage each |
| PUT | /api/power-ac/config/engineer-groups/{id} | AdminOnly | regiondata id, designation, verified, note |
| GET/POST/DELETE | /api/power-ac/config/assignments[/{id}] | AdminOnly | Which users may enter data for which group |
| GET | /api/power-ac/allocations?year&month | View | Effective allocation per group, total, missing count |
| GET | /api/power-ac/allocations/history?engineerGroupId | View | All versions for a group |
| PUT | /api/power-ac/allocations | Group managers | `{effectiveYear, effectiveMonth, items[]}`, value 0–1,000,000, max 4 dp |
| GET | /api/power-ac/component-results?year&month&componentId&engineerGroupId&status | View | Component records |
| PUT | /api/power-ac/component-results | Group managers | Upsert one record. Any change returns it to Draft |
| POST | /api/power-ac/component-results/{id}/submit | Group managers | Draft or Rejected → Submitted |
| POST | /api/power-ac/component-results/{id}/verify | Verifiers | `{approve, comment}`. Not by the person who entered or submitted the record |
| GET | /api/power-ac/kpi/preview?year&periodType&period | View | Live calculation, not saved |
| POST | /api/power-ac/kpi/calculate?year&periodType&period | Calculators | Calculate and replace the stored snapshot (transaction) |
| GET | /api/power-ac/kpi/results?year&periodType&period | View | Stored snapshot with component breakdown |

**Roles:**
- **Group managers** are Admin and SuperAdmin, plus a PlatformAdmin with page 11 edit rights *and* a `PowerAcGroupUserAssignment` row for that group.
- **Verifiers** are SuperAdmin, or a PlatformAdmin with page 11 edit rights.
- **Calculators** are Admin, SuperAdmin, or a PlatformAdmin with page 11 edit rights.

**Change to an existing endpoint:** `POST /api/overall-kpi-results/calculate` and `calculate-range` no longer have `[AllowAnonymous]`, so they require a login. The dashboard already sends the token.

## 5. Screens

- **Platform → Power & A/C KPI** (`/platform/power-ac-kpi`) has four tabs:
  - **Results** shows month or quarter, status counts, component weights, global issues, and a per-group table: four component scores, composite, allocation, maximum points and points achieved. Expanding a row shows the component breakdown, issues and warnings. It also has the preview and calculate-and-save buttons.
  - **Area Allocation** lists the 20 workbook rows with mapping status. Values are editable only for groups the user manages. It shows the live total, the missing count and the effective month.
  - **Component Data** has monthly entry for Safety, Corrective and Availability, with Draft, Submit and status display.
  - **Review** lets verifiers verify or reject submitted records; a rejection needs a comment.
- **Admin → Power & A/C KPI Config** (`/admin/power-ac-config`) has five tabs: effective-dated weights with a live 100 % check and history, component targets and scoring approval, points formula and verification policy, engineer group mapping, and data-entry assignments.
- The existing Power & A/C pages, for PM data entry and the verify toggle, are unchanged.

## 6. Deployment

1. Take a full database backup.
2. Run `001_power_ac_kpi_deploy.sql` on a non-production copy:
   ```
   sqlcmd -S <server> -d <db> -b -i database/power-ac/001_power_ac_kpi_deploy.sql
   ```
3. Run `002_power_ac_kpi_verify.sql` and check that it shows 9 tables, 4 components, weights totalling 100, 1 settings row, and 20 groups with 19 verified.
4. Deploy the backend. Leave `PowerAc:UseComponentKpiInOverall` set to `false`.
5. Deploy the frontend (`npx ng build`).
6. Admin setup:
   - give the responsible users page 11 access (`UserPageAccess` / `PlatformKpiAssignment`, existing screens);
   - add Power & AC group assignments;
   - confirm D19 (EP);
   - approve scoring methods once the business confirms them.
7. Engineers enter allocations and component data. Verifiers verify. A calculator runs "Calculate & save".
8. After a parallel run against the workbook, set `PowerAc:UseComponentKpiInOverall` to `true` if the overall scorecard should use the new KPI.
9. To roll back: deploy the previous build, then run `001_power_ac_kpi_rollback.sql` after backing up the new tables.

## 7. Pending business decisions

1. **Critical Safety scoring rule** for 0, 1 and several incidents. The workbook only has a typed value of 1, and `'KPI Calculation'!K21` divides by its 0 target.
2. **Corrective Action** numerator and denominator (records completed ÷ records due, or actions closed ÷ raised), and whether a percentage may be entered directly.
3. **Power System Availability** measure and source: sites available ÷ applicable, outage minutes, or an external alarm/NMS feed. Also whether to score against the 99 % target (TargetScaled) or as a raw ratio.
4. **Preventive Maintenance** rule when Attended > Scheduled: cap at 100 % (RatioCapped) or reject (RatioStrict). Also whether monthly results should stay quarter-to-date.
5. **Verification policy:** only verified data counts (seeded) or all data. Note that only 5 of 120 existing PowerAndAC rows are verified.
6. **D19 "E/NW/ EPS"**: confirm it is NW/EP.
7. **Allocation rules:** what the value represents, its permitted range, and how often it changes.
8. **Points Applicable** ownership: it stays on KPI definition 2017 (Admin, KPI Management screen).
9. **Whether PlatformAdmins may also enter PM rows.** The existing PowerAndAC create, update and delete endpoints remain Admin-only.

## 8. Known issues outside this scope (not changed)

- `verify-azure-login` issues a token from a client-supplied email and Service ID without validating an Azure token.
- `TelemetryController` has no `[Authorize]`, including its `upsert` endpoint.
- The shared `CalculatePointsAchieved` in `OverallKpiResultsController` uses achieved ÷ 100 rather than achieved ÷ target for the other KPIs.
- `OverallKpiResult` stores some areas under two codes (for example `adpr` and `adipr`).
- The existing Angular test `app.spec.ts › should render title` fails on the base commit too.

## 9. Data readiness, versions and audit (scripts 003 and 004)

**Scoring versions.** `PowerAcComponentVersion` holds the method, target, parameter and approval of each component, effective from a month. A period is scored with the version effective for its last month. A version cannot be created or changed for a month that already has saved results. Approval is an explicit Admin action, recorded with who approved it, when, and an approval note. Changing the method, target or parameter clears the approval unless it is approved again. No version is ever approved by a deployment script.

**Audit log.** `PowerAcAuditLog` records every change: allocation create and update (old and new value), component record Created, Updated, Submitted, Verified and Rejected (status before and after, who, when, comment), scoring version Created, Updated, Approved and ApprovalWithdrawn, weights, settings, mappings, assignments and each calculation run.

**Snapshots.** Each saved result stores the settings row (`CalculationSettingId`) and the scoring version of every component (`ComponentVersionId`) it used. The saved-results view shows those, not today's configuration.

**Readiness.** `GET /api/power-ac/kpi/readiness?year&periodType&period` classifies every engineer group:

| State | Meaning |
|---|---|
| NotConfigured | Mapping unverified, allocation missing for any group, weights not 100%, or no settings |
| DataIncomplete | A required month of component data is missing |
| AwaitingVerification | Data present but not verified (when verification is required) |
| AwaitingApproval | Data ready but a scoring method or the points formula is not approved |
| ScoreReady | Complete result with points |

The Results tab shows this as a "Scoring Readiness" table with the list of decisions awaiting the business.

**Overall KPI.** With `PowerAc:UseComponentKpiInOverall = true`, the overall calculation reads the **saved** monthly snapshot. Only Complete groups get a row. Incomplete or Pending groups, and months with no saved snapshot, get no row (never a zero).

**Monthly PM data process.** An Admin enters the month on Admin → Power & A/C, which uses the existing PowerAndAC table and trigger. A page-11 Platform Admin then verifies it with the ✓/✗ toggle on the Results page or the existing Power & A/C page. The readiness table shows missing and unverified months per group. The existing PowerAndAC table has no "entered by" column, so self-verification of PM rows cannot be enforced by the backend.

### Test environment deployment

```
# 1. Create a separate test database from a backup (never the working database)
RESTORE DATABASE NWKPI_PAC_IT FROM DISK='/var/opt/mssql/backup/<backup>.bak'
  WITH RECOVERY, MOVE 'NWKPI' TO '/var/opt/mssql/data/NWKPI_PAC_IT.mdf', MOVE 'NWKPI_log' TO '/var/opt/mssql/data/NWKPI_PAC_IT_log.ldf'
# 2. Deploy, verify
sqlcmd -S <server> -d NWKPI_PAC_IT -b -i 001_power_ac_kpi_deploy.sql
sqlcmd -S <server> -d NWKPI_PAC_IT -b -i 003_power_ac_versions_audit_deploy.sql
sqlcmd -S <server> -d NWKPI_PAC_IT -i 002_power_ac_kpi_verify.sql
sqlcmd -S <server> -d NWKPI_PAC_IT -i 004_power_ac_data_readiness_report.sql
# 3. SQL Server integration tests (database name must contain _IT or TEST)
export POWERAC_SQL_TEST_CONNECTION="Server=<server>;Database=NWKPI_PAC_IT;User Id=<user>;Password=<from your secret store>;Encrypt=False;TrustServerCertificate=True"
dotnet test backend.Tests
# 4. Rollback order: 003 rollback, then 001 rollback (001 rollback also removes 003 objects if present)
```

The integration tests do not start the web application, so `Program.cs` startup writes never run during tests. Running the backend itself still applies those startup writes to whatever database it connects to.
