using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-040 Phase 1: idempotent seed of the global permissions catalog.
    /// One row per (feature × action) for every feature currently in the
    /// hard-coded menu tree in NavigationService.GetDynamicMenuAsync.
    /// Uses ON CONFLICT (key) DO NOTHING so re-running the migration (or
    /// adding this migration against an already-seeded database) is safe.
    /// </summary>
    public partial class SeedPermissionsCatalog : Migration
    {
        private static readonly (string Feature, string[] Actions)[] Catalog =
        {
            ("dashboard",                   new[] { "View" }),
            ("analytics",                   new[] { "View" }),
            ("tracking",                    new[] { "View" }),
            ("consignments",                new[] { "View" }),
            ("consignments.create",         new[] { "View", "Create", "Edit", "Delete", "Print" }),
            ("consignments.all",            new[] { "View", "Edit", "Delete", "Print", "Export" }),
            ("delivery_settlement",         new[] { "View", "Create", "Edit" }),
            ("trips",                       new[] { "View", "Create", "Edit", "Delete", "Print" }),
            ("trip_settlement",             new[] { "View", "Create", "Edit" }),
            ("empty_trips",                 new[] { "View", "Create", "Edit", "Delete" }),
            ("pod",                         new[] { "View", "Create", "Edit" }),
            ("billing",                     new[] { "View" }),
            ("billing.bill_book",           new[] { "View", "Create", "Edit", "Delete", "Print" }),
            ("billing.invoices",            new[] { "View", "Create", "Edit", "Delete", "Print", "Export" }),
            ("billing.receipts",            new[] { "View", "Create", "Edit", "Delete", "Print" }),
            ("master_data",                 new[] { "View" }),
            ("master_data.parties",         new[] { "View", "Create", "Edit", "Delete", "Export" }),
            ("master_data.fleet",           new[] { "View", "Create", "Edit", "Delete" }),
            ("master_data.compliance",      new[] { "View", "Export" }),
            ("master_data.tyres",           new[] { "View", "Create", "Edit", "Delete" }),
            ("master_data.spares",          new[] { "View", "Create", "Edit", "Delete" }),
            ("master_data.loans",           new[] { "View", "Create", "Edit", "Delete" }),
            ("master_data.driver_ledger",   new[] { "View", "Create", "Edit" }),
            ("master_data.vehicle_claims",  new[] { "View", "Create", "Edit", "Delete" }),
            ("master_data.rates",           new[] { "View", "Create", "Edit", "Delete" }),
            ("master_data.vendor_rates",    new[] { "View", "Create", "Edit", "Delete" }),
            ("vendors",                     new[] { "View", "Create", "Edit", "Delete" }),
            ("claims",                      new[] { "View", "Create", "Edit", "Approve" }),
            ("quotations",                  new[] { "View", "Create", "Edit", "Delete", "Approve", "Print" }),
            ("reports",                     new[] { "View" }),
            ("reports.booking_register",    new[] { "View", "Export" }),
            ("reports.tax_summary",         new[] { "View", "Export" }),
            ("reports.party_outstanding",   new[] { "View", "Export" }),
            ("reports.trip_profitability",  new[] { "View", "Export" }),
            ("reports.vendor_payables",     new[] { "View", "Export" }),
            ("clients",                     new[] { "View", "Create", "Edit", "Delete" }),
            ("system",                      new[] { "View" }),
            ("system.settings",             new[] { "View", "Edit" }),
            ("system.users",                new[] { "View", "Create", "Edit", "Delete" }),
            ("system.onboard",              new[] { "View", "Create" })
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO permissions (\"key\", feature_key, action, description, display_order) VALUES");

            var rows = new System.Collections.Generic.List<string>();
            int order = 0;
            foreach (var (feature, actions) in Catalog)
            {
                foreach (var action in actions)
                {
                    var key = $"{feature}.{action.ToLowerInvariant()}";
                    var desc = $"{action} on {feature}";
                    rows.Add($"('{key}', '{feature}', '{action}', '{desc}', {order++})");
                }
            }

            sb.AppendLine(string.Join(",\n", rows));
            sb.AppendLine("ON CONFLICT (\"key\") DO NOTHING;");

            migrationBuilder.Sql(sb.ToString());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Catalog-only Down. Safe to leave rows in place on rollback.
            migrationBuilder.Sql("DELETE FROM permissions;");
        }
    }
}
