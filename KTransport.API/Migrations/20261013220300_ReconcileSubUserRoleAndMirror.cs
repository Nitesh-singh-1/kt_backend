using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Migration D:
    /// 1. Reassign jugal and any other sub_user users to the viewer role.
    /// 2. Disable the sub_user role so it is not reusable.
    /// 3. Dual-write legacy users.role string column.
    /// 4. Reconcile tenant_entitlement_subscriptions.enabled_feature_keys
    ///    legacy mirror column from active tenant_modules for all tenants.
    /// Idempotent.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261013220300_ReconcileSubUserRoleAndMirror")]
    public partial class ReconcileSubUserRoleAndMirror : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1. If any user already has an active viewer role, remove the redundant sub_user role_assignment first
DELETE FROM user_roles ur
USING roles r_source, roles r_target, user_roles ur_existing
WHERE ur.role_id = r_source.id
  AND r_source.code = 'sub_user'
  AND r_target.code = 'viewer'
  AND r_target.tenant_id = r_source.tenant_id
  AND ur_existing.user_id = ur.user_id
  AND ur_existing.role_id = r_target.id
  AND ur_existing.revoked_at IS NULL;

-- Reassign remaining sub_user users to viewer role in their tenant
UPDATE user_roles ur
   SET role_id = r_target.id
  FROM roles r_source, roles r_target
 WHERE ur.role_id = r_source.id
   AND r_source.code = 'sub_user'
   AND r_target.code = 'viewer'
   AND r_target.tenant_id = r_source.tenant_id;

-- 2. Disable the sub_user role so it is not reusable
UPDATE roles
   SET is_active = false
 WHERE code = 'sub_user';

-- 3. Dual-write legacy users.role string column
UPDATE users
   SET role = 'viewer'
 WHERE LOWER(TRIM(role)) = 'sub_user';

-- 4. Reconcile legacy mirror for ALL tenants from current active tenant_modules
UPDATE tenant_entitlement_subscriptions s
   SET enabled_feature_keys = COALESCE(mc.codes, ARRAY[]::text[])
  FROM (
    SELECT tm.tenant_id,
           array_agg(m.code ORDER BY m.code) AS codes
      FROM tenant_modules tm
      JOIN modules m ON m.id = tm.module_id
     WHERE tm.enabled_until IS NULL
     GROUP BY tm.tenant_id
  ) mc
 WHERE s.effective_until IS NULL
   AND s.tenant_id = mc.tenant_id;

-- Also handle tenants with an active subscription but zero active modules
UPDATE tenant_entitlement_subscriptions s
   SET enabled_feature_keys = ARRAY[]::text[]
 WHERE s.effective_until IS NULL
   AND NOT EXISTS (
       SELECT 1 FROM tenant_modules tm
       WHERE tm.tenant_id = s.tenant_id
         AND tm.enabled_until IS NULL
   )
   AND s.enabled_feature_keys IS DISTINCT FROM ARRAY[]::text[];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way reconciliation migration — Down is a deliberate no-op.
        }
    }
}
