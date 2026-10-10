using KTransport.API.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-049 Option B — menu + RBAC normalization.
    ///
    /// Idempotent. Adds the structural FKs that let menu rendering become a
    /// pure JOIN and lets admin role seeding stop fuzzy-matching on string
    /// prefixes:
    ///
    /// - <c>permissions.module_id</c> FK to <c>modules.id</c> (NOT NULL after backfill).
    /// - <c>menu_items.module_id</c>   FK to <c>modules.id</c> (NOT NULL after backfill).
    /// - <c>role_permissions.permission_id</c>          FK to <c>permissions.id</c> (nullable; new rows set it).
    /// - <c>user_permission_overrides.permission_id</c> FK to <c>permissions.id</c> (nullable; new rows set it).
    ///
    /// Also:
    /// - Ensures a <c>clients</c> module row exists (platform SaaS management
    ///   menu did not previously have one, which would otherwise orphan the
    ///   clients menu_items row during the FAIL-LOUDLY backfill).
    /// - One-time rewrite of legacy aliases <c>gr</c>/<c>challan</c> inside
    ///   <c>tenant_entitlement_subscriptions.enabled_feature_keys</c> to the
    ///   canonical <c>consignments</c>/<c>trips</c>.
    ///
    /// Deliberately kept RAW SQL so the migration is idempotent and does not
    /// churn the ModelSnapshot every time an unrelated column is added. The
    /// legacy string columns (<c>menu_items.key/parent_key/permission_key</c>,
    /// <c>permissions.feature_key</c>, <c>role_permissions.permission_key</c>,
    /// <c>user_permission_overrides.permission_key</c>, and the
    /// <c>tenant_entitlement_subscriptions.enabled_feature_keys</c> text[])
    /// are KEPT through Phase A; a separate Phase B migration will drop them.
    ///
    /// FAIL-LOUDLY policy: any row that cannot be backfilled aborts the entire
    /// migration with a RAISE EXCEPTION so operations never silently null a FK.
    /// </summary>
    [DbContext(typeof(KTransportDbContext))]
    [Migration("20261010103000_MenuRbacNormalization")]
    public partial class MenuRbacNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // 0. Ensure the "clients" module row exists so the menu_items
            //    row key='clients' resolves cleanly during FK backfill.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
INSERT INTO modules (code, name, description, is_premium, is_active, display_order, created_at)
VALUES ('clients', 'Clients & Tenants', 'Platform SaaS tenant and client management', FALSE, TRUE, 100, CURRENT_TIMESTAMP)
ON CONFLICT (code) DO NOTHING;
");

            // ---------------------------------------------------------------
            // 1. permissions.module_id — add nullable, backfill, promote.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
ALTER TABLE permissions ADD COLUMN IF NOT EXISTS module_id INTEGER;

-- Backfill from feature_key prefix via a per-row CASE map.
UPDATE permissions p SET module_id = m.id
FROM modules m
WHERE p.module_id IS NULL
  AND m.code = CASE
    WHEN p.feature_key = 'dashboard'                                                                       THEN 'dashboard'
    WHEN p.feature_key = 'analytics'                                                                       THEN 'analytics'
    WHEN p.feature_key = 'tracking'                                                                        THEN 'tracking'
    WHEN p.feature_key = 'consignments' OR p.feature_key LIKE 'consignments.%'
      OR p.feature_key = 'bilty'        OR p.feature_key LIKE 'bilty.%'
      OR p.feature_key = 'gr'           OR p.feature_key LIKE 'gr.%'                                       THEN 'bilty'
    WHEN p.feature_key = 'delivery_settlement' OR p.feature_key LIKE 'delivery_settlement.%'               THEN 'delivery_settlement'
    WHEN p.feature_key = 'trip_settlement'     OR p.feature_key LIKE 'trip_settlement.%'                   THEN 'trip_settlement'
    WHEN p.feature_key = 'empty_trips' OR p.feature_key LIKE 'empty_trips.%'
      OR p.feature_key = 'trips'       OR p.feature_key LIKE 'trips.%'
      OR p.feature_key = 'challan'     OR p.feature_key LIKE 'challan.%'                                   THEN 'trips'
    WHEN p.feature_key = 'pod'   OR p.feature_key LIKE 'pod.%'                                             THEN 'pod'
    WHEN p.feature_key = 'billing'    OR p.feature_key LIKE 'billing.%'
      OR p.feature_key = 'bill_book'                                                                        THEN 'billing'
    WHEN p.feature_key = 'master_data' OR p.feature_key LIKE 'master_data.%'
      OR p.feature_key = 'masterdata'  OR p.feature_key LIKE 'masterdata.%'                                THEN 'master_data'
    WHEN p.feature_key = 'reports'  OR p.feature_key LIKE 'reports.%'                                      THEN 'reports'
    WHEN p.feature_key = 'vendors'  OR p.feature_key LIKE 'vendors.%'                                      THEN 'vendors'
    WHEN p.feature_key = 'claims'   OR p.feature_key LIKE 'claims.%'                                       THEN 'claims'
    WHEN p.feature_key = 'quotations' OR p.feature_key LIKE 'quotations.%'                                 THEN 'quotations'
    WHEN p.feature_key = 'clients'  OR p.feature_key LIKE 'clients.%'
      OR p.feature_key = 'saas'     OR p.feature_key LIKE 'saas.%'                                         THEN 'clients'
    WHEN p.feature_key = 'system'      OR p.feature_key LIKE 'system.%'
      OR p.feature_key = 'users'       OR p.feature_key LIKE 'users.%'
      OR p.feature_key = 'settings'    OR p.feature_key LIKE 'settings.%'
      OR p.feature_key = 'auth'        OR p.feature_key LIKE 'auth.%'
      OR p.feature_key = 'tenant'      OR p.feature_key LIKE 'tenant.%'
      OR p.feature_key = 'roles'       OR p.feature_key LIKE 'roles.%'
      OR p.feature_key = 'designations' OR p.feature_key LIKE 'designations.%'
      OR p.feature_key = 'modules'     OR p.feature_key LIKE 'modules.%'
      OR p.feature_key = 'tenant_modules' OR p.feature_key LIKE 'tenant_modules.%'                         THEN 'system'
    ELSE NULL
  END;

