using System;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.AspNetCore.Http;

namespace KTransport.API.Services
{
    public class AuditLogService : IAuditLogService
    {
        private readonly KTransportDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITenantContext _tenantContext;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(
            KTransportDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ITenantContext tenantContext,
            ILogger<AuditLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _tenantContext = tenantContext;
            _logger = logger;
        }

        public async Task LogAsync(
            string action,
            bool success = true,
            Guid? tenantId = null,
            int? userId = null,
            string? username = null,
            string? entityType = null,
            string? entityId = null,
            string? details = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var user = httpContext?.User;

                if (userId == null && user?.Identity?.IsAuthenticated == true)
                {
                    var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(claim, out var parsedUserId))
                    {
                        userId = parsedUserId;
                    }
                }

                if (string.IsNullOrWhiteSpace(username) && user?.Identity?.IsAuthenticated == true)
                {
                    username = user.FindFirst(ClaimTypes.Name)?.Value;
                }

                var entry = new AuditLog
                {
                    TenantId = tenantId ?? _tenantContext.CurrentTenantId,
                    UserId = userId,
                    Username = username,
                    Action = action,
                    Success = success,
                    EntityType = entityType,
                    EntityId = entityId,
                    Details = details,
                    IpAddress = ResolveIpAddress(httpContext),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(entry);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Audit logging must never break the operation it's observing.
                _logger.LogError(ex, "Failed to write audit log entry for action {Action}", action);
            }
        }

        private static string? ResolveIpAddress(HttpContext? httpContext)
        {
            if (httpContext == null) return null;

            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
