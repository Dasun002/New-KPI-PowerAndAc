/*
 * File: PlatformKpiEditRequirement.cs
 * Implements custom authorization requirement and handler for Platform KPI editing.
 * Enforces multiple conditions: role, date window, and page assignment.
 */

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Helpers.Authorization
{
    // =========================================================
    // PLATFORM KPI EDIT REQUIREMENT
    // Authorization requirement for editing Platform KPI data
    // =========================================================
    public class PlatformKpiEditRequirement : IAuthorizationRequirement
    {
        // Marker interface for Platform KPI edit authorization policy
    }

    // =========================================================
    // PLATFORM KPI EDIT HANDLER
    // Authorization handler with multiple checks for KPI edit access
    // =========================================================
    public class PlatformKpiEditHandler : AuthorizationHandler<PlatformKpiEditRequirement>
    {
        // HTTP context accessor for extracting page IDs from requests
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PlatformKpiEditHandler(IHttpContextAccessor httpContextAccessor, IDateHelper dateHelper)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Handles Platform KPI edit authorization with multiple conditions
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformKpiEditRequirement requirement)
        {
            var user = context.User;
            if (!user.Identity?.IsAuthenticated ?? true) return Task.CompletedTask;

            // Check 1: Role must be PlatformAdmin
            bool isPlatformAdmin = user.HasClaim(c =>
                (c.Type == "role" || c.Type == ClaimTypes.Role) &&
                string.Equals(c.Value, "PlatformAdmin", StringComparison.OrdinalIgnoreCase));

            if (!isPlatformAdmin)
            {
                return Task.CompletedTask;
            }

            // Check 2: User must have page assignment (assigned KPI pages or allowed pages)
            var httpContext = _httpContextAccessor.HttpContext;
            
            object? pageIdObj = null;
            // Try to get pageId from route values
            if (httpContext?.Request.RouteValues.TryGetValue("pageId", out pageIdObj) != true)
            {
                // Try to get pageId from query parameters
                if (httpContext?.Request.Query.ContainsKey("pageId") == true)
                {
                    pageIdObj = httpContext.Request.Query["pageId"];
                }
            }

            // Check if Resource is passed manually (e.g. AuthorizeAsync(User, pageId, policy))
            if (pageIdObj == null && context.Resource is int resourceInt)
            {
                pageIdObj = resourceInt;
            }
            if (pageIdObj == null && context.Resource is string resourceString)
            {
                pageIdObj = resourceString;
            }

            // Verify user has assignment to the requested page
            if (pageIdObj != null && int.TryParse(pageIdObj.ToString(), out int pageId))
            {
                // Get user's assigned KPI pages and allowed pages
                var assignedPages = user.FindAll("assignedKpiPages").Select(c => c.Value).ToList();
                var allowedPages = user.FindAll("allowedPages").Select(c => c.Value).ToList();

                // User can edit if page is either assigned for KPI or in allowed pages.
                // NOTE: Some claims may be emitted as strings; keep comparison exact.
                if (assignedPages.Contains(pageId.ToString()) || allowedPages.Contains(pageId.ToString()))
                {
                    context.Succeed(requirement);
                }
            }
            else
            {
                // If we cannot resolve a page id from route/query/resource,
                // fall back to denying (safer default).
                // context.Succeed is NOT called.
            }


            return Task.CompletedTask;
        }
    }
}