-- FAIL LOUDLY if any permission row stayed NULL.
DO $do$
DECLARE
    orphan INTEGER;
    sample TEXT;
BEGIN
    SELECT COUNT(*), COALESCE(STRING_AGG(DISTINCT feature_key, ','), '') INTO orphan, sample
      FROM permissions WHERE module_id IS NULL;
    IF orphan > 0 THEN
        RAISE EXCEPTION 'MenuRbacNormalization: % permissions rows have NULL module_id after backfill (feature_keys=[%]). Aborting — add their module to the modules catalog or extend the backfill CASE.', orphan, sample;
    END IF;
END $do$;

ALTER TABLE permissions ALTER COLUMN module_id SET NOT NULL;

DO $do$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_permissions_module') THEN
    ALTER TABLE permissions
      ADD CONSTRAINT fk_permissions_module FOREIGN KEY (module_id) REFERENCES modules(id);
  END IF;
END $do$;

CREATE INDEX IF NOT EXISTS permissions_module_id_idx ON permissions(module_id);
");

            // ---------------------------------------------------------------
            // 2. menu_items.module_id — add nullable, backfill, promote.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
ALTER TABLE menu_items ADD COLUMN IF NOT EXISTS module_id INTEGER;

UPDATE menu_items mi SET module_id = m.id
FROM modules m
WHERE mi.module_id IS NULL
  AND m.code = CASE
    WHEN mi.key = 'dashboard'                                   THEN 'dashboard'
    WHEN mi.key = 'analytics'                                   THEN 'analytics'
    WHEN mi.key = 'tracking'                                    THEN 'tracking'
    WHEN mi.key = 'consignments'                                THEN 'bilty'
    WHEN mi.parent_key = 'consignments' AND mi.key = 'delivery_settlement' THEN 'delivery_settlement'
    WHEN mi.parent_key = 'consignments'                         THEN 'bilty'
    WHEN mi.key = 'trips'                                       THEN 'trips'
    WHEN mi.parent_key = 'trips' AND mi.key = 'trip_settlement' THEN 'trip_settlement'
    WHEN mi.parent_key = 'trips'                                THEN 'trips'
    WHEN mi.key = 'pod'                                         THEN 'pod'
    WHEN mi.key = 'billing' OR mi.parent_key = 'billing'        THEN 'billing'
    WHEN mi.key = 'master_data' OR mi.parent_key = 'master_data' THEN 'master_data'
    WHEN mi.key = 'vendors'                                     THEN 'vendors'
    WHEN mi.key = 'claims'                                      THEN 'claims'
    WHEN mi.key = 'quotations'                                  THEN 'quotations'
    WHEN mi.key = 'reports'                                     THEN 'reports'
    WHEN mi.key = 'clients'                                     THEN 'clients'
    WHEN mi.key = 'system' OR mi.parent_key = 'system'          THEN 'system'
    WHEN mi.key = 'delivery_settlement'                         THEN 'delivery_settlement'
    WHEN mi.key = 'trip_settlement'                             THEN 'trip_settlement'
    WHEN mi.key = 'empty_trips'                                 THEN 'trips'
    ELSE NULL
  END;

-- Stash unresolved rows to a scratch table and FAIL LOUDLY.
DO $do$
DECLARE
    orphan INTEGER;
    sample TEXT;
BEGIN
    SELECT COUNT(*), COALESCE(STRING_AGG(key, ','), '')
      INTO orphan, sample
      FROM menu_items WHERE module_id IS NULL;
    IF orphan > 0 THEN
        -- Materialize for operator inspection.
        CREATE TABLE IF NOT EXISTS menu_items_backfill_unresolved (LIKE menu_items INCLUDING ALL);
        INSERT INTO menu_items_backfill_unresolved SELECT * FROM menu_items WHERE module_id IS NULL;
        RAISE EXCEPTION 'MenuRbacNormalization: % menu_items rows have NULL module_id after backfill (keys=[%]). See menu_items_backfill_unresolved. Aborting.', orphan, sample;
    END IF;
