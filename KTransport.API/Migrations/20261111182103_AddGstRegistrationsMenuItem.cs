using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Backfills the "system.gst_registrations" menu-catalog row (added to
    /// MenuCatalogSeedData after SeedMenuItemsCatalog was already recorded in
    /// __EFMigrationsHistory, so the seed never re-ran). Nests it under the
    /// existing "system" row and inherits that row's module_id. Idempotent:
    /// ON CONFLICT (key) DO NOTHING, and a no-op where "system" is absent.
    /// Matches the canonical catalog in MenuCatalogSeedData.cs (TASK-034 GST UI).
    /// </summary>
    public partial class AddGstRegistrationsMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO menu_items
    (key, parent_id, title, path, icon, permission_key, badge, display_order, visibility_rule, is_active, module_id)
SELECT 'system.gst_registrations', p.id, 'GST Registrations', '/settings/gst-registrations',
       'fileText', 'settings.manage', NULL, 6, NULL, true, p.module_id
  FROM menu_items p
 WHERE p.key = 'system'
ON CONFLICT (key) DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_items WHERE key = 'system.gst_registrations';");
        }
    }
}
