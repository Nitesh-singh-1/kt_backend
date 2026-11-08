using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-016 Phase 1 (review B3): adds the "Opening Balances" sidebar entry under the
    /// Accounting module. Data-only, idempotent via ON CONFLICT (key) DO NOTHING, mirrors the
    /// menu_items insert in SeedAccountingModuleMenuPermissions. Reuses the existing
    /// accounting.accounts.view permission (no new permission / role grant needed).
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261104000000_SeedOpeningBalancesMenuItem")]
    public partial class SeedOpeningBalancesMenuItem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO menu_items (key, parent_key, title, path, icon, permission_key, badge, display_order, visibility_rule, is_active, module_id)
SELECT 'accounting.opening_balances', 'accounting', 'Opening Balances', '/accounting/opening-balances', 'wallet', 'accounting.accounts.view', NULL, 6, NULL, true, m.id
FROM modules m WHERE m.code = 'accounting'
ON CONFLICT (key) DO NOTHING;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_items WHERE key = 'accounting.opening_balances';");
        }
    }
}
