/*
 * File: PageAccessRequirement.cs
 * Implements custom authorization requirement and handler for page-level access control.
 */

using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Helpers.Authorization
{
    // =========================================================
    // PAGE ACCESS REQUIREMENT
    // Authorization requirement for page-level access control
    // =========================================================
    public class PageAccessRequirement : IAuthorizationRequirement
    {
        // Marker interface for page access authorization policy
    }

    // =========================================================
    // PAGE ACCESS HANDLER
    // Authorization handler that checks user's page access claims
    // =========================================================
    public class PageAccessHandler : AuthorizationHandler<PageAccessRequirement>
    {
        // HTTP context accessor for extracting page IDs from requests
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PageAccessHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Handles page access authorization
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PageAccessRequirement requirement)
        {
            // Check if user is authenticated
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                return Task.CompletedTask;
            }

            // Helper function to check if user has a specific role (case-insensitive)
            bool IsRole(string roleName) => context.User
                .FindAll(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                .Any(c => string.Equals(NormalizeRole(c.Value), NormalizeRole(roleName), StringComparison.OrdinalIgnoreCase));

            // SuperAdmin has access to everything
            if (IsRole("SuperAdmin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // PlatformAdmin can view all pages (edit is restricted by EditPlatformKpiPolicy)
            if (IsRole("PlatformAdmin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Admin and User can view all Platform KPI pages (view-only)
            if (IsRole("Admin") || IsRole("User"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Extract PageId from Route or Query parameters
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return Task.CompletedTask;

            object? pageIdObj = null;
            if (httpContext.Request.RouteValues.TryGetValue("pageId", out pageIdObj) != true)
            {
                if (httpContext.Request.Query.ContainsKey("pageId"))
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

            // Verify user has access to the requested page
            if (pageIdObj != null && int.TryParse(pageIdObj.ToString(), out int pageId))
            {
                // Check if user has "allowedPages" claim for this pageId
                var allowedPages = context.User.FindAll("allowedPages").Select(c => c.Value).ToList();
                
                if (allowedPages.Contains(pageId.ToString()))
                {
                    context.Succeed(requirement);
                }
            }
            else
            {
                // If no pageId is specified, cannot enforce page access policy
            }

            return Task.CompletedTask;
        }

        // Normalizes role names by removing spaces and converting to lowercase
        private static string NormalizeRole(string? value)
        {
            return (value ?? string.Empty)
                .Replace(" ", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }
    }
}
