using System.ComponentModel.DataAnnotations;
using backend.Controllers;
using backend.DTOs.PowerAc;
using backend.Models;
using backend.Models.PowerAc;
using backend.Services;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace backend.Tests.PowerAc
{
    public class PowerAcReadinessWorkflowTests
    {
        private static PowerAcPeriod Month(int m) => PowerAcPeriod.TryCreate(2026, "Month", m, out var p, out _) ? p! : throw new Exception();

        private static PowerAcDataController Data(PowerAcTestHost host, System.Security.Claims.ClaimsPrincipal user) =>
            PowerAcTestHost.WithUser(new PowerAcDataController(host.Db, host.Get<PowerAcAccessService>()), user);

        private static PowerAcConfigController Config(PowerAcTestHost host, System.Security.Claims.ClaimsPrincipal user) =>
            PowerAcTestHost.WithUser(new PowerAcConfigController(host.Db, host.Get<PowerAcAccessService>()), user);

        private static PowerAcKpiController Kpi(PowerAcTestHost host, System.Security.Claims.ClaimsPrincipal user) =>
            PowerAcTestHost.WithUser(new PowerAcKpiController(host.Db, host.Get<PowerAcAccessService>(), host.Get<PowerAcKpiCalculationService>()), user);

        private static readonly System.Security.Claims.ClaimsPrincipal Admin = PowerAcTestHost.User("Admin", 1, "admin1");

        private static void AllocateBoth(PowerAcTestHost host, decimal g1 = 7, decimal g2 = 3)
        {
            host.Db.PowerAcAreaAllocations.AddRange(
                new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = g1 },
                new PowerAcAreaAllocation { EngineerGroupId = 2, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = g2 });
            host.Db.SaveChanges();
        }

        // ================= Allocations: all 20 groups =================

        public static IEnumerable<object[]> TwentyGroups() => Enumerable.Range(0, 20).Select(i => new object[] { 101 + i });

        [Theory]
        [MemberData(nameof(TwentyGroups))]
        public async Task Each_of_the_20_groups_accepts_allocation_only_from_its_assigned_engineer(int groupId)
        {
            using var host = new PowerAcTestHost();
            host.SeedTwentyGroups();
            host.Db.PowerAcGroupUserAssignments.Add(new PowerAcGroupUserAssignment { EngineerGroupId = groupId, UserId = 500 });
            host.Db.SaveChanges();
            var engineer = Data(host, PowerAcTestHost.User("PlatformAdmin", 500, "eng500", 11));

            var own = await engineer.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = groupId, AllocationValue = 4.25m } } });
            Assert.IsType<OkObjectResult>(own);

            var otherId = groupId == 120 ? 101 : groupId + 1;
            var other = await engineer.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = otherId, AllocationValue = 1m } } });
            Assert.IsType<ForbidResult>(other);

            Assert.Equal(4.25m, host.Db.PowerAcAreaAllocations.Single().AllocationValue);
            Assert.Contains(host.Db.PowerAcAuditLogs, a => a.EntityType == PowerAcAuditEntities.AreaAllocation && a.ChangedBy == "eng500");
        }

        [Fact]
        public async Task All_20_allocations_entered_make_the_set_complete_and_one_missing_does_not()
        {
            using var host = new PowerAcTestHost();
            host.SeedTwentyGroups();
            var admin = Data(host, Admin);

            var nineteen = Enumerable.Range(101, 19).Select(id => new PowerAcAllocationItemDto { EngineerGroupId = id, AllocationValue = id - 100 }).ToList();
            Assert.IsType<OkObjectResult>(await admin.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = nineteen }));
            var partial = Assert.IsType<PowerAcAllocationSummaryDto>(Assert.IsType<OkObjectResult>((await admin.GetAllocations(2026, 1)).Result).Value);
            Assert.Equal(1, partial.MissingCount);
            Assert.False(partial.IsComplete);

            Assert.IsType<OkObjectResult>(await admin.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = 120, AllocationValue = 20 } } }));
            var full = Assert.IsType<PowerAcAllocationSummaryDto>(Assert.IsType<OkObjectResult>((await admin.GetAllocations(2026, 1)).Result).Value);
            Assert.Equal(0, full.MissingCount);
            Assert.True(full.IsComplete);
            Assert.Equal(210m, full.Total); // 1 + 2 + ... + 20
        }

        [Fact]
        public async Task Invalid_allocation_requests_are_rejected()
        {
            using var host = new PowerAcTestHost();
            host.SeedTwentyGroups();
            var admin = Data(host, Admin);

            var duplicate = await admin.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = 101, AllocationValue = 1 }, new() { EngineerGroupId = 101, AllocationValue = 2 } } });
            Assert.IsType<BadRequestObjectResult>(duplicate);

            var unknown = await admin.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = 9999, AllocationValue = 1 } } });
            Assert.IsType<BadRequestObjectResult>(unknown);

            var tooPrecise = await admin.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = 101, AllocationValue = 1.00001m } } });
            Assert.IsType<BadRequestObjectResult>(tooPrecise);

            // Negative and out-of-range values are rejected by model validation before the action runs.
            Assert.False(IsValid(new PowerAcAllocationItemDto { EngineerGroupId = 101, AllocationValue = -1 }));
            Assert.False(IsValid(new PowerAcAllocationItemDto { EngineerGroupId = 101, AllocationValue = 1_000_001 }));
            Assert.False(IsValid(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 13, Items = new() { new() { EngineerGroupId = 101, AllocationValue = 1 } } }));
            Assert.True(IsValid(new PowerAcAllocationItemDto { EngineerGroupId = 101, AllocationValue = 0 }));

            Assert.Empty(host.Db.PowerAcAreaAllocations);
        }

        private static bool IsValid(object dto) =>
            Validator.TryValidateObject(dto, new ValidationContext(dto), new List<ValidationResult>(), validateAllProperties: true);

        // ================= Scoring approval states =================

        [Fact]
        public async Task Four_approval_states_control_scoring()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            AllocateBoth(host);
            var service = host.Get<PowerAcKpiCalculationService>();
            var safety = host.Db.PowerAcComponentVersions.Single(v => v.ComponentId == 2);

            // 1. Approved -> scored
            Assert.Equal(PowerAcResultStatus.Complete, (await service.PreviewAsync(Month(1))).Output.Groups[0].Status);

            // 2. Method chosen but not approved -> Pending, no points
            safety.ScoringApproved = false; safety.ApprovedBy = null; safety.ApprovedAt = null;
            host.Db.SaveChanges();
            var unapproved = (await service.PreviewAsync(Month(1))).Output.Groups[0];
            Assert.Equal(PowerAcResultStatus.Pending, unapproved.Status);
            Assert.Null(unapproved.PointsAchieved);

            // 3. Pending (rule not provided) -> Pending
            safety.ScoringMethod = PowerAcScoringMethods.Pending;
            host.Db.SaveChanges();
            Assert.Equal(PowerAcResultStatus.Pending, (await service.PreviewAsync(Month(1))).Output.Groups[0].Status);

            // 4. No version effective for the period -> Pending with an explicit reason
            host.Db.PowerAcComponentVersions.Remove(safety);
            host.Db.SaveChanges();
            var noVersion = (await service.PreviewAsync(Month(1))).Output.Groups[0];
            Assert.Equal(PowerAcResultStatus.Pending, noVersion.Status);
            Assert.Contains(noVersion.Issues, i => i.Contains("No scoring version is effective"));
        }

        [Fact]
        public async Task Admin_approval_is_recorded_versioned_and_reset_when_the_method_changes()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            var config = Config(host, Admin);

            // Pending cannot be approved.
            var pendingApproved = await config.UpdateComponent(2, new UpdatePowerAcComponentDto { EffectiveYear = 2026, EffectiveMonth = 7, TargetValue = 0, ScoringMethod = "Pending", ScoringApproved = true, IsActive = true });
            Assert.IsType<BadRequestObjectResult>(pendingApproved.Result);

            // Approve a new version from July with a note.
            var approved = await config.UpdateComponent(3, new UpdatePowerAcComponentDto { EffectiveYear = 2026, EffectiveMonth = 7, TargetValue = 100, ScoringMethod = "RatioStrict", ScoringApproved = true, ApprovalNote = "Board minute 12", IsActive = true, SortOrder = 3 });
            var dto = Assert.IsType<PowerAcComponentDto>(Assert.IsType<OkObjectResult>(approved.Result).Value);
            Assert.True(dto.ScoringApproved);
            Assert.Equal("admin1", dto.ApprovedBy);
            var july = host.Db.PowerAcComponentVersions.Single(v => v.ComponentId == 3 && v.EffectiveMonth == 7);
            Assert.Equal("Board minute 12", july.ApprovalNote);

            // Changing the method of that version without re-approving withdraws approval.
            await config.UpdateComponent(3, new UpdatePowerAcComponentDto { EffectiveYear = 2026, EffectiveMonth = 7, TargetValue = 100, ScoringMethod = "RatioCapped", ScoringApproved = false, IsActive = true, SortOrder = 3 });
            Assert.False(host.Db.PowerAcComponentVersions.Single(v => v.Id == july.Id).ScoringApproved);

            // January version is untouched (history preserved).
            Assert.True(host.Db.PowerAcComponentVersions.Single(v => v.ComponentId == 3 && v.EffectiveMonth == 1).ScoringApproved);

            var audit = host.Db.PowerAcAuditLogs.Where(a => a.EntityType == PowerAcAuditEntities.ComponentVersion && a.EntityId == july.Id).OrderBy(a => a.Id).Select(a => a.Action).ToList();
            Assert.Equal(new[] { "Created", "Approved", "Updated", "ApprovalWithdrawn" }, audit);

            var versions = Assert.IsType<List<PowerAcComponentVersionDto>>(Assert.IsType<OkObjectResult>((await config.GetComponentVersions(3)).Result).Value);
            Assert.Equal(2, versions.Count);
        }

        [Fact]
        public async Task A_later_scoring_version_does_not_change_an_earlier_period()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(6);
            host.SeedMonth(4); host.SeedMonth(5);
            AllocateBoth(host);
            var config = Config(host, Admin);
            var service = host.Get<PowerAcKpiCalculationService>();

            var juneBefore = (await service.PreviewAsync(Month(6))).Output.Groups[0];
            // From July, availability becomes unapproved.
            await config.UpdateComponent(4, new UpdatePowerAcComponentDto { EffectiveYear = 2026, EffectiveMonth = 7, TargetValue = 99, ScoringMethod = "TargetScaled", ScoringApproved = false, IsActive = true, SortOrder = 4 });
            var juneAfter = (await service.PreviewAsync(Month(6))).Output.Groups[0];

            Assert.Equal(PowerAcResultStatus.Complete, juneBefore.Status);
            Assert.Equal(juneBefore.Status, juneAfter.Status);
            Assert.Equal(juneBefore.PointsAchieved, juneAfter.PointsAchieved);
        }

        [Fact]
        public async Task Scoring_versions_cannot_be_changed_for_months_that_already_have_saved_results()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            AllocateBoth(host);
            await host.Get<PowerAcKpiCalculationService>().CalculateAndPersistAsync(Month(1), "calc");

            var config = Config(host, Admin);
            var blocked = await config.UpdateComponent(1, new UpdatePowerAcComponentDto { EffectiveYear = 2026, EffectiveMonth = 1, TargetValue = 100, ScoringMethod = "RatioStrict", ScoringApproved = true, IsActive = true, SortOrder = 1 });
            Assert.IsType<ConflictObjectResult>(blocked.Result);
        }

        // ================= Draft -> Submitted -> Verified =================

        [Fact]
        public async Task Only_verified_component_data_counts_and_every_transition_is_audited()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            // PM and the two other components verified for group 1; Safety goes through the workflow.
            host.Db.PowerAndAC.Add(new PowerAndAC { Designation = "NW/WPC-1", Year = 2026, Month = 1, Scheduled = 10, Attended = 9, Cumulative_Sched = 10, Cumulative_Achieved = 9, IsVerified = true });
            host.Db.PowerAcComponentResults.AddRange(
                new PowerAcComponentResult { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 3, ApplicableCount = 10, CompletedCount = 8, Status = PowerAcRecordStatus.Verified },
                new PowerAcComponentResult { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 4, AchievementPercent = 100, Status = PowerAcRecordStatus.Verified });
            host.Db.PowerAcGroupUserAssignments.Add(new PowerAcGroupUserAssignment { EngineerGroupId = 1, UserId = 50 });
            host.Db.SaveChanges();
            AllocateBoth(host);
            var service = host.Get<PowerAcKpiCalculationService>();
            var engineer = Data(host, PowerAcTestHost.User("PlatformAdmin", 50, "eng50", 11));
            var verifier = Data(host, PowerAcTestHost.User("PlatformAdmin", 60, "ver60", 11));

            async Task<string> SafetyState()
            {
                var report = PowerAcReadiness.Build(await service.PreviewAsync(Month(1)));
                return report.Groups.Single(g => g.EngineerGroupId == 1).State;
            }

            // Draft
            var saved = await engineer.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 2, IncidentCount = 0 });
            var id = Assert.IsType<PowerAcComponentResultDto>(Assert.IsType<OkObjectResult>(saved.Result).Value).Id;
            Assert.Equal(PowerAcReadinessStates.AwaitingVerification, await SafetyState());

            // Submitted
            await engineer.Submit(id);
            Assert.Equal(PowerAcReadinessStates.AwaitingVerification, await SafetyState());

            // Rejected (comment required) -> still not counted
            Assert.IsType<BadRequestObjectResult>((await verifier.Verify(id, new VerifyPowerAcComponentResultDto { Approve = false })).Result);
            await verifier.Verify(id, new VerifyPowerAcComponentResultDto { Approve = false, Comment = "Check the incident log" });
            Assert.Equal(PowerAcReadinessStates.AwaitingVerification, await SafetyState());

            // Corrected, resubmitted, verified -> eligible
            await engineer.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 2, IncidentCount = 0, Remarks = "Log checked" });
            await engineer.Submit(id);
            await verifier.Verify(id, new VerifyPowerAcComponentResultDto { Approve = true });
            var group1 = (await service.PreviewAsync(Month(1))).Output.Groups.Single(g => g.EngineerGroupId == 1);
            Assert.Equal(PowerAcResultStatus.Complete, group1.Status);

            var history = Assert.IsType<List<PowerAcAuditEntryDto>>(Assert.IsType<OkObjectResult>((await engineer.GetHistory(id)).Result).Value);
            Assert.Equal(new[] { "Created", "Submitted", "Rejected", "Updated", "Submitted", "Verified" }, history.Select(h => h.Action).ToArray());
            Assert.Equal(new[] { "eng50", "eng50", "ver60", "eng50", "eng50", "ver60" }, history.Select(h => h.ChangedBy).ToArray());
            Assert.Equal("Check the incident log", history[2].Comment);
            Assert.Equal(PowerAcRecordStatus.Rejected, history[3].FromStatus);
            Assert.All(history, h => Assert.True(h.ChangedAt > DateTime.UtcNow.AddMinutes(-5)));
        }

        [Fact]
        public async Task Backend_blocks_self_verification_cross_group_submission_and_non_verifier_roles()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.Db.PowerAcGroupUserAssignments.AddRange(
                new PowerAcGroupUserAssignment { EngineerGroupId = 1, UserId = 50 },
                new PowerAcGroupUserAssignment { EngineerGroupId = 2, UserId = 70 });
            host.Db.SaveChanges();
            var eng1 = Data(host, PowerAcTestHost.User("PlatformAdmin", 50, "eng50", 11));
            var eng2 = Data(host, PowerAcTestHost.User("PlatformAdmin", 70, "eng70", 11));

            var saved = await eng1.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 3, ApplicableCount = 5, CompletedCount = 5 });
            var id = Assert.IsType<PowerAcComponentResultDto>(Assert.IsType<OkObjectResult>(saved.Result).Value).Id;

            // Another group's engineer cannot submit or edit it (no IDOR).
            Assert.IsType<ForbidResult>(await eng2.Submit(id) is { Result: var r1 } ? r1 : null);
            Assert.IsType<ForbidResult>((await eng2.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 3, ApplicableCount = 1, CompletedCount = 0 })).Result);

            await eng1.Submit(id);
            // Self-verification is refused by the API, not only hidden in the UI.
            Assert.IsType<ConflictObjectResult>((await eng1.Verify(id, new VerifyPowerAcComponentResultDto { Approve = true })).Result);
            // An Admin (not SuperAdmin) and a User cannot verify.
            Assert.IsType<ForbidResult>((await Data(host, Admin).Verify(id, new VerifyPowerAcComponentResultDto { Approve = true })).Result);
            Assert.IsType<ForbidResult>((await Data(host, PowerAcTestHost.User("User", 9, "u9")).Verify(id, new VerifyPowerAcComponentResultDto { Approve = true })).Result);
            // A PlatformAdmin without page 11 rights cannot verify either.
            Assert.IsType<ForbidResult>((await Data(host, PowerAcTestHost.User("PlatformAdmin", 80, "p80")).Verify(id, new VerifyPowerAcComponentResultDto { Approve = true })).Result);

            Assert.Equal(PowerAcRecordStatus.Submitted, host.Db.PowerAcComponentResults.Single().Status);
        }

        [Fact]
        public async Task Future_months_and_wrong_fields_are_rejected()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            var admin = Data(host, Admin);
            var future = DateTime.UtcNow.AddMonths(2);

            Assert.IsType<BadRequestObjectResult>((await admin.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = future.Year, Month = future.Month, ComponentId = 2, IncidentCount = 0 })).Result);
            Assert.IsType<BadRequestObjectResult>((await admin.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 3, ApplicableCount = 5 })).Result);
            Assert.IsType<BadRequestObjectResult>((await admin.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 4, IncidentCount = 1 })).Result);
            Assert.False(IsValid(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 4, AchievementPercent = 101 }));
            Assert.False(IsValid(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 2, IncidentCount = -1 }));
            Assert.Empty(host.Db.PowerAcComponentResults);
        }

        // ================= Readiness states =================

        [Fact]
        public async Task Readiness_distinguishes_all_five_states()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            var service = host.Get<PowerAcKpiCalculationService>();
            string StateOf(PowerAcReadinessReport r, int id) => r.Groups.Single(g => g.EngineerGroupId == id).State;

            // No allocations -> NotConfigured
            host.SeedMonth(1);
            var r1 = PowerAcReadiness.Build(await service.PreviewAsync(Month(1)));
            Assert.Equal(PowerAcReadinessStates.NotConfigured, StateOf(r1, 1));
            Assert.Equal(0, r1.AllocationsEntered);

            // Allocations -> ScoreReady (all data verified and approved)
            AllocateBoth(host);
            var r2 = PowerAcReadiness.Build(await service.PreviewAsync(Month(1)));
            Assert.Equal(PowerAcReadinessStates.ScoreReady, StateOf(r2, 1));
            Assert.Equal(2, r2.StateCounts[PowerAcReadinessStates.ScoreReady]);

            // Remove one Corrective record -> DataIncomplete
            var corrective = host.Db.PowerAcComponentResults.First(r => r.EngineerGroupId == 1 && r.ComponentId == 3);
            host.Db.PowerAcComponentResults.Remove(corrective);
            host.Db.SaveChanges();
            Assert.Equal(PowerAcReadinessStates.DataIncomplete, StateOf(PowerAcReadiness.Build(await service.PreviewAsync(Month(1))), 1));

            // Unverified PM -> AwaitingVerification for group 2
            host.Db.PowerAndAC.Single(p => p.Designation == "NW/WPC-2").IsVerified = false;
            host.Db.SaveChanges();
            Assert.Equal(PowerAcReadinessStates.AwaitingVerification, StateOf(PowerAcReadiness.Build(await service.PreviewAsync(Month(1))), 2));

            // Verified again but Availability unapproved -> AwaitingApproval
            host.Db.PowerAndAC.Single(p => p.Designation == "NW/WPC-2").IsVerified = true;
            var avail = host.Db.PowerAcComponentVersions.Single(v => v.ComponentId == 4);
            avail.ScoringApproved = false; avail.ApprovedBy = null; avail.ApprovedAt = null;
            host.Db.SaveChanges();
            var r5 = PowerAcReadiness.Build(await service.PreviewAsync(Month(1)));
            Assert.Equal(PowerAcReadinessStates.AwaitingApproval, StateOf(r5, 2));
            Assert.Contains(r5.PendingBusinessDecisions, d => d.Contains("Power System Availability"));
            Assert.Equal(3, r5.ApprovedComponentCount);
        }

        [Fact]
        public async Task Readiness_endpoint_requires_view_access_and_reports_missing_months()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(5); // Q2 has April missing for a June monthly result
            AllocateBoth(host);
            var result = await Kpi(host, PowerAcTestHost.User("User", 3, "u3")).Readiness(2026, "Month", 6);
            var report = Assert.IsType<PowerAcReadinessReport>(Assert.IsType<OkObjectResult>(result.Result).Value);
            var pm = report.Groups[0].Components.Single(c => c.Code == "PM");
            Assert.False(pm.DataComplete);
            Assert.Contains(pm.Missing, m => m.Contains("April"));
            Assert.Contains(pm.Missing, m => m.Contains("June"));
            Assert.Equal(PowerAcReadinessStates.DataIncomplete, report.Groups[0].State);
        }

        // ================= Snapshot consistency =================

        [Fact]
        public async Task Saved_snapshot_records_and_reports_the_exact_versions_used()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            AllocateBoth(host);
            var kpi = Kpi(host, Admin);
            await kpi.Calculate(2026, "Month", 1);

            var result = host.Db.PowerAcKpiResults.Include(r => r.ComponentScores).First(r => r.EngineerGroupId == 1);
            Assert.NotNull(result.CalculationSettingId);
            var versionIds = host.Db.PowerAcComponentVersions.Select(v => v.Id).ToHashSet();
            Assert.All(result.ComponentScores, s => Assert.Contains(s.ComponentVersionId!.Value, versionIds));
            Assert.Contains(host.Db.PowerAcAuditLogs, a => a.EntityType == PowerAcAuditEntities.KpiCalculation && a.Action == "Calculated" && a.ChangedBy == "admin1");

            // Withdrawing approval later (directly, simulating a future change) must not alter how the snapshot is shown.
            var safety = host.Db.PowerAcComponentVersions.Single(v => v.ComponentId == 2);
            var safetyVersionId = safety.Id;
            safety.ScoringApproved = false; safety.ApprovedBy = null; safety.ApprovedAt = null;
            host.Db.SaveChanges();
            var saved = Assert.IsType<PowerAcKpiResultsDto>(Assert.IsType<OkObjectResult>((await kpi.GetResults(2026, "Month", 1)).Result).Value);
            Assert.Equal(PowerAcResultStatus.Complete, saved.Groups.Single(g => g.EngineerGroupId == 1).Status);
            Assert.Equal(3237.5m, saved.Groups.Single(g => g.EngineerGroupId == 1).PointsAchieved);
            Assert.Equal(safetyVersionId, saved.Groups[0].Components.Single(c => c.Code == "SAFETY").ComponentVersionId);
            Assert.Equal(100m, saved.WeightTotal);
        }

        // ================= Overall KPI integration =================

        private static OverallKpiResultsController Overall(PowerAcTestHost host, bool useComponentKpi)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PowerAc:UseComponentKpiInOverall"] = useComponentKpi.ToString(),
                ["PowerAc:OverallKpiDefinitionId"] = "2017"
            }).Build();
            return new OverallKpiResultsController(host.Db,
                new RoutineMaintenanceService(host.Db), new TowerMaintenanceService(host.Db), new PowerAndACService(host.Db), config);
        }

        private static void PrepareOverallKpi(PowerAcTestHost host)
        {
            var kpi = host.Db.KpiDefinitions.Single(k => k.Id == 2017);
            kpi.Perspectives = "Customer";
            kpi.Category = "Power & AC";
            kpi.DescriptionOfKPI = "90.000%";
            host.Db.SaveChanges();
        }

        [Fact]
        public async Task Overall_kpi_uses_only_complete_saved_snapshots_when_the_switch_is_on()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            PrepareOverallKpi(host);
            host.SeedMonth(1);
            AllocateBoth(host);
            // Group 2 loses its Corrective record, so its saved status is Incomplete.
            host.Db.PowerAcComponentResults.Remove(host.Db.PowerAcComponentResults.First(r => r.EngineerGroupId == 2 && r.ComponentId == 3));
            host.Db.SaveChanges();
            await host.Get<PowerAcKpiCalculationService>().CalculateAndPersistAsync(Month(1), "calc");

            await Overall(host, true).Calculate(1, 2026);
            var rows = host.Db.OverallKpiResults.Where(r => r.KpiDefinitionId == 2017).ToList();
            var row = Assert.Single(rows);                 // only the Complete group
            Assert.Equal("hk", row.AreaCode);
            Assert.Equal(3500m, row.MaximumPointsPerKpi);
            Assert.Equal(3237.5m, row.PointsAchieved);
            Assert.Equal(92.5m, row.AchievedKpi);
            Assert.DoesNotContain(rows, r => r.AreaCode == "cenmd"); // incomplete group: no row, not a zero
        }

        [Fact]
        public async Task Overall_kpi_writes_nothing_for_power_ac_without_a_saved_snapshot()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            PrepareOverallKpi(host);
            host.SeedMonth(1);
            AllocateBoth(host);

            await Overall(host, true).Calculate(1, 2026);
            Assert.Empty(host.Db.OverallKpiResults.Where(r => r.KpiDefinitionId == 2017));
        }

        [Fact]
        public async Task Overall_kpi_keeps_the_legacy_branch_when_the_switch_is_off()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            PrepareOverallKpi(host);
            host.SeedMonth(1);
            AllocateBoth(host);
            await host.Get<PowerAcKpiCalculationService>().CalculateAndPersistAsync(Month(1), "calc");

            await Overall(host, false).Calculate(1, 2026);
            // Legacy branch: target 90 and allocation by scheduled count (10 and 10) -> 2500 each, not 3500/1500.
            var rows = host.Db.OverallKpiResults.Where(r => r.KpiDefinitionId == 2017).ToList();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(90m, r.TargetValue));
            Assert.All(rows, r => Assert.Equal(2500m, r.MaximumPointsPerKpi));
        }
    }
}
