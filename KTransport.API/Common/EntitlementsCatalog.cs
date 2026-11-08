using System.Collections.Generic;

namespace KTransport.API.Common
{
    /// <summary>
    /// TASK-040 Phase 1: hard-coded source-of-truth catalog of (feature × action)
    /// pairs. Mirrors the <c>catalog_seed.features</c> list in
    /// <c>.agent/contracts/TASK-040-entitlements-tables-phase1.yaml</c>.
    ///
    /// Used by:
    ///   - The <c>SeedPermissionsCatalog</c> migration (INSERTs the rows).
    ///   - <see cref="Services.EntitlementsService"/> at backfill time to
    ///     expand "role has feature X" into "role has (X.view, X.create, ...)".
    /// Keep this in lock-step with the migration; the migration INSERTs are
    /// idempotent (ON CONFLICT DO NOTHING) so re-running is safe.
    /// </summary>
    public static class EntitlementsCatalog
    {
        public static readonly string[] Actions =
        {
            "View", "Create", "Edit", "Delete", "Print", "Approve", "Export"
        };

        public sealed record FeatureSpec(string FeatureKey, string[] Actions);

        public static readonly IReadOnlyList<FeatureSpec> Features = new List<FeatureSpec>
        {
            new("dashboard",             new[] { "View" }),
            new("analytics",             new[] { "View" }),
            new("tracking",              new[] { "View" }),
            new("consignments",          new[] { "View" }),
            new("consignments.create",   new[] { "View", "Create", "Edit", "Delete", "Print" }),
            new("consignments.all",      new[] { "View", "Edit", "Delete", "Print", "Export" }),
            new("delivery_settlement",   new[] { "View", "Create", "Edit" }),
            new("trips",                 new[] { "View", "Create", "Edit", "Delete", "Print" }),
            new("trip_settlement",       new[] { "View", "Create", "Edit" }),
            new("empty_trips",           new[] { "View", "Create", "Edit", "Delete" }),
            new("pod",                   new[] { "View", "Create", "Edit" }),
            new("billing",               new[] { "View" }),
            new("billing.bill_book",     new[] { "View", "Create", "Edit", "Delete", "Print" }),
            new("billing.invoices",      new[] { "View", "Create", "Edit", "Delete", "Print", "Export" }),
            new("billing.receipts",      new[] { "View", "Create", "Edit", "Delete", "Print" }),
            new("master_data",           new[] { "View" }),
            new("master_data.parties",   new[] { "View", "Create", "Edit", "Delete", "Export" }),
            new("master_data.fleet",     new[] { "View", "Create", "Edit", "Delete" }),
            new("master_data.compliance", new[] { "View", "Export" }),
            new("master_data.tyres",     new[] { "View", "Create", "Edit", "Delete" }),
            new("master_data.spares",    new[] { "View", "Create", "Edit", "Delete" }),
            new("master_data.loans",     new[] { "View", "Create", "Edit", "Delete" }),
            new("master_data.driver_ledger", new[] { "View", "Create", "Edit" }),
            new("master_data.vehicle_claims", new[] { "View", "Create", "Edit", "Delete" }),
            new("master_data.rates",     new[] { "View", "Create", "Edit", "Delete" }),
            new("master_data.vendor_rates", new[] { "View", "Create", "Edit", "Delete" }),
            new("vendors",               new[] { "View", "Create", "Edit", "Delete" }),
            new("claims",                new[] { "View", "Create", "Edit", "Approve" }),
            new("quotations",            new[] { "View", "Create", "Edit", "Delete", "Approve", "Print" }),
            new("reports",               new[] { "View" }),
            new("reports.booking_register", new[] { "View", "Export" }),
            new("reports.tax_summary",   new[] { "View", "Export" }),
            new("reports.party_outstanding", new[] { "View", "Export" }),
            new("reports.trip_profitability", new[] { "View", "Export" }),
            new("reports.vendor_payables", new[] { "View", "Export" }),
            new("accounting",            new[] { "View" }),
            new("accounting.accounts",   new[] { "View", "Create", "Edit", "Delete" }),
            new("accounting.vouchers",   new[] { "View", "Create", "Delete", "Print" }),
            new("accounting.periods",    new[] { "View", "Create", "Edit" }),
            new("accounting.reports",    new[] { "View", "Export" }),
            new("clients",               new[] { "View", "Create", "Edit", "Delete" }),
            new("system",                new[] { "View" }),
            new("system.settings",       new[] { "View", "Edit" }),
            new("system.users",          new[] { "View", "Create", "Edit", "Delete" }),
            new("system.onboard",        new[] { "View", "Create" })
        };

        // RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): the legacy
        // NormalizeFeatureKey (alias map) and ExpandFeatureKey (dotted expansion)
        // are DELETED. Feature/permission assignment now resolves against the
        // permissions catalog by canonical key prefix — see
        // EntitlementsCatalog.ExpandToPermissionKeys below (catalog-driven, no aliases).

        /// <summary>
        /// Expand a canonical feature/page key into the catalog permission keys it
        /// backs, by exact prefix against <see cref="Features"/>. "billing" →
        /// billing.view + billing.&lt;child&gt;.* ; "billing.invoices" →
        /// billing.invoices.* . No alias translation, no cross-module magic.
        /// </summary>
        public static IReadOnlyList<string> ExpandToPermissionKeys(string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return System.Array.Empty<string>();
            var key = featureKey.Trim();
            var result = new List<string>();
            foreach (var spec in Features)
            {
                bool match = string.Equals(spec.FeatureKey, key, System.StringComparison.OrdinalIgnoreCase)
                             || spec.FeatureKey.StartsWith(key + ".", System.StringComparison.OrdinalIgnoreCase);
                if (!match) continue;
                foreach (var action in spec.Actions)
                    result.Add($"{spec.FeatureKey}.{action.ToLowerInvariant()}");
            }
            return result;
        }
    }
}
