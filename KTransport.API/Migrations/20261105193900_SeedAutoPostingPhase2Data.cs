using System.Collections.Generic;
using System.Text;
using KTransport.API.Common;
using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Phase 2 auto-posting DATA seed (no schema change; follows the SeedAccountingModuleMenuPermissions
    /// pattern — inline [Migration] attribute, no Designer / snapshot change). Idempotent via
    /// ON CONFLICT DO NOTHING. Four steps:
    ///   1. Well-known system LEAF ledgers (ADR Q3) under the Phase 1 groups, for EVERY tenant that
    ///      already has the chart of accounts (the data migration for existing-module tenants).
    ///   2. New permission catalog rows accounting.autoposting.{view,manage}.
    ///   3. Grant those to every tenant's admin role (tenant_modules still gates visibility).
    ///   4. Two sidebar menu items (Auto-Posting, Reconciliation) under the Accounting parent.
    /// The well-known ledger list is sourced from <see cref="WellKnownLedgers.All"/> so it cannot drift.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261105193900_SeedAutoPostingPhase2Data")]
    public partial class SeedAutoPostingPhase2Data : Migration
    {
        // feature-key -> actions for the two NEW Phase 2 keys.
        private static readonly (string Feature, string[] Actions)[] PermCatalog =
        {
            ("accounting.autoposting", new[] { "View", "Manage" }),
        };

        // key, parent, title, path, icon, permission_key, display_order
        private static readonly (string Key, string Parent, string Title, string Path, string Icon, string Perm, int Order)[] Menu =
        {
            ("accounting.auto_posting",   "accounting", "Auto-Posting",   "/accounting/auto-posting",   "zap",         "accounting.autoposting.view", 7),
            ("accounting.reconciliation", "accounting", "Reconciliation", "/accounting/reconciliation", "checkCircle", "accounting.reports.view",     8),
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Well-known leaf ledgers for every tenant that already has the chart of accounts.
            //    account_type / nature are inherited from the resolved parent GROUP row (per tenant).
            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO accounts (tenant_id, parent_id, code, name, account_type, nature, is_group, is_system, is_active, created_at)");
            sb.AppendLine("SELECT g.tenant_id, g.id, v.code, v.name, g.account_type, g.nature, v.is_group, true, true, CURRENT_TIMESTAMP");
            sb.AppendLine("FROM (VALUES");
            var rows = new List<string>();
            foreach (var wk in WellKnownLedgers.All)
                rows.Add($"  ('{Esc(wk.Key)}', '{Esc(wk.Name)}', '{Esc(wk.ParentGroupCode)}', {(wk.IsGroup ? "true" : "false")})");
            sb.AppendLine(string.Join(",\n", rows));
            sb.AppendLine(") AS v(code, name, parent_code, is_group)");
            sb.AppendLine("JOIN accounts g ON g.code = v.parent_code AND g.is_group = true");
            sb.AppendLine("ON CONFLICT (tenant_id, code) DO NOTHING;");
            migrationBuilder.Sql(sb.ToString());

            // 2. Permission catalog rows (module_id resolved from the accounting module, like Phase 1).
            var pb = new StringBuilder();
            pb.AppendLine("INSERT INTO permissions (\"key\", feature_key, action, description, display_order, module_id)");
            pb.AppendLine("SELECT v.\"key\", v.feature_key, v.action, v.description, v.display_order, m.id");
            pb.AppendLine("FROM (VALUES");
            var prows = new List<string>();
            int order = 1100; // after the Phase 1 accounting catalog block
            foreach (var (feature, actions) in PermCatalog)
                foreach (var action in actions)
                    prows.Add($"  ('{feature}.{action.ToLowerInvariant()}', '{feature}', '{action}', '{action} on {feature}', {order++})");
            pb.AppendLine(string.Join(",\n", prows));
            pb.AppendLine(") AS v(\"key\", feature_key, action, description, display_order)");
            pb.AppendLine("CROSS JOIN modules m WHERE m.code = 'accounting'");
            pb.AppendLine("ON CONFLICT (\"key\") DO NOTHING;");
            migrationBuilder.Sql(pb.ToString());

            // 3. Grant the new accounting.autoposting.* permissions to every tenant's admin role.
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'admin'
   AND p.feature_key = 'accounting.autoposting'
   AND NOT EXISTS (
     SELECT 1 FROM role_permissions rp
      WHERE rp.tenant_id = r.tenant_id
        AND rp.role_name = r.code
        AND rp.permission_key = p.key
        AND rp.revoked_at IS NULL
   )
ON CONFLICT (role_id, permission_id) DO NOTHING;");

            // 4. Menu items (module_id resolved from the accounting module row).
            var mb = new StringBuilder();
            foreach (var (key, parent, title, path, icon, perm, ord) in Menu)
            {
                mb.AppendLine($@"
INSERT INTO menu_items (key, parent_key, title, path, icon, permission_key, badge, display_order, visibility_rule, is_active, module_id)
SELECT '{key}', '{parent}', '{title.Replace("'", "''")}', '{path}', '{icon}', '{perm}', NULL, {ord}, NULL, true, m.id
FROM modules m WHERE m.code = 'accounting'
ON CONFLICT (key) DO NOTHING;");
            }
            migrationBuilder.Sql(mb.ToString());
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_items WHERE key IN ('accounting.auto_posting','accounting.reconciliation');");
            migrationBuilder.Sql("DELETE FROM role_permissions WHERE permission_key LIKE 'accounting.autoposting.%';");
            migrationBuilder.Sql("DELETE FROM permissions WHERE feature_key = 'accounting.autoposting';");
            // Well-known ledgers are left in place (they may already carry postings); like the
            // Phase 1 group seed, they are catalog-forever and only removed with the schema teardown.
        }

        private static string Esc(string s) => s.Replace("'", "''");
    }
}
