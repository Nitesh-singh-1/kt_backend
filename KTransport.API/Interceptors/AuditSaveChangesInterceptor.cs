using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Interceptors
{
    /// <summary>
    /// Field-level audit trail. On every SaveChanges this inspects the ChangeTracker
    /// and writes one <see cref="AuditLog"/> row per changed business entity, recording
    /// the actual column diffs (old -&gt; new). Because it runs at the EF layer it is
    /// verb-independent and captures every edit regardless of which controller/raw-EF
    /// path made it.
    ///
    /// Scope (matches the "changes to existing records only" decision):
    /// - Modified entities  -> action "Update", with a "col: old -> new; ..." detail.
    /// - A Modified entity whose IsActive flips true-&gt;false (or IsDeleted false-&gt;true)
    ///   is a SOFT delete and is recorded as action "Delete" instead.
    /// - Deleted entities (genuine hard deletes) -> action "Delete".
    /// - Added entities (new records) are NOT audited.
    ///
    /// Safety: never throws into the save; the audit log itself and auth-plumbing
    /// tables are excluded (no recursion / no secret leakage).
    /// </summary>
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor _http;
        private readonly ILogger<AuditSaveChangesInterceptor> _logger;

        // Entities we never audit: the audit log (recursion), auth plumbing, and
        // high-churn infra tables that would only add noise.
        private static readonly HashSet<string> ExcludedTypes = new(StringComparer.Ordinal)
        {
            nameof(AuditLog), "RefreshToken", "VerificationCode", "MenuParityLog",
        };

        // Property names whose VALUE must never be written to the log — only the fact
        // that they changed. (Hashes/tokens/secrets.)
        private static readonly HashSet<string> SensitiveProps = new(StringComparer.OrdinalIgnoreCase)
        {
            "Password", "PasswordHash", "CodeHash", "Token", "TokenHash", "RefreshToken",
        };

        // Bookkeeping columns that change on almost every update — not interesting on
        // their own, so they never cause a log row by themselves (but are shown if other
        // fields also changed).
        private static readonly HashSet<string> NoiseProps = new(StringComparer.OrdinalIgnoreCase)
        {
            "UpdatedAt", "UpdatedBy", "ModifiedAt", "ModifiedBy", "RowVersion", "ConcurrencyStamp",
        };

        public AuditSaveChangesInterceptor(IHttpContextAccessor http, ILogger<AuditSaveChangesInterceptor> logger)
        {
            _http = http;
            _logger = logger;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            TryWriteAudit(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            TryWriteAudit(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void TryWriteAudit(DbContext? context)
        {
            if (context == null) return;

            try
            {
                // Snapshot first — we add AuditLog rows below and must not mutate while iterating.
                var entries = context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    .ToList();
                if (entries.Count == 0) return;

                var httpUser = _http.HttpContext?.User;
                int? userId = null;
                string? username = null;
                if (httpUser?.Identity?.IsAuthenticated == true)
                {
                    if (int.TryParse(httpUser.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid)) userId = uid;
                    username = httpUser.FindFirst(ClaimTypes.Name)?.Value;
                }
                var ip = ResolveIp(_http.HttpContext);
                var claimTenant = ResolveClaimTenant(httpUser);

                var logs = new List<AuditLog>();

                foreach (var entry in entries)
                {
                    var typeName = entry.Entity.GetType().Name;
                    if (ExcludedTypes.Contains(typeName)) continue;

                    var entityId = ResolveKey(entry);
                    string action;
                    string? details;

                    if (entry.State == EntityState.Deleted)
                    {
                        action = "Delete";
                        details = "Record hard-deleted";
                    }
                    else // Modified
                    {
                        var changes = new List<string>();
                        var softDeleted = false;

                        foreach (var prop in entry.Properties)
                        {
                            if (!prop.IsModified) continue;
                            var name = prop.Metadata.Name;
                            var oldV = prop.OriginalValue;
                            var newV = prop.CurrentValue;
                            if (Equals(oldV, newV)) continue;

                            if ((name.Equals("IsActive", StringComparison.OrdinalIgnoreCase) && oldV is bool ob && ob && newV is bool nb && !nb) ||
                                (name.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase) && oldV is bool od && !od && newV is bool nd && nd))
                            {
                                softDeleted = true;
                            }

                            if (NoiseProps.Contains(name)) continue;

                            changes.Add(SensitiveProps.Contains(name)
                                ? $"{name}: changed"
                                : $"{name}: {Format(oldV)} -> {Format(newV)}");
                        }

                        // Nothing meaningful changed (only bookkeeping columns) — skip,
                        // unless it was a soft delete, which we always want recorded.
                        if (changes.Count == 0 && !softDeleted) continue;

                        action = softDeleted ? "Delete" : "Update";
                        details = changes.Count > 0 ? string.Join("; ", changes) : "Soft-deleted";
                    }

                    if (details != null && details.Length > 1000) details = details.Substring(0, 997) + "...";

                    var tenantId = (entry.Entity as ITenantScopedEntity)?.TenantId ?? Guid.Empty;
                    if (tenantId == Guid.Empty) tenantId = claimTenant ?? TenantConstants.DefaultTenantId;

                    logs.Add(new AuditLog
                    {
                        TenantId = tenantId,
                        UserId = userId,
                        Username = username,
                        Action = action,
                        Success = true,
                        EntityType = typeName.Length > 100 ? typeName.Substring(0, 100) : typeName,
                        EntityId = entityId,
                        Details = details,
                        IpAddress = ip,
                        CreatedAt = DateTime.UtcNow,
                    });
                }

                if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
            }
            catch (Exception ex)
            {
                // Auditing must never break the operation it observes.
                _logger.LogError(ex, "AuditSaveChangesInterceptor failed to record changes");
            }
        }

        private static string? ResolveKey(EntityEntry entry)
        {
            var pk = entry.Metadata.FindPrimaryKey();
            if (pk == null) return null;

            var parts = pk.Properties.Select(p =>
            {
                var prop = entry.Property(p.Name);
                var val = entry.State == EntityState.Deleted ? prop.OriginalValue : prop.CurrentValue;
                return val?.ToString();
            }).Where(v => !string.IsNullOrEmpty(v));

            var id = string.Join(",", parts);
            if (string.IsNullOrEmpty(id)) return null;
            return id.Length > 100 ? id.Substring(0, 100) : id;
        }

        private static string Format(object? value)
        {
            if (value == null) return "null";
            var s = value switch
            {
                DateTime dt => dt.ToString("u"),
                bool b => b ? "true" : "false",
                _ => value.ToString() ?? "null",
            };
            return s.Length > 80 ? s.Substring(0, 77) + "..." : s;
        }

        private static Guid? ResolveClaimTenant(ClaimsPrincipal? user)
        {
            var raw = user?.FindFirst("tenant_id")?.Value ?? user?.FindFirst("TenantId")?.Value;
            return Guid.TryParse(raw, out var g) ? g : (Guid?)null;
        }

        private static string? ResolveIp(HttpContext? httpContext)
        {
            if (httpContext == null) return null;
            var fwd = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(fwd)) return fwd.Split(',')[0].Trim();
            return httpContext.Connection.RemoteIpAddress?.ToString();
        }
    }
}
