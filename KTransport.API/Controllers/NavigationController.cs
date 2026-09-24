using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NavigationController : ControllerBase
    {
        private readonly INavigationService _navService;
        private readonly ITenantContext _tenantContext;
        private readonly KTransportDbContext _context;
        private readonly IFeatureAuthorizationService _authService;

        public NavigationController(
            INavigationService navService, 
            ITenantContext tenantContext,
            KTransportDbContext context,
            IFeatureAuthorizationService authService)
        {
            _navService = navService;
            _tenantContext = tenantContext;
            _context = context;
            _authService = authService;
        }

        /// <summary>
        /// Automated Acceptance Test Runner for SaaS Authorization & Multi-User Hierarchy.
        /// </summary>
        [HttpGet("test-scenarios")]
        [AllowAnonymous]
        public async Task<IActionResult> RunTestScenarios()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var results = new List<object>();

            // 1. Super User Principal
            var superUserClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Role, "SUPER_USER"),
                new Claim("tenant_id", tenantId.ToString())
            }, "TestAuth"));

            // 2. Sub User (Nitesh) Principal
            var subUserClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "2"),
                new Claim(ClaimTypes.Name, "nitesh"),
                new Claim(ClaimTypes.Role, "SUB_USER"),
                new Claim("tenant_id", tenantId.ToString())
            }, "TestAuth"));

            // Test 1: Super User accessing SAAS_CONFIGURATION
            var t1 = await _authService.AuthorizeFeatureAsync(superUserClaims, tenantId, FeatureConstants.SAAS_CONFIGURATION);
            results.Add(new { test = "Test 1: Super User -> SAAS_CONFIGURATION", passed = t1.IsAuthorized, status = t1.StatusCode, reason = t1.FailureReason });

            // Test 2: Sub User (Nitesh) accessing SAAS_CONFIGURATION
            var t2 = await _authService.AuthorizeFeatureAsync(subUserClaims, tenantId, FeatureConstants.SAAS_CONFIGURATION);
            results.Add(new { test = "Test 2: Sub User (Nitesh) -> SAAS_CONFIGURATION", passed = !t2.IsAuthorized && t2.StatusCode == 403, status = t2.StatusCode, reason = t2.FailureReason });

            // Test 3: Sub User (Nitesh) accessing GOOD_RECEIPT (assigned)
            var t3 = await _authService.AuthorizeFeatureAsync(subUserClaims, tenantId, FeatureConstants.GOOD_RECEIPT);
            results.Add(new { test = "Test 3: Sub User (Nitesh) -> GOOD_RECEIPT (assigned)", passed = t3.IsAuthorized, status = t3.StatusCode, reason = t3.FailureReason });

            // Test 4: Dynamic menu for Super User contains SaaS Configuration
            var superMenu = await _navService.GetDynamicMenuAsync(tenantId, "SUPER_USER", "admin");
            bool superHasSettings = superMenu.Any(m => m.Id == "system" && m.Children != null && m.Children.Any(c => c.Id == "system.settings"));
            results.Add(new { test = "Test 4: Super User Menu contains SaaS Configuration", passed = superHasSettings });

            // Test 5: Dynamic menu for Sub User (Nitesh) DOES NOT contain SaaS Configuration or Tenant Admin
            var niteshMenu = await _navService.GetDynamicMenuAsync(tenantId, "SUB_USER", "nitesh");
            bool niteshHasSettings = niteshMenu.Any(m => m.Id == "system" || m.Id == "clients" || (m.Children != null && m.Children.Any(c => c.Id == "system.settings" || c.Id == "system.onboard")));
            results.Add(new { test = "Test 5: Sub User (Nitesh) Menu EXCLUDES SaaS Configuration & Clients", passed = !niteshHasSettings });

            // Test 6: Effective Access Intersection (Organization Subscription ∩ User Permissions)
            var effectiveFeatures = await _authService.GetEffectiveFeaturesForUserAsync(tenantId, "SUB_USER", "nitesh");
            bool niteshNoSettings = !effectiveFeatures.Contains(FeatureConstants.SAAS_CONFIGURATION);
            results.Add(new { test = "Test 6: Effective Access Intersection excludes SAAS_CONFIGURATION", passed = niteshNoSettings });

            return Ok(new
            {
                allPassed = results.All(r => (bool)((dynamic)r).passed),
                timestamp = DateTime.UtcNow,
                tenantId = tenantId,
                totalTests = results.Count,
                results = results
            });
        }

        /// <summary>
        /// Get the dynamic hierarchical menu configured for the active tenant and caller's role / user assignment.
        /// </summary>
        [HttpGet("menu")]
        [AllowAnonymous]
        public async Task<ActionResult<List<DynamicMenuItemDto>>> GetMenu()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var role = User.FindFirst(ClaimTypes.Role)?.Value 
                       ?? User.FindFirst("role")?.Value 
                       ?? "admin";
            var userIdOrName = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst(ClaimTypes.Name)?.Value
                               ?? User.FindFirst("sub")?.Value;

            var menu = await _navService.GetDynamicMenuAsync(tenantId, role, userIdOrName);
            return Ok(menu);
        }

        /// <summary>
        /// Get active permission keys for the caller (for UI element gating and route protection).
        /// </summary>
        [HttpGet("permissions")]
        [AllowAnonymous]
        public async Task<ActionResult<List<string>>> GetPermissions()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var role = User.FindFirst(ClaimTypes.Role)?.Value 
                       ?? User.FindFirst("role")?.Value 
                       ?? "admin";
            var userIdOrName = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst(ClaimTypes.Name)?.Value
                               ?? User.FindFirst("sub")?.Value;

            var permissions = await _navService.GetUserPermissionsAsync(tenantId, role, userIdOrName);
            return Ok(permissions);
        }

        /// <summary>
        /// Get all registered users for active tenant to configure dedicated page assignments (Super User only).
        /// </summary>
        [HttpGet("/api/configuration/users")]
        [Authorize]
        [RequireSuperUser]
        public async Task<IActionResult> GetTenantUsers()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var users = await _context.Users
                .Where(u => u.TenantId == tenantId)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.FullName,
                    u.Role,
                    u.Mobile,
                    u.IsActive
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return Ok(users);
        }

        /// <summary>
        /// Get all onboarded clients/tenants (for multi-tenant administration - Super User only).
        /// </summary>
        [HttpGet("/api/configuration/tenants")]
        [Authorize]
        [RequireSuperUser]
        public async Task<IActionResult> GetAllTenants()
        {
            var tenants = await _context.Tenants
                .IgnoreQueryFilters()
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.Code,
                    t.IsActive,
                    t.CreatedAt
                })
                .OrderBy(t => t.Name)
                .ToListAsync();

            return Ok(tenants);
        }

        /// <summary>
        /// Get the full menu, pages, and sub-reports entitlement catalog for current authenticated tenant (Super User only).
        /// </summary>
        [HttpGet("/api/configuration/menu-entitlements")]
        [Authorize]
        [RequireSuperUser]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> GetEntitlements()
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var entitlements = await _navService.GetTenantMenuEntitlementsAsync(tenantId);
            return Ok(entitlements);
        }

        /// <summary>
        /// Get menu, pages, and sub-reports entitlement catalog for a specific tenant ID (Super User only).
        /// </summary>
        [HttpGet("/api/configuration/tenants/{tenantId:guid}/menu-entitlements")]
        [Authorize]
        [RequireSuperUser]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> GetEntitlementsForTenant(Guid tenantId)
        {
            var entitlements = await _navService.GetTenantMenuEntitlementsAsync(tenantId);
            return Ok(entitlements);
        }

        /// <summary>
        /// Update menu, page, and report entitlements for the current tenant (Super User only).
        /// </summary>
        [HttpPut("/api/configuration/menu-entitlements")]
        [Authorize]
        [RequireSuperUser]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> UpdateEntitlements([FromBody] TenantMenuEntitlementsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var tenantId = _tenantContext.CurrentTenantId;
            var updated = await _navService.UpdateTenantMenuEntitlementsAsync(tenantId, dto);
            return Ok(updated);
        }

        /// <summary>
        /// Update menu, page, and report entitlements for a specific tenant ID (Super User only).
        /// </summary>
        [HttpPut("/api/configuration/tenants/{tenantId:guid}/menu-entitlements")]
        [Authorize]
        [RequireSuperUser]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> UpdateEntitlementsForTenant(Guid tenantId, [FromBody] TenantMenuEntitlementsDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updated = await _navService.UpdateTenantMenuEntitlementsAsync(tenantId, dto);
            return Ok(updated);
        }
    }
}
