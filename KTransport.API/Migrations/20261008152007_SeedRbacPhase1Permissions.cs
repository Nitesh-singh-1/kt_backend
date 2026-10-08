using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-046 Phase 1: additive seed of the permissions catalog with the new
    /// RBAC-admin keys. Idempotent via ON CONFLICT (key) DO NOTHING. Existing
    /// 126 rows stay untouched; this only adds roles.*, designations.*,
    /// modules.view, tenant_modules.view, and any missing system.users.* keys.
    /// </summary>
    public partial class SeedRbacPhase1Permissions : Migration
    {
        private static readonly (string Feature, string[] Actions)[] Additions =
        {
            ("roles",                      new[] { "View", "Create", "Edit" }),
            ("designations",               new[] { "View", "Create", "Edit", "Delete" }),
            ("modules",                    new[] { "View" }),
            ("tenant_modules",             new[] { "View" }),
            ("system.users",               new[] { "View", "Create", "Edit", "Delete" }),
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var sb = new StringBuilder();
            sb.AppendLine("INSERT INTO permissions (\"key\", feature_key, action, description, display_order) VALUES");
            var rows = new System.Collections.Generic.List<string>();
            int order = 1000; // keep away from the Phase-1 TASK-040 range (0-200)
            foreach (var (feature, actions) in Additions)
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

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // TASK-046 §Y: catalog rows stay forever even on rollback.
        }
    }
}
