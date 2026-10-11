using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-046 Phase 1 — the big backfill:
    ///
    /// Part 1: seed 7 system roles per tenant (admin, operations_manager,
    ///         supervisor, operator, billing_executive, accountant, viewer).
    ///         Idempotent via roles_tenant_code_uq.
    ///
    /// Part 2: seed role_permissions grants per role per tenant, scoped to
    ///         the modules the tenant has enabled (tenant_modules). The
    ///         admin role gets all catalog permissions for its enabled
    ///         modules. Idempotent via role_permissions_tenant_role_perm_key.
    ///
    /// Part 3: backfill user_roles M2M from users.role string. The string
    ///         column stays forever per §Y and is kept dual-written on
    ///         user create/update.
    ///
    /// Part 4: populate role_permissions.role_id from (tenant_id, role_name).
    ///         role_name stays populated forever per §Y.
    ///
    /// Zero DELETEs — every statement is append-only / idempotent. The
    /// tenant_entitlement_subscriptions.enabled_feature_keys column is NOT
    /// touched.
    /// </summary>
    public partial class BackfillRolesAndUserRoles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // -------- Part 1: seed system roles per tenant ------------------
            migrationBuilder.Sql(@"
INSERT INTO roles (tenant_id, code, name, description, is_system, is_active, created_at)
SELECT t.id, r.code, r.name, r.description, TRUE, TRUE, CURRENT_TIMESTAMP
  FROM tenants t
 CROSS JOIN (VALUES
    ('admin',              'Administrator',       'Full tenant management + all enabled modules'),
    ('operations_manager', 'Operations Manager',  'Operational modules + reports'),
    ('supervisor',         'Supervisor',          'Approve / edit operational docs'),
    ('operator',           'Operator',            'Create own documents only'),
    ('billing_executive',  'Billing Executive',   'Billing + invoices CRUD'),
    ('accountant',         'Accountant',          'Financial reports + view-only operations'),
    ('viewer',             'Viewer',              'View-only everything the tenant has')
 ) AS r(code, name, description)
ON CONFLICT (tenant_id, code) DO NOTHING;
");

            // -------- Part 2: seed role_permissions per (tenant, role) ------
            // The seed_grants rule from the contract, encoded as a predicate on
            // permissions.key / feature_key / action. Each INSERT is scoped to
            // the modules the tenant has enabled (tenant_modules). All INSERTs
            // use ON CONFLICT ... DO NOTHING so re-running the migration is a
            // no-op. role_name stays populated per §Y.

            // Helper: a view-like CTE that lists (tenant_id, module_code) for
            // the modules that tenant currently has active, which we JOIN to
            // narrow grants.
            // For each role we encode its seed_grants predicate:
            // - admin:              every catalog permission whose feature's first segment is in the tenant's modules
            // - operations_manager: consignments.*, trips.*, pod.*, reports.view, reports.*.export
            // - supervisor:         consignments.view/edit/approve, pod.view/edit/approve
            // - operator:           consignments.view/create, pod.view/create
            // - billing_executive:  billing.*, consignments.view
            // - accountant:         reports.*, *.view (view-only across consignments/trips/billing)
            // - viewer:             *.view for every enabled feature

            migrationBuilder.Sql(@"
-- CTE: active modules per tenant (code set).
WITH tenant_mods AS (
    SELECT tm.tenant_id, m.code AS module_code
      FROM tenant_modules tm
      JOIN modules m ON m.id = tm.module_id
     WHERE tm.enabled_until IS NULL AND tm.is_enabled = TRUE
),
-- Map every permission to its 'root' module code (first segment of feature_key,
-- with hand-waved aliasing for the handful of non-matching ones).
perm_mod AS (
    SELECT p.id, p.key, p.feature_key, p.action,
           CASE
             WHEN p.feature_key LIKE 'consignments%'         THEN 'bilty'
             WHEN p.feature_key = 'delivery_settlement'      THEN 'delivery_settlement'
             WHEN p.feature_key = 'trip_settlement'          THEN 'trip_settlement'
             WHEN p.feature_key LIKE 'empty_trips%'          THEN 'trips'
             WHEN p.feature_key LIKE 'trips%'                THEN 'trips'
             WHEN p.feature_key LIKE 'billing%'              THEN 'billing'
             WHEN p.feature_key LIKE 'pod%'                  THEN 'pod'
             WHEN p.feature_key LIKE 'reports%'              THEN 'reports'
             WHEN p.feature_key LIKE 'master_data%'          THEN 'master_data'
             WHEN p.feature_key LIKE 'quotations%'           THEN 'quotations'
             WHEN p.feature_key LIKE 'vendors%'              THEN 'vendors'
             WHEN p.feature_key LIKE 'claims%'               THEN 'claims'
             WHEN p.feature_key LIKE 'tracking%'             THEN 'tracking'
             WHEN p.feature_key LIKE 'analytics%'            THEN 'analytics'
             WHEN p.feature_key LIKE 'dashboard%'            THEN 'dashboard'
             WHEN p.feature_key LIKE 'system%'               THEN 'system'
             ELSE NULL
           END AS module_code
      FROM permissions p
)
INSERT INTO role_permissions (tenant_id, role_name, permission_key, granted_at)
-- admin: all permissions in enabled modules
SELECT tm.tenant_id, 'admin', pm.key, CURRENT_TIMESTAMP
  FROM tenant_mods tm
  JOIN perm_mod pm ON pm.module_code = tm.module_code
UNION
-- operations_manager
SELECT tm.tenant_id, 'operations_manager', pm.key, CURRENT_TIMESTAMP
  FROM tenant_mods tm
  JOIN perm_mod pm ON pm.module_code = tm.module_code
 WHERE pm.module_code IN ('bilty','trips','pod','trip_settlement','delivery_settlement','dashboard')
    OR (pm.module_code = 'reports' AND pm.action IN ('View','Export'))
UNION
-- supervisor
SELECT tm.tenant_id, 'supervisor', pm.key, CURRENT_TIMESTAMP
  FROM tenant_mods tm
  JOIN perm_mod pm ON pm.module_code = tm.module_code
 WHERE (pm.module_code = 'bilty' AND pm.action IN ('View','Edit','Approve'))
    OR (pm.module_code = 'pod'   AND pm.action IN ('View','Edit','Approve'))
    OR (pm.module_code = 'dashboard' AND pm.action = 'View')
UNION
-- operator
SELECT tm.tenant_id, 'operator', pm.key, CURRENT_TIMESTAMP
  FROM tenant_mods tm
  JOIN perm_mod pm ON pm.module_code = tm.module_code
 WHERE (pm.module_code = 'bilty' AND pm.action IN ('View','Create'))
    OR (pm.module_code = 'pod'   AND pm.action IN ('View','Create'))
    OR (pm.module_code = 'dashboard' AND pm.action = 'View')
UNION
-- billing_executive
SELECT tm.tenant_id, 'billing_executive', pm.key, CURRENT_TIMESTAMP
  FROM tenant_mods tm
  JOIN perm_mod pm ON pm.module_code = tm.module_code
 WHERE pm.module_code = 'billing'
    OR (pm.module_code = 'bilty'      AND pm.action = 'View')
    OR (pm.module_code = 'dashboard'  AND pm.action = 'View')
UNION
-- accountant
SELECT tm.tenant_id, 'accountant', pm.key, CURRENT_TIMESTAMP
  FROM tenant_mods tm
  JOIN perm_mod pm ON pm.module_code = tm.module_code
 WHERE pm.module_code = 'reports'
    OR (pm.module_code IN ('bilty','trips','billing') AND pm.action = 'View')
    OR (pm.module_code = 'dashboard' AND pm.action = 'View')
UNION
-- viewer
SELECT tm.tenant_id, 'viewer', pm.key, CURRENT_TIMESTAMP
  FROM tenant_mods tm
  JOIN perm_mod pm ON pm.module_code = tm.module_code
 WHERE pm.action = 'View'
ON CONFLICT (tenant_id, role_name, permission_key) WHERE revoked_at IS NULL DO NOTHING;
");

            // -------- Part 3: backfill user_roles from users.role string ----
            // For every active user, find the matching role in their tenant
            // (case-insensitive). If none, insert a custom role with that code
            // (IsSystem=false) and then assign it. Idempotent via the partial
            // unique index user_roles_tenant_user_role_active_uq.
            migrationBuilder.Sql(@"
-- Step 3a-pre: ensure a role exists for every DISTINCT role_permissions.role_name
-- (which may include legacy roles like 'dispatcher', 'billing_operator',
-- 'fleet_manager' seeded by TASK-043 even when no user has that string).
INSERT INTO roles (tenant_id, code, name, description, is_system, is_active, created_at)
SELECT DISTINCT rp.tenant_id, LOWER(rp.role_name), rp.role_name,
       'Backfilled from role_permissions.role_name (TASK-046 Phase 1).',
       FALSE, TRUE, CURRENT_TIMESTAMP
  FROM role_permissions rp
 WHERE rp.role_name IS NOT NULL AND TRIM(rp.role_name) <> ''
ON CONFLICT (tenant_id, code) DO NOTHING;

-- Step 3a: ensure a role exists for every users.role string that is NOT one
-- of the seeded 7. Create as a custom (non-system) role, idempotent.
INSERT INTO roles (tenant_id, code, name, description, is_system, is_active, created_at)
SELECT DISTINCT u.tenant_id, LOWER(TRIM(u.role)), TRIM(u.role),
       'Backfilled custom role from users.role string (TASK-046 Phase 1).',
       FALSE, TRUE, CURRENT_TIMESTAMP
  FROM users u
 WHERE u.role IS NOT NULL
   AND TRIM(u.role) <> ''
   AND u.is_active IS TRUE
   AND LOWER(TRIM(u.role)) NOT IN ('admin','operations_manager','supervisor','operator','billing_executive','accountant','viewer')
ON CONFLICT (tenant_id, code) DO NOTHING;

-- Step 3b: insert user_roles rows (M2M). Case-insensitive match on role.code.
INSERT INTO user_roles (tenant_id, user_id, role_id, assigned_at)
SELECT u.tenant_id, u.id, r.id, CURRENT_TIMESTAMP
  FROM users u
  JOIN roles r
    ON r.tenant_id = u.tenant_id
   AND LOWER(r.code) = LOWER(TRIM(u.role))
 WHERE u.is_active IS TRUE
   AND u.role IS NOT NULL
   AND TRIM(u.role) <> ''
ON CONFLICT (tenant_id, user_id, role_id) WHERE revoked_at IS NULL DO NOTHING;
");

            // -------- Part 4: populate role_permissions.role_id -------------
            migrationBuilder.Sql(@"
UPDATE role_permissions rp
   SET role_id = r.id
  FROM roles r
 WHERE rp.role_id IS NULL
   AND r.tenant_id = rp.tenant_id
   AND LOWER(r.code) = LOWER(rp.role_name);
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // TASK-046 §Y: no row deletes on rollback. The backfill is additive.
        }
    }
}
