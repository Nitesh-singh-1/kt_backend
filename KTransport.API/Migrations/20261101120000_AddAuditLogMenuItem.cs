using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Adds the "Audit Log" sidebar entry (key "system.audit_logs", path
    /// /audit-logs) that surfaces the security audit log viewer (GET /api/auditlog).
    ///
    /// Idempotent: INSERT … ON CONFLICT (key) DO NOTHING. module_id is resolved
    /// from the "system" module row rather than hardcoded. permission_key is
    /// "system.users.view" — a real catalog permission every tenant admin holds
    /// (same gate the Roles screen uses), so whoever can administer users/roles
    /// also sees the audit log. The API itself is RequireSuperUser, which every
    /// tenant admin satisfies, so the menu and the endpoint agree on the audience.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261101120000_AddAuditLogMenuItem")]
    public partial class AddAuditLogMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO menu_items
    (key,                parent_key, title,        path,          icon,     permission_key,       badge, display_order, visibility_rule, is_active, module_id)
SELECT
    'system.audit_logs', 'system',   'Audit Log',  '/audit-logs', 'shield', 'system.users.view',  NULL,  5,             NULL,            true,      m.id
FROM modules m
WHERE m.code = 'system'
ON CONFLICT (key) DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_items WHERE key = 'system.audit_logs';");
        }
    }
}
