using System.Reflection;
using backend.Controllers;
using backend.DTOs.PowerAc;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.PowerAc
{
    public class PowerAcServiceAndApiTests
    {
        private static PowerAcPeriod Month(int m) => PowerAcPeriod.TryCreate(2026, "Month", m, out var p, out _) ? p! : throw new Exception();

        [Fact]
        public async Task Calculation_reproduces_workbook_example_end_to_end()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            host.Db.PowerAcAreaAllocations.AddRange(
                new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 7 },
                new PowerAcAreaAllocation { EngineerGroupId = 2, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 3 });
            host.Db.SaveChanges();

            var context = await host.Get<PowerAcKpiCalculationService>().PreviewAsync(Month(1));
            var g1 = context.Output.Groups.Single(g => g.EngineerGroupId == 1);
            Assert.Equal(PowerAcResultStatus.Complete, g1.Status);
            Assert.Equal(0.925m, g1.CompositeAchievement);
            Assert.Equal(3500m, g1.MaximumPoints);
            Assert.Equal(3237.5m, g1.PointsAchieved);
        }

        [Fact]
        public async Task Missing_allocations_give_incomplete_status_without_points()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            host.Db.PowerAcAreaAllocations.Add(new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 7 });
            host.Db.SaveChanges();

            var context = await host.Get<PowerAcKpiCalculationService>().PreviewAsync(Month(1));
            Assert.All(context.Output.Groups, g =>
            {
                Assert.Equal(PowerAcResultStatus.Incomplete, g.Status);
                Assert.Null(g.PointsAchieved);
            });
        }

        [Fact]
        public async Task Historical_weights_and_allocations_are_resolved_by_effective_month()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(6);
            host.SeedMonth(7);
            // Allocation changes from July; weights change from July to 40/20/20/20.
            host.Db.PowerAcAreaAllocations.AddRange(
                new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 1 },
                new PowerAcAreaAllocation { EngineerGroupId = 2, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 1 },
                new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 7, AllocationValue = 3 });
            host.Db.PowerAcComponentWeights.AddRange(
                new PowerAcComponentWeight { ComponentId = 1, EffectiveYear = 2026, EffectiveMonth = 7, WeightPercent = 40 },
                new PowerAcComponentWeight { ComponentId = 2, EffectiveYear = 2026, EffectiveMonth = 7, WeightPercent = 20 },
                new PowerAcComponentWeight { ComponentId = 3, EffectiveYear = 2026, EffectiveMonth = 7, WeightPercent = 20 },
                new PowerAcComponentWeight { ComponentId = 4, EffectiveYear = 2026, EffectiveMonth = 7, WeightPercent = 20 });
            host.Db.SaveChanges();

            var service = host.Get<PowerAcKpiCalculationService>();
            var june = (await service.PreviewAsync(Month(6))).Output.Groups.Single(g => g.EngineerGroupId == 1);
            var july = (await service.PreviewAsync(Month(7))).Output.Groups.Single(g => g.EngineerGroupId == 1);

            // PM for June is quarter-to-date (Apr-Jun) but only June is seeded, so June PM is incomplete.
            // Weights and allocations are still resolved independently of data completeness.
            Assert.Equal(2500m, june.MaximumPoints);              // 5000 x 1 / 2
            Assert.Equal(3750m, july.MaximumPoints);              // 5000 x 3 / 4
            Assert.Equal(25m, june.Components.Single(c => c.Code == "PM").WeightPercent);
            Assert.Equal(40m, july.Components.Single(c => c.Code == "PM").WeightPercent);
            // July is the first month of Q3, so quarter-to-date PM is complete.
            Assert.Equal(0.92m, july.CompositeAchievement);       // 0.9x0.4 + 1x0.2 + 0.8x0.2 + 1x0.2
        }

        [Fact]
        public async Task Recalculation_replaces_the_snapshot_and_keeps_one_row_per_group()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            host.Db.PowerAcAreaAllocations.AddRange(
                new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 1 },
                new PowerAcAreaAllocation { EngineerGroupId = 2, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 1 });
            host.Db.SaveChanges();

            var service = host.Get<PowerAcKpiCalculationService>();
            await service.CalculateAndPersistAsync(Month(1), "tester");
            await service.CalculateAndPersistAsync(Month(1), "tester");

            var rows = await host.Db.PowerAcKpiResults.Include(r => r.ComponentScores).ToListAsync();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(4, r.ComponentScores.Count));
            Assert.All(rows, r => Assert.Equal(2500m, r.MaximumPoints));
            Assert.All(rows, r => Assert.Equal(PowerAcResultStatus.Complete, r.Status));
        }

        [Fact]
        public async Task Saved_results_show_the_weights_and_settings_used_by_the_snapshot()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            host.Db.PowerAcAreaAllocations.Add(new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 7 });
            host.Db.SaveChanges();
            var admin = PowerAcTestHost.User("Admin", 1, "admin");
            var controller = PowerAcTestHost.WithUser(
                new PowerAcKpiController(host.Db, host.Get<PowerAcAccessService>(), host.Get<PowerAcKpiCalculationService>()), admin);

            await controller.Calculate(2026, "Month", 1);
            var saved = Assert.IsType<PowerAcKpiResultsDto>(Assert.IsType<OkObjectResult>((await controller.GetResults(2026, "Month", 1)).Result).Value);

            Assert.False(saved.IsPreview);
            Assert.Equal(new decimal?[] { 25, 25, 25, 25 }, saved.Components.Select(c => c.CurrentWeightPercent).ToArray());
            Assert.Equal(100m, saved.WeightTotal);
            Assert.True(saved.WeightsValid);
            Assert.True(saved.PointsMethodApproved);
            Assert.True(saved.RequireVerifiedData);
            // Group 2 has no allocation: the shared issue is shown once, globally, not repeated per group.
            Assert.Contains(saved.GlobalIssues, i => i.Contains("Area allocation is missing"));
            Assert.All(saved.Groups, g => Assert.DoesNotContain(g.Issues, i => i.Contains("Area allocation is missing")));

            // Later weight changes must not alter how the stored snapshot is displayed.
            foreach (var w in host.Db.PowerAcComponentWeights) w.WeightPercent = w.ComponentId == 1 ? 40 : 20;
            host.Db.SaveChanges();
            var again = Assert.IsType<PowerAcKpiResultsDto>(Assert.IsType<OkObjectResult>((await controller.GetResults(2026, "Month", 1)).Result).Value);
            Assert.Equal(25m, again.Components.First(c => c.Code == "PM").CurrentWeightPercent);
        }

        [Fact]
        public async Task Quarter_requires_three_months_of_component_data()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.SeedMonth(1);
            host.SeedMonth(2);
            host.Db.PowerAcAreaAllocations.AddRange(
                new PowerAcAreaAllocation { EngineerGroupId = 1, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 1 },
                new PowerAcAreaAllocation { EngineerGroupId = 2, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 1 });
            host.Db.SaveChanges();

            PowerAcPeriod.TryCreate(2026, "Quarter", 1, out var q1, out _);
            var context = await host.Get<PowerAcKpiCalculationService>().PreviewAsync(q1!);
            Assert.All(context.Output.Groups, g => Assert.Equal(PowerAcResultStatus.Incomplete, g.Status));
            Assert.Contains(context.Output.Groups[0].Issues, i => i.Contains("March"));

            host.SeedMonth(3);
            var complete = await host.Get<PowerAcKpiCalculationService>().PreviewAsync(q1!);
            Assert.All(complete.Output.Groups, g => Assert.Equal(PowerAcResultStatus.Complete, g.Status));
            Assert.Equal(0.925m, complete.Output.Groups[0].CompositeAchievement);
        }

        // ---------------- Authorization ----------------

        [Fact]
        public async Task Platform_admin_needs_page_11_and_a_group_assignment()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.Db.PowerAcGroupUserAssignments.Add(new PowerAcGroupUserAssignment { EngineerGroupId = 1, UserId = 50 });
            host.Db.SaveChanges();
            var access = host.Get<PowerAcAccessService>();

            var assigned = PowerAcTestHost.User("PlatformAdmin", 50, "50", 11);
            Assert.True(await access.CanManageGroupAsync(assigned, 1));
            Assert.False(await access.CanManageGroupAsync(assigned, 2));

            var noPage = PowerAcTestHost.User("PlatformAdmin", 50, "50");
            Assert.False(await access.CanManageGroupAsync(noPage, 1));

            var otherPage = PowerAcTestHost.User("PlatformAdmin", 50, "50", 7);
            Assert.False(await access.CanManageGroupAsync(otherPage, 1));
        }

        [Fact]
        public async Task Role_rules_for_admin_user_and_verifier()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            var access = host.Get<PowerAcAccessService>();

            var admin = PowerAcTestHost.User("Admin", 1, "admin");
            Assert.True(await access.CanManageGroupAsync(admin, 2));
            Assert.True(await access.CanCalculateAsync(admin));
            Assert.False(await access.CanVerifyAsync(admin));

            var user = PowerAcTestHost.User("User", 2, "u2");
            Assert.True(await access.CanViewAsync(user));
            Assert.False(await access.CanCalculateAsync(user));
            Assert.False(await access.CanVerifyAsync(user));
            Assert.Empty(await access.GetManageableGroupIdsAsync(user));

            var platform = PowerAcTestHost.User("PlatformAdmin", 3, "p3", 11);
            Assert.True(await access.CanVerifyAsync(platform));
            Assert.True(await access.CanCalculateAsync(platform));

            var superAdmin = PowerAcTestHost.User("SuperAdmin", 4, "s4");
            Assert.True(await access.CanVerifyAsync(superAdmin));
        }

        [Fact]
        public void New_endpoints_are_never_anonymous_and_configuration_is_admin_only()
        {
            var controllers = new[] { typeof(PowerAcConfigController), typeof(PowerAcDataController), typeof(PowerAcKpiController) };
            foreach (var controller in controllers)
            {
                Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
                foreach (var method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
            }

            var writes = typeof(PowerAcConfigController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes().Any(a => a is HttpPostAttribute || a is HttpPutAttribute || a is HttpDeleteAttribute));
            foreach (var method in writes)
                Assert.Equal("AdminOnly", method.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        }

        [Fact]
        public void Overall_kpi_calculation_endpoints_are_no_longer_anonymous()
        {
            var calculate = typeof(OverallKpiResultsController).GetMethod("Calculate")!;
            var range = typeof(OverallKpiResultsController).GetMethod("CalculateRange")!;
            Assert.Null(calculate.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Null(range.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.NotNull(typeof(OverallKpiResultsController).GetCustomAttribute<AuthorizeAttribute>());
        }

        [Fact]
        public async Task User_role_cannot_calculate()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            var controller = PowerAcTestHost.WithUser(
                new PowerAcKpiController(host.Db, host.Get<PowerAcAccessService>(), host.Get<PowerAcKpiCalculationService>()),
                PowerAcTestHost.User("User", 2, "u2"));

            var result = await controller.Calculate(2026, "Month", 1);
            Assert.IsType<ForbidResult>(result.Result);
            Assert.Empty(host.Db.PowerAcKpiResults);
        }

        [Fact]
        public async Task Engineer_workflow_submit_and_independent_verification()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.Db.PowerAcGroupUserAssignments.Add(new PowerAcGroupUserAssignment { EngineerGroupId = 1, UserId = 50 });
            host.Db.SaveChanges();
            var access = host.Get<PowerAcAccessService>();
            var engineer = PowerAcTestHost.User("PlatformAdmin", 50, "eng50", 11);
            var data = PowerAcTestHost.WithUser(new PowerAcDataController(host.Db, access), engineer);

            // Group 2 is not assigned to this engineer.
            var forbidden = await data.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 2, Year = 2026, Month = 1, ComponentId = 2, IncidentCount = 0 });
            Assert.IsType<ForbidResult>(forbidden.Result);

            // Safety records hold an incident count only.
            var wrongField = await data.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 2, ApplicableCount = 5, CompletedCount = 5 });
            Assert.IsType<BadRequestObjectResult>(wrongField.Result);

            // PM is entered through the existing PowerAndAC screen.
            var pm = await data.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 1, ApplicableCount = 5, CompletedCount = 5 });
            Assert.IsType<BadRequestObjectResult>(pm.Result);

            var saved = await data.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 2, IncidentCount = 0 });
            var record = Assert.IsType<PowerAcComponentResultDto>(Assert.IsType<OkObjectResult>(saved.Result).Value);
            Assert.Equal(PowerAcRecordStatus.Draft, record.Status);

            var submitted = await data.Submit(record.Id);
            Assert.Equal(PowerAcRecordStatus.Submitted, Assert.IsType<PowerAcComponentResultDto>(Assert.IsType<OkObjectResult>(submitted.Result).Value).Status);

            // The submitter cannot verify their own record.
            var self = await data.Verify(record.Id, new VerifyPowerAcComponentResultDto { Approve = true });
            Assert.IsType<ConflictObjectResult>(self.Result);

            // A different page-11 Platform Admin can.
            var verifier = PowerAcTestHost.WithUser(new PowerAcDataController(host.Db, access), PowerAcTestHost.User("PlatformAdmin", 60, "ver60", 11));
            var verified = await verifier.Verify(record.Id, new VerifyPowerAcComponentResultDto { Approve = true, Comment = "ok" });
            Assert.Equal(PowerAcRecordStatus.Verified, Assert.IsType<PowerAcComponentResultDto>(Assert.IsType<OkObjectResult>(verified.Result).Value).Status);

            // Once verified, the engineer cannot change it.
            var locked = await data.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = 1, Year = 2026, Month = 1, ComponentId = 2, IncidentCount = 1 });
            Assert.IsType<ConflictObjectResult>(locked.Result);
        }

        [Fact]
        public async Task Weights_must_total_100_and_calculated_periods_are_protected()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            var admin = PowerAcTestHost.User("Admin", 1, "admin");
            var config = PowerAcTestHost.WithUser(new PowerAcConfigController(host.Db, host.Get<PowerAcAccessService>()), admin);

            var bad = await config.SaveWeights(new SavePowerAcWeightsDto
            {
                EffectiveYear = 2026, EffectiveMonth = 8,
                Weights = new() { new() { ComponentId = 1, WeightPercent = 30 }, new() { ComponentId = 2, WeightPercent = 25 }, new() { ComponentId = 3, WeightPercent = 25 }, new() { ComponentId = 4, WeightPercent = 25 } }
            });
            Assert.IsType<BadRequestObjectResult>(bad);

            var missing = await config.SaveWeights(new SavePowerAcWeightsDto
            {
                EffectiveYear = 2026, EffectiveMonth = 8,
                Weights = new() { new() { ComponentId = 1, WeightPercent = 50 }, new() { ComponentId = 2, WeightPercent = 50 } }
            });
            Assert.IsType<BadRequestObjectResult>(missing);

            // A snapshot exists for June, so weights effective from May would change it: rejected.
            host.Db.PowerAcKpiResults.Add(new PowerAcKpiResult { EngineerGroupId = 1, Year = 2026, PeriodType = "Month", PeriodNumber = 6, Status = "Complete", PointsMethod = "TargetScaled", CompositeTarget = 1 });
            host.Db.SaveChanges();
            var historic = await config.SaveWeights(new SavePowerAcWeightsDto
            {
                EffectiveYear = 2026, EffectiveMonth = 5,
                Weights = new() { new() { ComponentId = 1, WeightPercent = 40 }, new() { ComponentId = 2, WeightPercent = 20 }, new() { ComponentId = 3, WeightPercent = 20 }, new() { ComponentId = 4, WeightPercent = 20 } }
            });
            Assert.IsType<ConflictObjectResult>(historic);

            var ok = await config.SaveWeights(new SavePowerAcWeightsDto
            {
                EffectiveYear = 2026, EffectiveMonth = 7,
                Weights = new() { new() { ComponentId = 1, WeightPercent = 40 }, new() { ComponentId = 2, WeightPercent = 20 }, new() { ComponentId = 3, WeightPercent = 20 }, new() { ComponentId = 4, WeightPercent = 20 } }
            });
            Assert.IsType<OkObjectResult>(ok);
            Assert.Equal(8, host.Db.PowerAcComponentWeights.Count()); // 4 original + 4 new; history kept
        }

        [Fact]
        public async Task Allocation_entry_is_limited_to_assigned_groups()
        {
            using var host = new PowerAcTestHost();
            host.SeedBasics();
            host.Db.PowerAcGroupUserAssignments.Add(new PowerAcGroupUserAssignment { EngineerGroupId = 1, UserId = 50 });
            host.Db.SaveChanges();
            var data = PowerAcTestHost.WithUser(new PowerAcDataController(host.Db, host.Get<PowerAcAccessService>()), PowerAcTestHost.User("PlatformAdmin", 50, "eng50", 11));

            var own = await data.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = 1, AllocationValue = 12.5m } } });
            Assert.IsType<OkObjectResult>(own);

            var other = await data.SaveAllocations(new SavePowerAcAllocationsDto { EffectiveYear = 2026, EffectiveMonth = 1, Items = new() { new() { EngineerGroupId = 2, AllocationValue = 3m } } });
            Assert.IsType<ForbidResult>(other);

            var summary = Assert.IsType<PowerAcAllocationSummaryDto>(Assert.IsType<OkObjectResult>((await data.GetAllocations(2026, 1)).Result).Value);
            Assert.Equal(12.5m, summary.Total);
            Assert.Equal(1, summary.MissingCount);
            Assert.False(summary.IsComplete);
        }
    }
}
