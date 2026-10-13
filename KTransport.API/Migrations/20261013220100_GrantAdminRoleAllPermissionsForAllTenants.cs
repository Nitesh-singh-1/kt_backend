using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Migration B: Grant admin role ALL catalog permissions across every tenant,
    /// regardless of currently enabled modules.
    /// Idempotent via ON CONFLICT (role_id, permission_id) DO NOTHING.
    /// Also dual-writes tenant_id, role_name, permission_key per ADR §Y and ITenantScopedEntity.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261013220100_GrantAdminRoleAllPermissionsForAllTenants")]
    public partial class GrantAdminRoleAllPermissionsForAllTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'admin'
ON CONFLICT (role_id, permission_id) DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Additive data seed — Down is a deliberate no-op.
        }
    }
}
