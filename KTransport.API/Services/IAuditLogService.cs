using System;
using System.Threading.Tasks;

namespace KTransport.API.Services
{
    public interface IAuditLogService
    {
        /// <summary>
        /// Records a sensitive/security-relevant action. Never throws — logging failures are swallowed
        /// (and reported to ILogger) so audit logging can never break the calling operation.
        /// Tenant/user/IP are inferred from the current HttpContext when not supplied explicitly,
        /// which is required for pre-authentication events like failed logins.
        /// </summary>
        Task LogAsync(
            string action,
            bool success = true,
            Guid? tenantId = null,
            int? userId = null,
            string? username = null,
            string? entityType = null,
            string? entityId = null,
            string? details = null);
    }
}
