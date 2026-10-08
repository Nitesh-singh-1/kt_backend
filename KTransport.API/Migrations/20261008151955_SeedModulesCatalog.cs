using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-046 Phase 1: idempotent seed of the global modules catalog.
    /// 15 modules covering today's product surface. Uses ON CONFLICT (code)
    /// DO NOTHING so re-running the migration is safe.
    /// </summary>
    public partial class SeedModulesCatalog : Migration
    {
        private static readonly (string Code, string Name, string Description, bool IsPremium)[] Catalog =
        {
            ("dashboard",            "Dashboard",             "Operational dashboard and KPI tiles",                            false),
            ("bilty",                "Bilty / Consignments",  "Consignment booking, GR / LR management, delivery settlement",   false),
            ("pod",                  "Proof of Delivery",     "POD capture, review, and audit",                                 false),
            ("trips",                "Trips & Manifests",     "Trip planning, manifest build, trip settlement, empty trips",    false),
            ("billing",              "Billing & Invoicing",   "Bill book, GST invoices, money receipts",                        false),
            ("master_data",          "Master Data",           "Parties, fleet, drivers, tyres, rates, compliance",              false),
            ("reports",              "Reports",               "Standard operational and financial reports",                     false),
            ("vendors",              "Vendors & Lorry Hire",  "Vendor master, lorry hire contracts, payables",                  false),
            ("claims",               "Claims",                "Cargo and vehicle insurance claims",                             false),
            ("quotations",           "Quotations",            "Freight quotations and conversion to bookings",                  false),
            ("tracking",             "Live Tracking",         "Vehicle live-tracking and position history",                     true),
            ("analytics",            "Analytics",             "Advanced analytics, drill-downs, cohort views",                  true),
            ("system",               "System",                "Settings, users, onboarding, SaaS configuration",                false),
            ("trip_settlement",      "Trip Settlement",       "Trip-level cost and advance settlement",                         false),
            ("delivery_settlement",  "Delivery Settlement",   "Delivery-side settlement and reconciliation",                    false),
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO modules (code, name, description, is_premium, is_active, display_order, created_at) VALUES");
            var rows = new System.Collections.Generic.List<string>();
            int order = 0;
            foreach (var (code, name, desc, premium) in Catalog)
            {
                rows.Add($"('{code}', '{name.Replace("'", "''")}', '{desc.Replace("'", "''")}', {(premium ? "TRUE" : "FALSE")}, TRUE, {order++}, CURRENT_TIMESTAMP)");
            }
            sb.AppendLine(string.Join(",\n", rows));
            sb.AppendLine("ON CONFLICT (code) DO NOTHING;");
            migrationBuilder.Sql(sb.ToString());
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // TASK-046 §Y: catalog rows stay forever even on rollback.
        }
    }
}
