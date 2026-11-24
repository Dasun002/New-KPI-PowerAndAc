using System.Security.Claims;
using backend.Controllers;
using backend.Data;
using backend.Models.PowerAc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.PowerAc
{
    public class PowerAcControllerTests
    {
        private const string AssignedServiceId = "30001";

        [Fact]
        public async Task Assigned_user_can_verify_own_monthly_data()
        {
            await using var db = CreateDatabase();
            db.PowerAcPmData.Add(new PowerAcPmData
            {
                Designation = "NW/EP",
                Year = "2026",
                Month = "November",
                Scheduled = 20,
                Attended = 18,
                EnteredBy = AssignedServiceId
            });
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.ToggleVerified(2026, 11, "NW/EP");

            Assert.IsType<OkObjectResult>(result);
            var row = await db.PowerAcPmData.SingleAsync();
            Assert.True(row.IsVerified);
            Assert.Equal(AssignedServiceId, row.VerifiedBy);
        }

        [Fact]
        public async Task Assigned_user_can_bulk_verify_own_kpi_data()
        {
            await using var db = CreateDatabase();
            db.PowerAcPmData.Add(new PowerAcPmData
            {
                Designation = "NW/EP",
                Year = "2026",
                Month = "November",
                PmAchievement = 90m,
                AreaAllocation = 100m,
                EnteredBy = AssignedServiceId
            });
            await db.SaveChangesAsync();
            var controller = CreateController(db);

            var result = await controller.Verify(2026, 11);

            Assert.IsType<OkObjectResult>(result);
            var row = await db.PowerAcPmData.SingleAsync();
            Assert.True(row.IsVerified);
            Assert.Equal(AssignedServiceId, row.VerifiedBy);
        }

        private static AppDbContext CreateDatabase()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private static PowerAcController CreateController(AppDbContext db)
        {
            var controller = new PowerAcController(db, new UnusedAuthorizationService());
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, AssignedServiceId),
                new Claim("serviceId", AssignedServiceId),
                new Claim("role", "PlatformAdmin"),
                new Claim("assignedKpiPages", "11")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            };
            return controller;
        }

        private sealed class UnusedAuthorizationService : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user,
                object? resource,
                IEnumerable<IAuthorizationRequirement> requirements) =>
                throw new NotImplementedException();

            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user,
                object? resource,
                string policyName) =>
                throw new NotImplementedException();
        }
    }
}
