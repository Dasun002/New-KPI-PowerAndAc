/*
 * SQL Server integration tests for the Power & AC KPI.
 *
 * They run only when POWERAC_SQL_TEST_CONNECTION is set, and refuse to run unless the database name
 * contains "_IT" or "TEST" so they can never touch a working database. They do not start the web
 * application, so Program.cs startup writes never run. Every row they create is tagged "it-" and removed.
 *
 *   export POWERAC_SQL_TEST_CONNECTION="Server=localhost,1433;Database=NWKPI_PAC_IT;User Id=sa;Password=...;Encrypt=False;TrustServerCertificate=True"
 *   dotnet test --filter "FullyQualifiedName~PowerAcSqlServerTests"
 */
using System.Text.RegularExpressions;
using backend.Controllers;
using backend.Data;
using backend.DTOs.PowerAc;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.PowerAc
{
    public sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(PowerAcSqlFixture.ConnectionString))
                Skip = "POWERAC_SQL_TEST_CONNECTION is not set; SQL Server integration tests skipped.";
        }
    }

    public sealed class PowerAcSqlFixture : IDisposable
    {
        public static readonly string? ConnectionString = Environment.GetEnvironmentVariable("POWERAC_SQL_TEST_CONNECTION");
        public List<string> ScriptRuns { get; } = new();

        public PowerAcSqlFixture()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString)) return;

            var database = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
            if (!(database.Contains("_IT", StringComparison.OrdinalIgnoreCase) || database.Contains("TEST", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Refusing to run integration tests against '{database}'. Use a database whose name contains _IT or TEST.");

            // Deploy (idempotently) exactly the scripts that production would use.
            foreach (var script in new[] { "001_power_ac_kpi_deploy.sql", "003_power_ac_versions_audit_deploy.sql" })
            {
                RunScript(script);
                ScriptRuns.Add(script);
            }
            Cleanup();
        }

        public static string ScriptPath(string name)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "database", "power-ac")))
                dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("database/power-ac not found above the test output folder.");
            return Path.Combine(dir.FullName, "database", "power-ac", name);
        }

        public static void RunScript(string name)
        {
            var text = File.ReadAllText(ScriptPath(name));
            var batches = Regex.Split(text, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                .Where(b => !string.IsNullOrWhiteSpace(b));
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            foreach (var batch in batches)
            {
                using var command = new SqlCommand(batch, connection) { CommandTimeout = 120 };
                command.ExecuteNonQuery();
            }
        }

        public static int Scalar(string sql)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            return Convert.ToInt32(command.ExecuteScalar());
        }

        public static void Exec(string sql)
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        // Removes everything the tests created (all tagged with "it-").
        public static void Cleanup()
        {
            Exec(@"
                DELETE s FROM dbo.PowerAcKpiComponentScore s JOIN dbo.PowerAcKpiResult r ON r.Id = s.KpiResultId WHERE r.CalculatedBy LIKE 'it-%';
                DELETE FROM dbo.PowerAcKpiResult WHERE CalculatedBy LIKE 'it-%';
                DELETE FROM dbo.PowerAcComponentResult WHERE CreatedBy LIKE 'it-%';
                DELETE FROM dbo.PowerAcAreaAllocation WHERE CreatedBy LIKE 'it-%';
                DELETE FROM dbo.PowerAcComponentWeight WHERE CreatedBy LIKE 'it-%';
                DELETE FROM dbo.PowerAcComponentVersion WHERE CreatedBy LIKE 'it-%';
                DELETE FROM dbo.PowerAcEngineerGroup WHERE Code LIKE 'IT-%';
                DELETE FROM dbo.PowerAcAuditLog WHERE ChangedBy LIKE 'it-%';");
        }

        public void Dispose()
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString)) Cleanup();
        }
    }

    [CollectionDefinition("PowerAcSqlServer", DisableParallelization = true)]
    public class PowerAcSqlCollection : ICollectionFixture<PowerAcSqlFixture> { }

    [Collection("PowerAcSqlServer")]
    public class PowerAcSqlServerTests
    {
        private readonly PowerAcSqlFixture _fixture;
        private static readonly System.Security.Claims.ClaimsPrincipal Admin = PowerAcTestHost.User("Admin", 1, "it-admin");

        public PowerAcSqlServerTests(PowerAcSqlFixture fixture) => _fixture = fixture;

        private static PowerAcTestHost Host() => new(PowerAcSqlFixture.ConnectionString);

        private static PowerAcPeriod Month(int m) => PowerAcPeriod.TryCreate(2026, "Month", m, out var p, out _) ? p! : throw new Exception();

        [SqlServerFact]
        public void Deployment_scripts_are_idempotent_and_create_all_objects()
        {
            Assert.Equal(2, _fixture.ScriptRuns.Count);
            PowerAcSqlFixture.RunScript("001_power_ac_kpi_deploy.sql");
            PowerAcSqlFixture.RunScript("003_power_ac_versions_audit_deploy.sql");
            Assert.Equal(11, PowerAcSqlFixture.Scalar("SELECT COUNT(*) FROM sys.tables WHERE name LIKE 'PowerAc%'"));
            Assert.Equal(20, PowerAcSqlFixture.Scalar("SELECT COUNT(*) FROM dbo.PowerAcEngineerGroup"));
            Assert.Equal(4, PowerAcSqlFixture.Scalar("SELECT COUNT(*) FROM dbo.PowerAcComponentVersion WHERE EffectiveYear = 2026 AND EffectiveMonth = 1"));
            // Deployment never approves a scoring method.
            Assert.Equal(0, PowerAcSqlFixture.Scalar("SELECT COUNT(*) FROM dbo.PowerAcComponentVersion WHERE ScoringApproved = 1 AND CreatedBy LIKE 'deploy-%'"));
            Assert.Equal(0, PowerAcSqlFixture.Scalar("SELECT COUNT(*) FROM dbo.PowerAcCalculationSetting WHERE PointsMethodApproved = 1 AND CreatedBy LIKE 'deploy-%'"));
            Assert.Equal(0, PowerAcSqlFixture.Scalar("SELECT COUNT(*) FROM dbo.PowerAcAreaAllocation WHERE CreatedBy LIKE 'deploy-%'"));
            Assert.Equal(1, PowerAcSqlFixture.Scalar("SELECT COUNT(*) FROM dbo.PowerAcEngineerGroup WHERE MappingVerified = 0 AND WorkbookAllocationCell = 'D19'"));
        }

        [SqlServerFact]
        public void Check_constraints_reject_invalid_values()
        {
            try
            {
                var group = PowerAcSqlFixture.Scalar("SELECT MIN(Id) FROM dbo.PowerAcEngineerGroup");
                var component = PowerAcSqlFixture.Scalar("SELECT Id FROM dbo.PowerAcComponent WHERE Code = 'CORRECTIVE'");
                var ex1 = Assert.Throws<SqlException>(() => PowerAcSqlFixture.Exec(
                    $"INSERT dbo.PowerAcAreaAllocation (EngineerGroupId, EffectiveYear, EffectiveMonth, AllocationValue, CreatedBy) VALUES ({group}, 2026, 1, -1, 'it-x')"));
                Assert.Equal(547, ex1.Number);
                var ex2 = Assert.Throws<SqlException>(() => PowerAcSqlFixture.Exec(
                    $"INSERT dbo.PowerAcComponentResult (EngineerGroupId, Year, Month, ComponentId, AchievementPercent, Status, CreatedBy) VALUES ({group}, 2026, 1, {component}, 101, 'Draft', 'it-x')"));
                Assert.Equal(547, ex2.Number);
                var ex3 = Assert.Throws<SqlException>(() => PowerAcSqlFixture.Exec(
                    $"INSERT dbo.PowerAcComponentResult (EngineerGroupId, Year, Month, ComponentId, Status, CreatedBy) VALUES ({group}, 2026, 13, {component}, 'Draft', 'it-x')"));
                Assert.Equal(547, ex3.Number);
                var ex4 = Assert.Throws<SqlException>(() => PowerAcSqlFixture.Exec(
                    $"INSERT dbo.PowerAcComponentResult (EngineerGroupId, Year, Month, ComponentId, Status, CreatedBy) VALUES ({group}, 2026, 1, {component}, 'Approved', 'it-x')"));
                Assert.Equal(547, ex4.Number);
                // A Pending method cannot be approved, and approval must record who and when.
                var ex5 = Assert.Throws<SqlException>(() => PowerAcSqlFixture.Exec(
                    $"INSERT dbo.PowerAcComponentVersion (ComponentId, EffectiveYear, EffectiveMonth, TargetValue, ScoringMethod, ScoringApproved, ApprovedBy, ApprovedAt, CreatedBy) VALUES ({component}, 2099, 1, 0, 'Pending', 1, 'x', SYSUTCDATETIME(), 'it-x')"));
                Assert.Equal(547, ex5.Number);
                var ex6 = Assert.Throws<SqlException>(() => PowerAcSqlFixture.Exec(
                    $"INSERT dbo.PowerAcComponentVersion (ComponentId, EffectiveYear, EffectiveMonth, TargetValue, ScoringMethod, ScoringApproved, CreatedBy) VALUES ({component}, 2099, 1, 100, 'RatioCapped', 1, 'it-x')"));
                Assert.Equal(547, ex6.Number);
            }
            finally { PowerAcSqlFixture.Cleanup(); }
        }

        [SqlServerFact]
        public async Task Unique_keys_and_filtered_indexes_prevent_duplicates()
        {
            try
            {
                using var host = Host();
                var groupId = await host.Db.PowerAcEngineerGroups.Where(g => g.Code == "CPN").Select(g => g.Id).SingleAsync();
                host.Db.PowerAcAreaAllocations.Add(new PowerAcAreaAllocation { EngineerGroupId = groupId, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 1, CreatedBy = "it-a", CreatedAt = DateTime.UtcNow });
                await host.Db.SaveChangesAsync();
                host.Db.PowerAcAreaAllocations.Add(new PowerAcAreaAllocation { EngineerGroupId = groupId, EffectiveYear = 2026, EffectiveMonth = 1, AllocationValue = 2, CreatedBy = "it-a", CreatedAt = DateTime.UtcNow });
                var dup = await Assert.ThrowsAsync<DbUpdateException>(() => host.Db.SaveChangesAsync());
                Assert.Contains((dup.InnerException as SqlException)!.Number, new[] { 2601, 2627 });

                // Filtered unique index: two groups may both be unmapped, but not share a regiondata row.
                PowerAcSqlFixture.Exec("INSERT dbo.PowerAcEngineerGroup (Code, WorkbookLabel, WorkbookAllocationCell) VALUES ('IT-1', 'x', 'IT1'), ('IT-2', 'x', 'IT2')");
                var regionId = PowerAcSqlFixture.Scalar("SELECT RegionDataId FROM dbo.PowerAcEngineerGroup WHERE Code = 'CPN'");
                var ex = Assert.Throws<SqlException>(() => PowerAcSqlFixture.Exec($"UPDATE dbo.PowerAcEngineerGroup SET RegionDataId = {regionId} WHERE Code = 'IT-1'"));
                Assert.Contains(ex.Number, new[] { 2601, 2627 });
            }
            finally { PowerAcSqlFixture.Cleanup(); }
        }

        [SqlServerFact]
        public async Task Decimal_precision_round_trips()
        {
            try
            {
                using (var host = Host())
                {
                    var groupId = await host.Db.PowerAcEngineerGroups.Where(g => g.Code == "SPE").Select(g => g.Id).SingleAsync();
                    var componentId = await host.Db.PowerAcComponents.Where(c => c.Code == "PM").Select(c => c.Id).SingleAsync();
                    host.Db.PowerAcAreaAllocations.Add(new PowerAcAreaAllocation { EngineerGroupId = groupId, EffectiveYear = 2098, EffectiveMonth = 12, AllocationValue = 123456.7891m, CreatedBy = "it-p", CreatedAt = DateTime.UtcNow });
                    host.Db.PowerAcComponentWeights.Add(new PowerAcComponentWeight { ComponentId = componentId, EffectiveYear = 2098, EffectiveMonth = 12, WeightPercent = 33.3333m, CreatedBy = "it-p", CreatedAt = DateTime.UtcNow });
                    await host.Db.SaveChangesAsync();
                }
                using var fresh = Host();
                Assert.Equal(123456.7891m, (await fresh.Db.PowerAcAreaAllocations.SingleAsync(a => a.CreatedBy == "it-p")).AllocationValue);
                Assert.Equal(33.3333m, (await fresh.Db.PowerAcComponentWeights.SingleAsync(w => w.CreatedBy == "it-p")).WeightPercent);
            }
            finally { PowerAcSqlFixture.Cleanup(); }
        }

        [SqlServerFact]
        public async Task Calculation_on_real_data_is_transactional_and_never_scores_without_inputs()
        {
            try
            {
                using var host = Host();
                var kpi = PowerAcTestHost.WithUser(new PowerAcKpiController(host.Db, host.Get<PowerAcAccessService>(), host.Get<PowerAcKpiCalculationService>()), Admin);

                // Real 2026 PM data, no allocations, no approvals: every group must be explicitly not score-ready.
                var readiness = Assert.IsType<PowerAcReadinessReport>(Assert.IsType<OkObjectResult>((await kpi.Readiness(2026, "Month", 6)).Result).Value);
                Assert.Equal(20, readiness.GroupCount);
                Assert.Equal(0, readiness.StateCounts[PowerAcReadinessStates.ScoreReady]);
                Assert.Equal(0, readiness.ApprovedComponentCount);

                await kpi.Calculate(2026, "Month", 6);
                await kpi.Calculate(2026, "Month", 6); // replaces, never duplicates
                var rows = await host.Db.PowerAcKpiResults.Include(r => r.ComponentScores)
                    .Where(r => r.Year == 2026 && r.PeriodType == "Month" && r.PeriodNumber == 6).ToListAsync();
                Assert.Equal(20, rows.Count);
                Assert.All(rows, r => Assert.NotEqual(PowerAcResultStatus.Complete, r.Status));
                Assert.All(rows, r => Assert.Null(r.PointsAchieved));
                Assert.All(rows, r => Assert.NotNull(r.CalculationSettingId));
                Assert.All(rows.SelectMany(r => r.ComponentScores), s => Assert.NotNull(s.ComponentVersionId));

                // Saved results read back through the API on SQL Server, with the snapshot's weights.
                var saved = Assert.IsType<PowerAcKpiResultsDto>(Assert.IsType<OkObjectResult>((await kpi.GetResults(2026, "Month", 6)).Result).Value);
                Assert.Equal(20, saved.Groups.Count);
                Assert.Equal(100m, saved.WeightTotal);
                Assert.Contains(saved.GlobalIssues, i => i.Contains("Area allocation is missing"));

                // Config for months covered by the snapshot is now locked.
                var config = PowerAcTestHost.WithUser(new PowerAcConfigController(host.Db, host.Get<PowerAcAccessService>()), Admin);
                var componentId = await host.Db.PowerAcComponents.Where(c => c.Code == "PM").Select(c => c.Id).SingleAsync();
                var locked = await config.UpdateComponent(componentId, new UpdatePowerAcComponentDto { EffectiveYear = 2026, EffectiveMonth = 6, TargetValue = 100, ScoringMethod = "RatioCapped", ScoringApproved = false, IsActive = true, SortOrder = 1 });
                Assert.IsType<ConflictObjectResult>(locked.Result);

                Assert.True(await host.Db.PowerAcAuditLogs.CountAsync(a => a.EntityType == PowerAcAuditEntities.KpiCalculation && a.ChangedBy == "it-admin") >= 2);
            }
            finally { PowerAcSqlFixture.Cleanup(); }
        }

        [SqlServerFact]
        public async Task Workflow_and_audit_trail_on_sql_server()
        {
            try
            {
                using var host = Host();
                var groupId = await host.Db.PowerAcEngineerGroups.Where(g => g.Code == "CPN").Select(g => g.Id).SingleAsync();
                var safetyId = await host.Db.PowerAcComponents.Where(c => c.Code == "SAFETY").Select(c => c.Id).SingleAsync();
                var access = host.Get<PowerAcAccessService>();
                // The entering user is a SuperAdmin (a verifier), so the self-verification rule itself is exercised.
                var entry = PowerAcTestHost.WithUser(new PowerAcDataController(host.Db, access), PowerAcTestHost.User("SuperAdmin", 1, "it-entry"));
                var verifier = PowerAcTestHost.WithUser(new PowerAcDataController(host.Db, access), PowerAcTestHost.User("SuperAdmin", 2, "it-super"));

                var saved = await entry.SaveComponentResult(new SavePowerAcComponentResultDto { EngineerGroupId = groupId, Year = 2026, Month = 3, ComponentId = safetyId, IncidentCount = 0, Remarks = "it" });
                var id = Assert.IsType<PowerAcComponentResultDto>(Assert.IsType<OkObjectResult>(saved.Result).Value).Id;
                await entry.Submit(id);
                Assert.IsType<ConflictObjectResult>((await entry.Verify(id, new VerifyPowerAcComponentResultDto { Approve = true })).Result);
                Assert.IsType<OkObjectResult>((await verifier.Verify(id, new VerifyPowerAcComponentResultDto { Approve = true })).Result);

                var history = Assert.IsType<List<PowerAcAuditEntryDto>>(Assert.IsType<OkObjectResult>((await entry.GetHistory(id)).Result).Value);
                Assert.Equal(new[] { "Created", "Submitted", "Verified" }, history.Select(h => h.Action).ToArray());
                Assert.Equal(new[] { "it-entry", "it-entry", "it-super" }, history.Select(h => h.ChangedBy).ToArray());

                // Allocation entry for all 20 groups through the API on SQL Server.
                var groups = await host.Db.PowerAcEngineerGroups.Where(g => !g.Code.StartsWith("IT-")).Select(g => g.Id).ToListAsync();
                var allocation = await entry.SaveAllocations(new SavePowerAcAllocationsDto
                {
                    EffectiveYear = 2097, EffectiveMonth = 1,
                    Items = groups.Select((g, i) => new PowerAcAllocationItemDto { EngineerGroupId = g, AllocationValue = i + 1 }).ToList()
                });
                Assert.IsType<OkObjectResult>(allocation);
                var summary = Assert.IsType<PowerAcAllocationSummaryDto>(Assert.IsType<OkObjectResult>((await entry.GetAllocations(2097, 1)).Result).Value);
                Assert.True(summary.IsComplete);
                Assert.Equal(210m, summary.Total);
            }
            finally { PowerAcSqlFixture.Cleanup(); }
        }
    }
}
