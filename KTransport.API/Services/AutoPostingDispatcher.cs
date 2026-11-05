using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    /// <summary>
    /// Phase 2 auto-posting DISPATCHER (ADR accounting-autoposting-phase2.md Q1 / Option A3). A hosted
    /// <see cref="BackgroundService"/> that periodically drains the <c>auto_posting_queue</c>. It runs
    /// OUTSIDE any HTTP request, so it resolves the set of tenants with Pending rows (bypassing the
    /// tenant query filter), then opens a per-tenant scope, pins the tenant on <see cref="ITenantContext"/>,
    /// and delegates to <see cref="IAutoPostingService.DrainPendingForCurrentTenantAsync"/> which:
    ///   - skips a tenant whose auto-posting is OFF (rows stay Pending),
    ///   - defers rows whose period is Closed,
    ///   - caps retries on transient failures,
    /// and is idempotent so a crash/double-dispatch can never double-post.
    /// </summary>
    public class AutoPostingDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoPostingDispatcher> _logger;

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
        private const int MaxAttempts = 5;

        public AutoPostingDispatcher(IServiceScopeFactory scopeFactory, ILogger<AutoPostingDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // A brief startup delay lets migrations/seeding finish before the first drain.
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DrainAllTenantsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-posting dispatcher pass failed; will retry next interval.");
                }

                try { await Task.Delay(PollInterval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task DrainAllTenantsAsync(CancellationToken ct)
        {
            Guid[] tenantIds;
            using (var scanScope = _scopeFactory.CreateScope())
            {
                var db = scanScope.ServiceProvider.GetRequiredService<KTransportDbContext>();
                // Cross-tenant scan: ignore the tenant query filter to see every tenant's Pending rows.
                tenantIds = await db.AutoPostingQueue
                    .IgnoreQueryFilters()
                    .Where(r => r.Status == AutoPostStatus.Pending)
                    .Select(r => r.TenantId)
                    .Distinct()
                    .ToArrayAsync(ct);
            }

            foreach (var tenantId in tenantIds)
            {
                ct.ThrowIfCancellationRequested();
                using var scope = _scopeFactory.CreateScope();
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.SetTenantId(tenantId);

                var engine = scope.ServiceProvider.GetRequiredService<IAutoPostingService>();
                try
                {
                    var posted = await engine.DrainPendingForCurrentTenantAsync(MaxAttempts, ct);
                    if (posted > 0)
                        _logger.LogInformation("Auto-posting dispatcher posted {Count} voucher(s) for tenant {TenantId}.", posted, tenantId);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-posting drain failed for tenant {TenantId}.", tenantId);
                }
            }
        }
    }
}
