using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Adds the "Roles &amp; Permissions" sidebar entry (key "system.roles",
    /// path /roles) for the custom-role management screen (TASK-051 Phase 4).
    ///
    /// Idempotent: INSERT … ON CONFLICT (key) DO NOTHING. The module_id is
    /// resolved from the "system" module row rather than hardcoded. The
    /// permission_key is "system.users.view" — a real catalog permission that
    /// every admin holds (and that gates the "Manage Users" row too), so whoever
    /// can view users also sees role management. (NB: "users.manage" is only
    /// granted to the platform SUPER_USER role, so it would hide this row from
    /// ordinary tenant admins — see the menu visibility rule in MenuCatalogService.)
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261014120000_AddRolesMenuItem")]
    public partial class AddRolesMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO menu_items
    (key,             parent_key, title,                  path,     icon,     permission_key, badge, display_order, visibility_rule, is_active, module_id)
SELECT
    'system.roles',  'system',   'Roles & Permissions',  '/roles', 'shield', 'system.users.view', NULL, 1,          NULL,            true,      m.id
FROM modules m
WHERE m.code = 'system'
ON CONFLICT (key) DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_items WHERE key = 'system.roles';");
        }
    }
}
