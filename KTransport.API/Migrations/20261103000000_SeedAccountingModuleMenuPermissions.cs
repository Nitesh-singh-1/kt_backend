using System.Text;
using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-016 Phase 1 seed: registers the Accounting &amp; Finance module, its permission-catalog
    /// rows, and its sidebar menu entries. Data-only (no schema change), so it follows the
    /// AddAuditLogMenuItem pattern (inline [Migration] attribute, no Designer / snapshot change).
    ///
    /// Idempotent via ON CONFLICT DO NOTHING. The module is <c>is_premium = TRUE</c> (paid add-on):
    /// a tenant only sees Accounting once it is enabled in <c>tenant_modules</c>. Admin roles do NOT
    /// hold a "*" wildcard — they carry explicit role_permissions rows (see
    /// GrantAdminRoleAllPermissionsForAllTenants), and that one-time grant ran before these rows
    /// existed, so step 4 re-grants accounting.* to every tenant's admin role. tenant_modules still
    /// gates visibility, so the grant stays dormant until the paid add-on is enabled. The catalog
    /// rows also let sub-user roles be granted accounting access from the RBAC matrix.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261103000000_SeedAccountingModuleMenuPermissions")]
    public partial class SeedAccountingModuleMenuPermissions : Migration
    {
        // feature-key -> actions, mirrors EntitlementsCatalog accounting.* entries.
        private static readonly (string Feature, string[] Actions)[] PermCatalog =
        {
            ("accounting",           new[] { "View" }),
            ("accounting.accounts",  new[] { "View", "Create", "Edit", "Delete" }),
            ("accounting.vouchers",  new[] { "View", "Create", "Delete", "Print" }),
            ("accounting.periods",   new[] { "View", "Create", "Edit" }),
            ("accounting.reports",   new[] { "View", "Export" }),
        };

        // key, parent, title, path, icon, permission_key, display_order
        private static readonly (string Key, string Parent, string Title, string Path, string Icon, string Perm, int Order)[] Menu =
        {
            ("accounting",                 null,         "Accounting & Finance", null,                          "dollarSign", "accounting.view",          0),
            ("accounting.chart_of_accounts","accounting", "Chart of Accounts",    "/accounting/chart-of-accounts","book",      "accounting.accounts.view", 0),
            ("accounting.vouchers",        "accounting", "Vouchers",             "/accounting/vouchers",        "fileText",   "accounting.vouchers.view", 1),
            ("accounting.day_book",        "accounting", "Day Book",             "/accounting/day-book",        "calendar",   "accounting.reports.view",  2),
            ("accounting.ledger",         "accounting", "Account Ledger",        "/accounting/ledger",          "fileText",   "accounting.reports.view",  3),
            ("accounting.trial_balance",   "accounting", "Trial Balance",        "/accounting/trial-balance",   "barChart",   "accounting.reports.view",  4),
            ("accounting.periods",         "accounting", "Financial Years",      "/accounting/periods",         "calendar",   "accounting.periods.view",  5),
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Module (paid add-on).
            migrationBuilder.Sql(@"
INSERT INTO modules (code, name, description, is_premium, is_active, display_order, created_at)
VALUES ('accounting', 'Accounting & Finance', 'Double-entry ledger: chart of accounts, vouchers, financial statements', TRUE, TRUE, 15, CURRENT_TIMESTAMP)
ON CONFLICT (code) DO NOTHING;");

            // 2. Permission catalog rows. permissions.module_id is NOT NULL with FK
            //    fk_permissions_module (20261010103000_MenuRbacNormalization), so it must be set.
            //    Resolve it from the accounting module row via CROSS JOIN rather than a bare VALUES
            //    list, the same way the menu_items insert below does.
            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO permissions (\"key\", feature_key, action, description, display_order, module_id)");
            sb.AppendLine("SELECT v.\"key\", v.feature_key, v.action, v.description, v.display_order, m.id");
            sb.AppendLine("FROM (VALUES");
            var rows = new System.Collections.Generic.List<string>();
            int order = 1000; // after existing catalog
            foreach (var (feature, actions) in PermCatalog)
                foreach (var action in actions)
                    rows.Add($"  ('{feature}.{action.ToLowerInvariant()}', '{feature}', '{action}', '{action} on {feature}', {order++})");
            sb.AppendLine(string.Join(",\n", rows));
            sb.AppendLine(") AS v(\"key\", feature_key, action, description, display_order)");
            sb.AppendLine("CROSS JOIN modules m WHERE m.code = 'accounting'");
            sb.AppendLine("ON CONFLICT (\"key\") DO NOTHING;");
            migrationBuilder.Sql(sb.ToString());

            // 3. Menu items (module_id resolved from the accounting module row).
            var mb = new StringBuilder();
            foreach (var (key, parent, title, path, icon, perm, ord) in Menu)
            {
                var parentSql = parent == null ? "NULL" : $"'{parent}'";
                var pathSql = path == null ? "NULL" : $"'{path}'";
                mb.AppendLine($@"
INSERT INTO menu_items (key, parent_key, title, path, icon, permission_key, badge, display_order, visibility_rule, is_active, module_id)
SELECT '{key}', {parentSql}, '{title.Replace("'", "''")}', {pathSql}, '{icon}', '{perm}', NULL, {ord}, NULL, true, m.id
FROM modules m WHERE m.code = 'accounting'
ON CONFLICT (key) DO NOTHING;");
            }
            migrationBuilder.Sql(mb.ToString());

            // 4. Grant the new accounting.* permissions to every tenant's admin role.
            //    GrantAdminRoleAllPermissionsForAllTenants (20261013220100) ran BEFORE these rows
            //    existed, so admins would otherwise never receive them. tenant_modules still gates
            //    visibility (paid add-on), so this stays dormant until the module is enabled.
            //    Mirrors that migration's NOT EXISTS (partial unique index) + ON CONFLICT idempotency.
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'admin'
   AND (p.feature_key = 'accounting' OR p.feature_key LIKE 'accounting.%')
   AND NOT EXISTS (
     SELECT 1 FROM role_permissions rp
      WHERE rp.tenant_id = r.tenant_id
        AND rp.role_name = r.code
        AND rp.permission_key = p.key
        AND rp.revoked_at IS NULL
   )
ON CONFLICT (role_id, permission_id) DO NOTHING;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_items WHERE key LIKE 'accounting%';");
            // role_permissions.permission_id FKs permissions.id, so clear the grants first.
            migrationBuilder.Sql("DELETE FROM role_permissions WHERE permission_key LIKE 'accounting%';");
            migrationBuilder.Sql("DELETE FROM permissions WHERE feature_key LIKE 'accounting%';");
            // Module row stays (catalog-forever convention, matches SeedModulesCatalog.Down).
        }
    }
}
