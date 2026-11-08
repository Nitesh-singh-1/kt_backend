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
            // NOTE: role_permissions carries a PARTIAL unique index
            // role_permissions_tenant_role_perm_key ON (tenant_id, role_name,
            // permission_key) WHERE revoked_at IS NULL — separate from
            // uq_role_permissions_role_perm (role_id, permission_id). Legacy rows
            // can have permission_id NULL, so ON CONFLICT (role_id, permission_id)
            // does NOT catch a duplicate on the partial index. The NOT EXISTS
            // guard (matching that index exactly) is what makes this idempotent.
            migrationBuilder.Sql(@"
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'admin'
   AND NOT EXISTS (
     SELECT 1 FROM role_permissions rp
      WHERE rp.tenant_id = r.tenant_id
        AND rp.role_name = r.code
        AND rp.permission_key = p.key
        AND rp.revoked_at IS NULL
   )
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
