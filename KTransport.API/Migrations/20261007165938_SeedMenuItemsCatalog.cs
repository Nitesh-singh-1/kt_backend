using System.Collections.Generic;
using System.Text;
using KTransport.API.Common;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-041 Phase 1: idempotent seed of the global menu_items catalog.
    /// 38 rows mirror the hard-coded tree in NavigationService.GetDynamicMenuAsync
    /// (lines ~150-700, grep -c "new DynamicMenuItemDto" NavigationService.cs = 38).
    /// Uses ON CONFLICT (key) DO NOTHING so re-running (or adding this migration
    /// against an already-seeded database) is safe.
    ///
    /// The per-tenant "Reports children" loop stays in C# code for Phase 1 — its
    /// per-tenant dynamic nature means the catalog carries only the parent `reports`
    /// row (visibility_rule='report_entitlement'); children are emitted at request
    /// time against the tenant's enabled-reports list.
    ///
    /// Row data lives in <see cref="MenuCatalogSeedData"/> so tests can reference
    /// the same list without duplicating 38 lines.
    /// </summary>
    public partial class SeedMenuItemsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            static string S(string v) => v == null ? "NULL" : "'" + v.Replace("'", "''") + "'";

            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO menu_items (\"key\", parent_key, title, path, icon, permission_key, badge, display_order, visibility_rule, is_active) VALUES");

            var rowSql = new List<string>();
            foreach (var r in MenuCatalogSeedData.Rows)
            {
                rowSql.Add($"({S(r.Key)}, {S(r.ParentKey)}, {S(r.Title)}, {S(r.Path)}, {S(r.Icon)}, {S(r.PermissionKey)}, {S(r.Badge)}, {r.DisplayOrder}, {S(r.VisibilityRule)}, TRUE)");
            }
            sb.AppendLine(string.Join(",\n", rowSql));
            sb.AppendLine("ON CONFLICT (\"key\") DO NOTHING;");

            migrationBuilder.Sql(sb.ToString());
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Catalog-only Down. Safe to leave rows in place on rollback.
            migrationBuilder.Sql("DELETE FROM menu_items;");
        }
    }
}
