using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TenantController : ControllerBase
    {
        private readonly ITenantService _tenantService;
        private readonly INavigationService _navService;
        private readonly ITenantContext _tenantContext;
        private readonly IEntitlementsService _entitlements;
        private readonly KTransportDbContext _db;

        public TenantController(ITenantService tenantService, INavigationService navService, ITenantContext tenantContext,
            IEntitlementsService entitlements, KTransportDbContext db)
        {
            _tenantService = tenantService;
            _navService = navService;
            _tenantContext = tenantContext;
            _entitlements = entitlements;
            _db = db;
        }

        // ---------------- §B per-tenant entitlement (platform-admin) ----------------

        /// <summary>§B: the tenant's current entitlement ceiling (modules + page restrictions + reports).</summary>
        [HttpGet("{id:guid}/entitlements")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<ActionResult<TenantMenuEntitlementsDto>> GetEntitlements(Guid id)
            => Ok(await _navService.GetTenantMenuEntitlementsAsync(id));

        /// <summary>§B: set the tenant's entitlement ceiling. 204 on success.</summary>
        [HttpPut("{id:guid}/entitlements")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> UpdateEntitlements(Guid id, [FromBody] TenantMenuEntitlementsDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            int? actingUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
            await _entitlements.WriteTenantEntitlementsAsync(id, dto, actingUserId);
            return NoContent();
        }

        /// <summary>§B: assign a plan — seeds the ceiling from plan_modules / plan_module_pages. 204 on success.</summary>
        [HttpPut("{id:guid}/plan")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> AssignPlan(Guid id, [FromBody] AssignPlanRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlanCode)) return BadRequest(new { success = false, message = "planCode is required." });
            var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Tier.ToLower() == request.PlanCode.ToLower());
            if (plan == null) return NotFound(new { success = false, message = $"Unknown plan '{request.PlanCode}'." });

            var moduleCodes = await (
                from pm in _db.PlanModules
                join m in _db.Modules.IgnoreQueryFilters() on pm.ModuleId equals m.Id
                where pm.PlanId == plan.Id select m.Code).ToListAsync();

            var pageRows = await (
                from pp in _db.PlanModulePages
                join m in _db.Modules.IgnoreQueryFilters() on pp.ModuleId equals m.Id
                where pp.PlanId == plan.Id select new { m.Code, pp.PageKey }).ToListAsync();
            var pageRestrictions = pageRows.GroupBy(x => x.Code)
                .ToDictionary(g => g.Key, g => g.Select(x => x.PageKey).ToList());

            int? actingUserId = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : null;
            await _entitlements.WriteTenantEntitlementsAsync(id, new TenantMenuEntitlementsDto
            {
                TenantId = id,
                ModuleCodes = moduleCodes,
                PageRestrictions = pageRestrictions
            }, actingUserId);

            await _tenantService.UpdateTenantSubscriptionPlanAsync(id, plan.Tier);
            return NoContent();
        }

        /// <summary>
        /// Current tenant's usage vs. plan limits. Any authenticated user of the tenant can
        /// call this so the frontend can show plan-warning banners without needing
        /// super-user access. Tenant scope comes from the JWT — never a query parameter.
        /// </summary>
        [HttpGet("usage")]
        [Authorize]
        public async Task<ActionResult<TenantUsageDto>> GetMyUsage()
        {
            var snapshot = await _tenantService.GetUsageSnapshotAsync(_tenantContext.CurrentTenantId);
            return Ok(snapshot);
        }

        /// <summary>
        /// Platform-operator view of any tenant's usage snapshot (TASK-009). Same shape as
        /// the tenant-self version above but keyed by the {id} route rather than the JWT.
        /// Restricted to platform admins so a tenant admin can't peek at another tenant's
        /// counts.
        /// </summary>
        [HttpGet("{id:guid}/usage")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<ActionResult<TenantUsageDto>> GetTenantUsage(Guid id)
        {
            var snapshot = await _tenantService.GetUsageSnapshotAsync(id);
            return Ok(snapshot);
        }

        /// <summary>
        /// Onboard a new tenant organization, its administrator, and initial module pack.
        /// </summary>
        [HttpPost("onboard")]
        [AllowAnonymous]
        [EnableRateLimiting("AuthPolicy")]
        public async Task<ActionResult<TenantOnboardingResponse>> OnboardTenant([FromBody] TenantOnboardingRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _tenantService.OnboardTenantAsync(request);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get all registered tenants with full SaaS details (Super User / System Admin only).
        /// </summary>
        [HttpGet]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> GetAllTenants()
        {
            var tenants = await _tenantService.GetAllTenantsWithDetailsAsync();
            return Ok(tenants);
        }

        /// <summary>
        /// Get tenant by ID (Super User only).
        /// </summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> GetTenantById(Guid id)
        {
            var tenant = await _tenantService.GetTenantByIdAsync(id);
            if (tenant == null)
            {
                return NotFound(new { message = "Tenant not found." });
            }

            return Ok(tenant);
        }

        /// <summary>
        /// Toggle tenant active/inactive status (Super User only).
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> UpdateTenantStatus(Guid id, [FromBody] TenantStatusUpdateDto dto)
        {
            var success = await _tenantService.UpdateTenantStatusAsync(id, dto.IsActive);
            if (!success)
            {
                return NotFound(new { message = "Tenant not found." });
            }

            return Ok(new { success = true, message = $"Tenant status updated to {(dto.IsActive ? "Active" : "Inactive")}." });
        }

        // RBAC clean-slate (contract §B): the per-tenant entitlement GET/PUT and the
        // plan-assignment PUT live at the top of this controller (GetEntitlements /
        // UpdateEntitlements / AssignPlan). The old TenantPlanUpdateDto/nav-based
        // variants were removed to avoid duplicate route mappings.

        /// <summary>
        /// List the users of a specific tenant (Platform operator only).
        /// </summary>
        [HttpGet("{id:guid}/users")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> GetTenantUsers(Guid id)
        {
            var users = await _tenantService.GetTenantUsersAsync(id);
            return Ok(users);
        }

        /// <summary>
        /// Set a tenant user's role — e.g. promote to admin or demote to standard user (Platform operator only).
        /// Used to assign/repair an organization's administrator.
        /// </summary>
        [HttpPut("{id:guid}/users/{userId:int}/role")]
        [Authorize]
        [RequirePlatformAdmin]
        public async Task<IActionResult> SetTenantUserRole(Guid id, int userId, [FromBody] SetUserRoleRequest dto)
        {
            var (success, message) = await _tenantService.SetTenantUserRoleAsync(id, userId, dto.Role);
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
    }
}
