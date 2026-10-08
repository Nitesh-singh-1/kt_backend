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
            new("clients",               new[] { "View", "Create", "Edit", "Delete" }),
            new("system",                new[] { "View" }),
            new("system.settings",       new[] { "View", "Edit" }),
            new("system.users",          new[] { "View", "Create", "Edit", "Delete" }),
            new("system.onboard",        new[] { "View", "Create" })
        };

        /// <summary>
        /// Maps legacy JSON-era or shorthand feature keys ("gr", "challan", "bilty")
        /// to canonical catalog feature keys ("consignments", "trips", etc.).
        /// </summary>
        public static string NormalizeFeatureKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var k = key.Trim().ToLowerInvariant();
            return k switch
            {
                "gr" => "consignments",
                "gr.list" => "consignments.all",
                "gr.entry" => "consignments.create",
                "bilty" => "consignments",
                "challan" => "trips",
                "challan.list" => "trips",
                "challan.entry" => "trips",
                "trip" => "trips",
                "trip_settlement" => "trip_settlement",
                "delivery_settlement" => "delivery_settlement",
                "pod" => "pod",
                "bill_book" => "billing.bill_book",
                "billing.invoices" => "billing.invoices",
                "billing.receipts" => "billing.receipts",
                "master_data.parties" => "master_data.parties",
                "master_data.fleet" => "master_data.fleet",
                "master_data.compliance" => "master_data.compliance",
                "master_data.tyres" => "master_data.tyres",
                "master_data.spares" => "master_data.spares",
                "master_data.loans" => "master_data.loans",
                "master_data.driver_ledger" => "master_data.driver_ledger",
                "master_data.vehicle_claims" => "master_data.vehicle_claims",
                "master_data.rates" => "master_data.rates",
                "master_data.vendor_rates" => "master_data.vendor_rates",
                "vendors" => "vendors",
                "claims" => "claims",
                "quotations" => "quotations",
                "reports.booking_register" => "reports.booking_register",
                "reports.tax_summary" => "reports.tax_summary",
                "reports.party_outstanding" => "reports.party_outstanding",
                "reports.trip_profitability" => "reports.trip_profitability",
                "reports.vendor_payables" => "reports.vendor_payables",
                "system.settings" => "system.settings",
                "system.users" => "system.users",
                "system.onboard" => "system.onboard",
                _ => k
            };
        }

        /// <summary>
        /// Expand a feature key (as stored in the legacy JSON or passed from UI) into the catalog
        /// permission keys ("feature.action") that back it. Handles aliases and parent groups.
        /// </summary>
        public static IReadOnlyList<string> ExpandFeatureKey(string featureKey)
        {
            if (string.IsNullOrWhiteSpace(featureKey)) return System.Array.Empty<string>();
            var normalized = NormalizeFeatureKey(featureKey);
            var result = new List<string>();

            foreach (var spec in Features)
            {
                if (string.Equals(spec.FeatureKey, normalized, System.StringComparison.OrdinalIgnoreCase) ||
                    (!normalized.Contains('.') && spec.FeatureKey.StartsWith(normalized + ".")))
                {
                    foreach (var action in spec.Actions)
                    {
                        result.Add($"{spec.FeatureKey}.{action.ToLowerInvariant()}");
                    }
                }
            }

            // Also check for direct match if raw key differed
            if (result.Count == 0)
            {
                foreach (var spec in Features)
                {
                    if (string.Equals(spec.FeatureKey, featureKey.Trim(), System.StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var action in spec.Actions)
                        {
                            result.Add($"{spec.FeatureKey}.{action.ToLowerInvariant()}");
                        }
                    }
                }
            }

            return result;
        }
    }
}
