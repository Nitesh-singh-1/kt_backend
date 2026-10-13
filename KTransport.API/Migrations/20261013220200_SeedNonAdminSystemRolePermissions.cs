using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// Migration C: Seed semantic role permissions for non-admin system roles
    /// (operations_manager, supervisor, operator, billing_executive, accountant, viewer)
    /// across all tenants.
    /// Idempotent via ON CONFLICT (role_id, permission_id) DO NOTHING.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261013220200_SeedNonAdminSystemRolePermissions")]
    public partial class SeedNonAdminSystemRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
-- 1. operations_manager
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'operations_manager'
   AND (
     p.key LIKE 'consignments.%'
     OR p.key LIKE 'trips.%'
     OR p.key LIKE 'empty_trips.%'
     OR p.key LIKE 'pod.%'
     OR p.key LIKE 'delivery_settlement.%'
     OR p.key LIKE 'trip_settlement.%'
     OR p.key LIKE 'master_data.fleet.%'
     OR p.key LIKE 'master_data.compliance.%'
     OR p.key LIKE 'tracking.%'
     OR p.key LIKE 'reports.booking_register%'
     OR p.key IN ('dashboard.view', 'reports.view', 'vendors.view')
   )
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- 2. supervisor
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'supervisor'
   AND (
     p.key LIKE '%.view' OR p.action = 'View'
     OR (
       (p.key LIKE 'consignments.%' OR p.key LIKE 'trips.%' OR p.key LIKE 'pod.%')
       AND (p.action IN ('Create', 'Edit') OR p.key LIKE '%.create' OR p.key LIKE '%.edit')
     )
   )
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- 3. operator
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'operator'
   AND (
     (
       (p.key LIKE 'consignments.%' OR p.key LIKE 'trips.%')
       AND (p.action IN ('Create', 'Edit', 'View') OR p.key LIKE '%.create' OR p.key LIKE '%.edit' OR p.key LIKE '%.view')
     )
     OR (p.key LIKE 'pod.%' AND (p.action IN ('View', 'Edit') OR p.key IN ('pod.view', 'pod.edit')))
     OR p.key IN ('dashboard.view', 'master_data.parties.view', 'master_data.fleet.view')
   )
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- 4. billing_executive
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'billing_executive'
   AND (
     p.key LIKE 'billing.%'
     OR p.key LIKE 'reports.booking_register%'
     OR p.key IN ('consignments.all.view', 'consignments.view', 'reports.view', 'dashboard.view')
   )
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- 5. accountant
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'accountant'
   AND (
     (p.key LIKE 'billing.%' AND (p.key LIKE '%.view' OR p.key LIKE '%.export' OR p.action IN ('View', 'Export')))
     OR p.key LIKE 'reports.%'
     OR p.key LIKE 'master_data.driver_ledger.%'
     OR p.key IN ('master_data.loans.view', 'dashboard.view')
   )
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- 6. viewer
INSERT INTO role_permissions (tenant_id, role_name, role_id, permission_key, permission_id, granted_at)
SELECT r.tenant_id, r.code, r.id, p.key, p.id, CURRENT_TIMESTAMP
  FROM roles r
  CROSS JOIN permissions p
 WHERE r.code = 'viewer'
   AND (p.key LIKE '%.view' OR p.action = 'View')
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