END $do$;

ALTER TABLE menu_items ALTER COLUMN module_id SET NOT NULL;

DO $do$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_menu_items_module') THEN
    ALTER TABLE menu_items
      ADD CONSTRAINT fk_menu_items_module FOREIGN KEY (module_id) REFERENCES modules(id);
  END IF;
END $do$;

CREATE INDEX IF NOT EXISTS menu_items_module_id_idx ON menu_items(module_id);
");

            // ---------------------------------------------------------------
            // 3. role_permissions.permission_id — add, backfill, FK (nullable).
            //    Nullable because legacy rows pre-dating the permissions
            //    catalog may still exist; new inserts set it.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
ALTER TABLE role_permissions ADD COLUMN IF NOT EXISTS permission_id INTEGER;

UPDATE role_permissions rp SET permission_id = p.id
FROM permissions p
WHERE rp.permission_id IS NULL AND p.key = rp.permission_key;

-- FAIL LOUDLY: active role_permissions rows must resolve.
DO $do$
DECLARE
    orphan INTEGER;
    sample TEXT;
BEGIN
    SELECT COUNT(*), COALESCE(STRING_AGG(DISTINCT permission_key, ','), '')
      INTO orphan, sample
      FROM role_permissions
      WHERE permission_id IS NULL AND revoked_at IS NULL;
    IF orphan > 0 THEN
        RAISE EXCEPTION 'MenuRbacNormalization: % active role_permissions rows have unresolved permission_key (keys=[%]). Aborting — add the missing permission(s) to the catalog.', orphan, sample;
    END IF;
END $do$;

DO $do$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_role_permissions_permission') THEN
    ALTER TABLE role_permissions
      ADD CONSTRAINT fk_role_permissions_permission FOREIGN KEY (permission_id) REFERENCES permissions(id);
  END IF;
END $do$;

CREATE INDEX IF NOT EXISTS role_permissions_permission_id_idx ON role_permissions(permission_id);
");

            // ---------------------------------------------------------------
            // 4. user_permission_overrides.permission_id — same pattern.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
ALTER TABLE user_permission_overrides ADD COLUMN IF NOT EXISTS permission_id INTEGER;

UPDATE user_permission_overrides upo SET permission_id = p.id
FROM permissions p
WHERE upo.permission_id IS NULL AND p.key = upo.permission_key;

DO $do$
DECLARE
    orphan INTEGER;
    sample TEXT;
BEGIN
    SELECT COUNT(*), COALESCE(STRING_AGG(DISTINCT permission_key, ','), '')
      INTO orphan, sample
      FROM user_permission_overrides
      WHERE permission_id IS NULL AND superseded_by IS NULL;
    IF orphan > 0 THEN
        RAISE EXCEPTION 'MenuRbacNormalization: % active user_permission_overrides rows have unresolved permission_key (keys=[%]). Aborting.', orphan, sample;
    END IF;
END $do$;

DO $do$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_user_perm_overrides_permission') THEN
    ALTER TABLE user_permission_overrides
      ADD CONSTRAINT fk_user_perm_overrides_permission FOREIGN KEY (permission_id) REFERENCES permissions(id);
  END IF;
END $do$;

CREATE INDEX IF NOT EXISTS user_perm_overrides_permission_id_idx ON user_permission_overrides(permission_id);
");

            // ---------------------------------------------------------------
            // 5. One-time cleanup of legacy aliases inside
            //    tenant_entitlement_subscriptions.enabled_feature_keys so the
            //    alias normalization code can be deleted. Dedupe after rewrite.
            // ---------------------------------------------------------------
            migrationBuilder.Sql(@"
UPDATE tenant_entitlement_subscriptions
   SET enabled_feature_keys = (
     SELECT ARRAY(
       SELECT DISTINCT
         CASE
           WHEN v = 'gr'        THEN 'consignments'
           WHEN v = 'gr.list'   THEN 'consignments.all'
           WHEN v = 'gr.entry'  THEN 'consignments.create'
           WHEN v = 'bilty'     THEN 'consignments'
           WHEN v = 'challan'   THEN 'trips'
           WHEN v = 'challan.list'  THEN 'trips'
           WHEN v = 'challan.entry' THEN 'trips'
           WHEN v = 'trip'      THEN 'trips'
           ELSE v
         END
       FROM UNNEST(enabled_feature_keys) AS v
     )
   )
 WHERE enabled_feature_keys && ARRAY['gr','gr.list','gr.entry','bilty','challan','challan.list','challan.entry','trip'];
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // TASK-049 §Y: append-only soft-delete discipline. We do not drop
            // the FK columns or constraints on rollback — a redeploy of the
            // old service code treats permission_id/module_id as unknown
            // columns, which is harmless. If a true rollback is ever needed,
            // run a dedicated reversal migration under human review.
        }
    }
}
