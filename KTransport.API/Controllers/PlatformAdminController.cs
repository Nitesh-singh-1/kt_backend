using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services.Rbac;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// RBAC clean-slate (contract §C + §G): platform-admin plan editor and the
    /// tenant-traceability console. All actions require [RequirePlatformAdmin].
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize]
    [RequirePlatformAdmin]
    public class PlatformAdminController : ControllerBase
    {
        private readonly KTransportDbContext _db;

        public PlatformAdminController(KTransportDbContext db)
        {
            _db = db;
        }

        // ---------------- §C plans ----------------

        [HttpGet("plans")]
        public async Task<ActionResult<List<PlanDto>>> GetPlans()
        {
            var plans = await _db.SubscriptionPlans.OrderBy(p => p.MonthlyPrice).ToListAsync();
            var moduleCodeById = await _db.Modules.IgnoreQueryFilters().ToDictionaryAsync(m => m.Id, m => m.Code);

            var planModules = await _db.PlanModules.ToListAsync();
            var planPages = await _db.PlanModulePages.ToListAsync();

            var result = plans.Select(p => BuildPlanDto(p, planModules, planPages, moduleCodeById)).ToList();
            return Ok(result);
        }

        [HttpPut("plans/{code}")]
        public async Task<IActionResult> UpdatePlan(string code, [FromBody] PlanDto dto)
        {
            var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Tier.ToLower() == code.ToLower());
            if (plan == null) return NotFound(new { success = false, message = $"Unknown plan '{code}'." });

            if (!string.IsNullOrWhiteSpace(dto.Name)) plan.Name = dto.Name.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Description)) plan.Description = dto.Description!.Trim();
            plan.MonthlyPrice = dto.MonthlyPriceInr;
            if (dto.MaxUsers.HasValue) plan.MaxUsers = dto.MaxUsers.Value;
            if (dto.MaxVehicles.HasValue) plan.MaxVehicles = dto.MaxVehicles.Value;
            if (dto.MaxMonthlyShipments.HasValue) plan.MaxMonthlyShipments = dto.MaxMonthlyShipments.Value;
            if (dto.StorageLimitMb.HasValue) plan.StorageLimitMB = dto.StorageLimitMb.Value;
            plan.IsActive = dto.IsActive;

            var moduleIdByCode = await _db.Modules.IgnoreQueryFilters().ToDictionaryAsync(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);

            // Reconcile plan_modules to exactly dto.ModuleCodes.
            var desiredModuleIds = (dto.ModuleCodes ?? new List<string>())
                .Where(c => moduleIdByCode.ContainsKey(c)).Select(c => moduleIdByCode[c]).ToHashSet();
            var existingPlanModules = await _db.PlanModules.Where(pm => pm.PlanId == plan.Id).ToListAsync();
            _db.PlanModules.RemoveRange(existingPlanModules.Where(pm => !desiredModuleIds.Contains(pm.ModuleId)));
            foreach (var mid in desiredModuleIds.Where(id => !existingPlanModules.Any(pm => pm.ModuleId == id)))
                _db.PlanModules.Add(new PlanModule { PlanId = plan.Id, ModuleId = mid });

            // Reconcile plan_module_pages to dto.PageRestrictions.
            var existingPages = await _db.PlanModulePages.Where(pp => pp.PlanId == plan.Id).ToListAsync();
            _db.PlanModulePages.RemoveRange(existingPages);
            if (dto.PageRestrictions != null)
            {
                foreach (var kv in dto.PageRestrictions)
                {
                    if (!moduleIdByCode.TryGetValue(kv.Key, out var mid)) continue;
                    foreach (var pk in (kv.Value ?? new List<string>()).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
                        _db.PlanModulePages.Add(new PlanModulePage { PlanId = plan.Id, ModuleId = mid, PageKey = pk });
                }
            }

            await _db.SaveChangesAsync();
            return NoContent();
        }

        // ---------------- §G tenant console ----------------

        [HttpGet("tenants")]
        public async Task<ActionResult<List<TenantSummaryDto>>> GetTenants()
        {
            var tenants = await _db.Tenants.IgnoreQueryFilters().OrderBy(t => t.Name).ToListAsync();
            var tenantIds = tenants.Select(t => t.Id).ToList();

            var moduleCounts = (await _db.TenantModules.IgnoreQueryFilters()
                .Where(tm => tenantIds.Contains(tm.TenantId) && tm.EnabledUntil == null)
                .Select(tm => tm.TenantId).ToListAsync())
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            var userCounts = (await _db.Users.IgnoreQueryFilters()
                .Where(u => tenantIds.Contains(u.TenantId)).Select(u => u.TenantId).ToListAsync())
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            var planByTenant = await (
                from s in _db.TenantSubscriptions.IgnoreQueryFilters()
                join p in _db.SubscriptionPlans on s.SubscriptionPlanId equals p.Id
                where tenantIds.Contains(s.TenantId)
                select new { s.TenantId, p.Tier }).ToListAsync();
            var planMap = planByTenant.GroupBy(x => x.TenantId).ToDictionary(g => g.Key, g => g.First().Tier);

            var result = tenants.Select(t => new TenantSummaryDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                IsActive = t.IsActive,
                PlanCode = planMap.TryGetValue(t.Id, out var pc) ? pc : null,
                ModuleCount = moduleCounts.TryGetValue(t.Id, out var mc) ? mc : 0,
                UserCount = userCounts.TryGetValue(t.Id, out var uc) ? uc : 0,
                CreatedAt = t.CreatedAt
            }).ToList();
            return Ok(result);
        }

        [HttpGet("tenants/{id:guid}")]
        public async Task<ActionResult<TenantDetailDto>> GetTenantDetail(Guid id)
        {
            var tenant = await _db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound(new { success = false, message = "Tenant not found." });

            var modules = await (
                from tm in _db.TenantModules.IgnoreQueryFilters()
                join m in _db.Modules.IgnoreQueryFilters() on tm.ModuleId equals m.Id
                where tm.TenantId == id && tm.EnabledUntil == null
                select new { m.Id, m.Code, m.Name }).ToListAsync();

            var pageRestrictedModuleIds = (await _db.TenantModulePages.IgnoreQueryFilters()
                .Where(p => p.TenantId == id && p.EnabledUntil == null)
                .Select(p => p.ModuleId).ToListAsync()).ToHashSet();

            var roles = await _db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == id).ToListAsync();
            var userRoleCounts = (await _db.UserRoles.IgnoreQueryFilters()
                .Where(ur => ur.TenantId == id && ur.RevokedAt == null).Select(ur => ur.RoleId).ToListAsync())
                .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

            var users = await _db.Users.IgnoreQueryFilters().Where(u => u.TenantId == id).ToListAsync();
            var roleCodeByUser = (await (
                from ur in _db.UserRoles.IgnoreQueryFilters()
                join r in _db.Roles.IgnoreQueryFilters() on ur.RoleId equals r.Id
                where ur.TenantId == id && ur.RevokedAt == null
                select new { ur.UserId, r.Code }).ToListAsync())
                .GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Code).ToList());

            var planCode = await (
                from s in _db.TenantSubscriptions.IgnoreQueryFilters()
                join p in _db.SubscriptionPlans on s.SubscriptionPlanId equals p.Id
                where s.TenantId == id orderby s.StartedAt descending select p.Tier).FirstOrDefaultAsync();

            return Ok(new TenantDetailDto
            {
                Id = tenant.Id,
                Code = tenant.Code,
                Name = tenant.Name,
                IsActive = tenant.IsActive,
                PlanCode = planCode,
                Modules = modules.Select(m => new TenantDetailModuleDto { Code = m.Code, Name = m.Name, PageRestricted = pageRestrictedModuleIds.Contains(m.Id) }).ToList(),
                Roles = roles.Select(r => new TenantDetailRoleDto { Code = r.Code, Name = r.Name, IsSystem = r.IsSystem, UserCount = userRoleCounts.TryGetValue(r.Id, out var c) ? c : 0 }).ToList(),
                Users = users.Select(u => new TenantDetailUserDto
                {
                    Id = u.Id,
                    Username = u.Username,
                    FullName = u.FullName,
                    Email = u.Email,
                    IsActive = u.IsActive ?? true,
                    RoleCodes = roleCodeByUser.TryGetValue(u.Id, out var codes) ? codes : new List<string>()
                }).ToList()
            });
        }

        [HttpGet("tenants/{id:guid}/users")]
        public async Task<ActionResult<List<TenantDetailUserDto>>> GetTenantUsers(Guid id)
        {
            var detail = await GetTenantDetail(id);
            if (detail.Result is OkObjectResult ok && ok.Value is TenantDetailDto d) return Ok(d.Users);
            return detail.Result ?? NotFound();
        }

        [HttpPut("tenants/{id:guid}/users/{userId:int}/roles")]
        public async Task<IActionResult> SetUserRoles(Guid id, int userId, [FromBody] SetUserRolesRequest request)
        {
            var user = await _db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == id);
            if (user == null) return NotFound(new { success = false, message = "User not found in this tenant." });

            var desired = new HashSet<string>(request.RoleCodes ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            var tenantRoles = await _db.Roles.IgnoreQueryFilters().Where(r => r.TenantId == id).ToListAsync();
            var roleByCode = tenantRoles.ToDictionary(r => r.Code, r => r, StringComparer.OrdinalIgnoreCase);

            var activeAssignments = await _db.UserRoles.IgnoreQueryFilters()
                .Where(ur => ur.TenantId == id && ur.UserId == userId && ur.RevokedAt == null).ToListAsync();
            var activeRoleIds = activeAssignments.Select(a => a.RoleId).ToHashSet();

            // Revoke assignments not in desired.
            foreach (var a in activeAssignments)
            {
                var roleCode = tenantRoles.FirstOrDefault(r => r.Id == a.RoleId)?.Code;
                if (roleCode == null || !desired.Contains(roleCode))
                {
                    a.RevokedAt = DateTime.UtcNow;
                    a.RevokeReason = "platform-admin role set";
                }
            }
            // Add desired roles not yet active (create custom role if unknown via provisioning helper).
            foreach (var code in desired)
            {
                if (roleByCode.TryGetValue(code, out var role) && activeRoleIds.Contains(role.Id)) continue;
                await TenantProvisioning.AssignUserRoleAsync(_db, id, userId, code, null);
            }
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static PlanDto BuildPlanDto(SubscriptionPlan p, List<PlanModule> planModules, List<PlanModulePage> planPages, IReadOnlyDictionary<int, string> moduleCodeById)
        {
            var codes = planModules.Where(pm => pm.PlanId == p.Id)
                .Select(pm => moduleCodeById.TryGetValue(pm.ModuleId, out var c) ? c : null)
                .Where(c => c != null).Select(c => c!).OrderBy(c => c).ToList();
            var pages = planPages.Where(pp => pp.PlanId == p.Id)
                .Where(pp => moduleCodeById.ContainsKey(pp.ModuleId))
                .GroupBy(pp => moduleCodeById[pp.ModuleId])
                .ToDictionary(g => g.Key, g => g.Select(x => x.PageKey).ToList());
            return new PlanDto
            {
                Code = p.Tier,
                Name = p.Name,
                Tier = p.Tier,
                Description = p.Description,
                MonthlyPriceInr = p.MonthlyPrice,
                MaxUsers = p.MaxUsers,
                MaxVehicles = p.MaxVehicles,
                MaxMonthlyShipments = p.MaxMonthlyShipments,
                StorageLimitMb = p.StorageLimitMB,
                ModuleCodes = codes,
                PageRestrictions = pages.Count > 0 ? pages : null,
                IsActive = p.IsActive
            };
        }
    }
}
