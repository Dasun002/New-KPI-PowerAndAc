using System.Security.Claims;
using backend.Data;
using backend.Helpers;
using backend.Helpers.Authorization;
using backend.Models;
using backend.Models.PowerAc;
using backend.Services.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests.PowerAc
{
    // In-memory database plus the real authorization policies and handlers from Program.cs.
    public sealed class PowerAcTestHost : IDisposable
    {
        public ServiceProvider Services { get; }
        public AppDbContext Db { get; }

        // sqlServerConnection: null = isolated in-memory database; otherwise a real SQL Server test database.
        public PowerAcTestHost(string? sqlServerConnection = null)
        {
            var services = new ServiceCollection();
            var dbName = Guid.NewGuid().ToString();
            services.AddLogging();
            if (sqlServerConnection is null)
                services.AddDbContext<AppDbContext>(o => o
                    .UseInMemoryDatabase(dbName)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
            else
                services.AddDbContext<AppDbContext>(o => o.UseSqlServer(sqlServerConnection));
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IDateHelper, DateHelper>();
            services.AddScoped<IAuthorizationHandler, PageAccessHandler>();
            services.AddScoped<IAuthorizationHandler, PlatformKpiEditHandler>();
            services.AddAuthorization(options =>
            {
                options.AddPolicy("SuperAdminOnly", p => p.RequireClaim("role", "SuperAdmin"));
                options.AddPolicy("AdminOnly", p => p.RequireClaim("role", "Admin", "SuperAdmin"));
                options.AddPolicy("PlatformAdminOnly", p => p.RequireClaim("role", "PlatformAdmin", "SuperAdmin"));
                options.AddPolicy("ViewPagePolicy", p => p.AddRequirements(new PageAccessRequirement()));
                options.AddPolicy("EditPlatformKpiPolicy", p => p.AddRequirements(new PlatformKpiEditRequirement()));
            });
            services.AddScoped<PowerAcAccessService>();
            services.AddScoped<PowerAcKpiCalculationService>();
            Services = services.BuildServiceProvider();
            Db = Services.GetRequiredService<AppDbContext>();
        }

        public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

        public static ClaimsPrincipal User(string role, int userId, string serviceId, params int[] assignedPages)
        {
            var claims = new List<Claim>
            {
                new("role", role),
                new(ClaimTypes.Role, role),
                new("UserId", userId.ToString()),
                new("serviceId", serviceId)
            };
            claims.AddRange(assignedPages.Select(p => new Claim("assignedKpiPages", p.ToString())));
            return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        }

        public static T WithUser<T>(T controller, ClaimsPrincipal user) where T : ControllerBase
        {
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
            return controller;
        }

        // Seeds 2 engineer groups, the 4 components (all approved), 25% weights from 2026-01,
        // settings from 2026-01 and KPI definition 2017 with 5000 points.
        public void SeedBasics()
        {
            Db.KpiDefinitions.Add(new KpiDefinition { Id = 2017, KeyPerformanceIndicators = "O&M of Power & Aircondition", PointsApplicable = 5000 });
            Db.RegionData.AddRange(
                new RegionData { Id = 1, Region = "metro", Province = "metro 1", NetworkEngineer = "WPC-1", LeaCode = "HK" },
                new RegionData { Id = 2, Region = "metro", Province = "metro 1", NetworkEngineer = "WPC-2", LeaCode = "CEN/MD" });
            Db.PowerAcEngineerGroups.AddRange(
                new PowerAcEngineerGroup { Id = 1, Code = "WPC-1", WorkbookLabel = "E/NW/WPC1", WorkbookAllocationCell = "D31", RegionDataId = 1, PmDesignation = "NW/WPC-1", MappingVerified = true, SortOrder = 1 },
                new PowerAcEngineerGroup { Id = 2, Code = "WPC-2", WorkbookLabel = "E/NW/WPC2", WorkbookAllocationCell = "D32", RegionDataId = 2, PmDesignation = "NW/WPC-2", MappingVerified = true, SortOrder = 2 });
            Db.PowerAcComponents.AddRange(
                new PowerAcComponent { Id = 1, Code = "PM", Name = "Preventive Maintenance", TargetValue = 100, DataSource = PowerAcDataSources.PowerAndAC, ScoringMethod = PowerAcScoringMethods.RatioCapped, ScoringApproved = true, SortOrder = 1 },
                new PowerAcComponent { Id = 2, Code = "SAFETY", Name = "Critical Safety", TargetValue = 0, TargetUnit = "Incidents", DataSource = PowerAcDataSources.ComponentResult, ScoringMethod = PowerAcScoringMethods.ZeroIncidentsBinary, ScoringApproved = true, SortOrder = 2 },
                new PowerAcComponent { Id = 3, Code = "CORRECTIVE", Name = "Corrective Action and Maintenance Record", TargetValue = 100, DataSource = PowerAcDataSources.ComponentResult, ScoringMethod = PowerAcScoringMethods.RatioCapped, ScoringApproved = true, SortOrder = 3 },
                new PowerAcComponent { Id = 4, Code = "AVAILABILITY", Name = "Power System Availability", TargetValue = 99, DataSource = PowerAcDataSources.ComponentResult, ScoringMethod = PowerAcScoringMethods.DirectPercent, ScoringApproved = true, SortOrder = 4 });
            foreach (var id in new[] { 1, 2, 3, 4 })
                Db.PowerAcComponentWeights.Add(new PowerAcComponentWeight { ComponentId = id, EffectiveYear = 2026, EffectiveMonth = 1, WeightPercent = 25 });
            // Approved scoring versions effective January 2026 (test data only; production starts unapproved).
            Db.PowerAcComponentVersions.AddRange(
                new PowerAcComponentVersion { ComponentId = 1, EffectiveYear = 2026, EffectiveMonth = 1, TargetValue = 100, ScoringMethod = PowerAcScoringMethods.RatioCapped, ScoringApproved = true, ApprovedBy = "test", ApprovedAt = DateTime.UtcNow },
                new PowerAcComponentVersion { ComponentId = 2, EffectiveYear = 2026, EffectiveMonth = 1, TargetValue = 0, ScoringMethod = PowerAcScoringMethods.ZeroIncidentsBinary, ScoringApproved = true, ApprovedBy = "test", ApprovedAt = DateTime.UtcNow },
                new PowerAcComponentVersion { ComponentId = 3, EffectiveYear = 2026, EffectiveMonth = 1, TargetValue = 100, ScoringMethod = PowerAcScoringMethods.RatioCapped, ScoringApproved = true, ApprovedBy = "test", ApprovedAt = DateTime.UtcNow },
                new PowerAcComponentVersion { ComponentId = 4, EffectiveYear = 2026, EffectiveMonth = 1, TargetValue = 99, ScoringMethod = PowerAcScoringMethods.DirectPercent, ScoringApproved = true, ApprovedBy = "test", ApprovedAt = DateTime.UtcNow });
            Db.PowerAcCalculationSettings.Add(new PowerAcCalculationSetting
            {
                EffectiveYear = 2026, EffectiveMonth = 1, KpiDefinitionId = 2017,
                PointsMethod = PowerAcPointsMethods.TargetScaled, CompositeTarget = 1, PointsMethodApproved = true, RequireVerifiedData = true
            });
            Db.SaveChanges();
        }

        // The 20 workbook rows 'Power & AC'!D19:D38 as engineer groups 101..120 (mapping verified).
        public static readonly string[] WorkbookCodes =
        {
            "EP", "CPN", "CPS", "NCP", "NP-2", "NP-1", "NWPE", "NWPW", "SAB", "SPE",
            "SPW", "UVA", "WPC-1", "WPC-2", "WPE", "WPN", "WPNE", "WPS", "WPSE", "WPSW"
        };

        public void SeedTwentyGroups()
        {
            for (var i = 0; i < WorkbookCodes.Length; i++)
            {
                var id = 101 + i;
                Db.RegionData.Add(new RegionData { Id = id, Region = "R", Province = "P", NetworkEngineer = WorkbookCodes[i], LeaCode = "L" + id });
                Db.PowerAcEngineerGroups.Add(new PowerAcEngineerGroup
                {
                    Id = id, Code = WorkbookCodes[i] + "-T", WorkbookLabel = "E/NW/" + WorkbookCodes[i],
                    WorkbookAllocationCell = "D" + (19 + i), RegionDataId = id, PmDesignation = "NW/T" + WorkbookCodes[i],
                    MappingVerified = true, SortOrder = 100 + i
                });
            }
            Db.SaveChanges();
        }

        // Verified data for one month for both groups: PM 9/10, 0 incidents, corrective 8/10, availability 100%.
        public void SeedMonth(int month)
        {
            foreach (var (groupId, designation) in new[] { (1, "NW/WPC-1"), (2, "NW/WPC-2") })
            {
                Db.PowerAndAC.Add(new PowerAndAC { Designation = designation, Year = 2026, Month = month, Scheduled = 10, Attended = 9, Cumulative_Sched = 10, Cumulative_Achieved = 9, IsVerified = true });
                Db.PowerAcComponentResults.AddRange(
                    new PowerAcComponentResult { EngineerGroupId = groupId, Year = 2026, Month = (byte)month, ComponentId = 2, IncidentCount = 0, Status = PowerAcRecordStatus.Verified },
                    new PowerAcComponentResult { EngineerGroupId = groupId, Year = 2026, Month = (byte)month, ComponentId = 3, ApplicableCount = 10, CompletedCount = 8, Status = PowerAcRecordStatus.Verified },
                    new PowerAcComponentResult { EngineerGroupId = groupId, Year = 2026, Month = (byte)month, ComponentId = 4, AchievementPercent = 100, Status = PowerAcRecordStatus.Verified });
            }
            Db.SaveChanges();
        }

        public void Dispose() => Services.Dispose();
    }
}
