using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-043 reviewer follow-up: close the sub-user visibility gap exposed
    /// when TASK-043 removed the parent-module fallback from
    /// MenuCatalogService.BuildMenuIdSetFromTablesAsync.
    ///
    /// The 5 renamed/collapsed features (master_data.driver_ledger,
    /// master_data.vehicle_claims, master_data.vendor_rates, trip_settlement,
    /// delivery_settlement) never existed as granular keys in any tenant's
    /// original roleOverridesJson. TASK-040 Phase 1 backfill only expanded
    /// what was there, so non-super roles never received a `.view` grant on
    /// these keys. The old C# tree masked this via `IsEnabled("master_data")`
    /// (and friends) OR-fallback; Phase 2 flip to tables surfaces the gap and
    /// sub-users (dispatcher / billing_operator / fleet_manager / viewer)
    /// would see those menu entries disappear.
    ///
    /// Fix: grant `.view` on each renamed feature to every (tenant, role)
    /// that already holds `.view` on the appropriate parent module. Mutation
    /// grants (`.create` / `.edit` / `.delete`) are deliberately NOT seeded —
    /// Phase 2 of the migration is for READ visibility only; operators can
    /// grant mutations through the SaaS admin UI after per-role review.
    ///
    /// Mappings (parent_module.view -> child_feature.view):
    ///   master_data.view      -> master_data.driver_ledger.view
    ///                            master_data.vehicle_claims.view
    ///                            master_data.vendor_rates.view
    ///   trips.view            -> trip_settlement.view
    ///   consignments.view     -> delivery_settlement.view
    ///
    /// Idempotent via a NOT EXISTS guard matching the semantics of the
    /// `role_permissions (tenant_id, role_name, permission_key) WHERE revoked_at IS NULL`
    /// partial unique index. Safe to re-run.
    /// </summary>
    public partial class BackfillRenamedFeatureGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- master_data.view grantees -> master_data.driver_ledger.view
                INSERT INTO role_permissions (tenant_id, role_name, permission_key, granted_at, granted_by)
                SELECT DISTINCT rp.tenant_id, rp.role_name, 'master_data.driver_ledger.view', NOW(), NULL::integer
                FROM role_permissions rp
                WHERE rp.revoked_at IS NULL
                  AND rp.permission_key = 'master_data.view'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_permissions existing
                      WHERE existing.tenant_id = rp.tenant_id
                        AND existing.role_name = rp.role_name
                        AND existing.permission_key = 'master_data.driver_ledger.view'
                        AND existing.revoked_at IS NULL
                  );

                -- master_data.view grantees -> master_data.vehicle_claims.view
                INSERT INTO role_permissions (tenant_id, role_name, permission_key, granted_at, granted_by)
                SELECT DISTINCT rp.tenant_id, rp.role_name, 'master_data.vehicle_claims.view', NOW(), NULL::integer
                FROM role_permissions rp
                WHERE rp.revoked_at IS NULL
                  AND rp.permission_key = 'master_data.view'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_permissions existing
                      WHERE existing.tenant_id = rp.tenant_id
                        AND existing.role_name = rp.role_name
                        AND existing.permission_key = 'master_data.vehicle_claims.view'
                        AND existing.revoked_at IS NULL
                  );

                -- master_data.view grantees -> master_data.vendor_rates.view
                INSERT INTO role_permissions (tenant_id, role_name, permission_key, granted_at, granted_by)
                SELECT DISTINCT rp.tenant_id, rp.role_name, 'master_data.vendor_rates.view', NOW(), NULL::integer
                FROM role_permissions rp
                WHERE rp.revoked_at IS NULL
                  AND rp.permission_key = 'master_data.view'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_permissions existing
                      WHERE existing.tenant_id = rp.tenant_id
                        AND existing.role_name = rp.role_name
                        AND existing.permission_key = 'master_data.vendor_rates.view'
                        AND existing.revoked_at IS NULL
                  );

                -- trips.view grantees -> trip_settlement.view
                INSERT INTO role_permissions (tenant_id, role_name, permission_key, granted_at, granted_by)
                SELECT DISTINCT rp.tenant_id, rp.role_name, 'trip_settlement.view', NOW(), NULL::integer
                FROM role_permissions rp
                WHERE rp.revoked_at IS NULL
                  AND rp.permission_key = 'trips.view'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_permissions existing
                      WHERE existing.tenant_id = rp.tenant_id
                        AND existing.role_name = rp.role_name
                        AND existing.permission_key = 'trip_settlement.view'
                        AND existing.revoked_at IS NULL
                  );

                -- consignments.view grantees -> delivery_settlement.view
                INSERT INTO role_permissions (tenant_id, role_name, permission_key, granted_at, granted_by)
                SELECT DISTINCT rp.tenant_id, rp.role_name, 'delivery_settlement.view', NOW(), NULL::integer
                FROM role_permissions rp
                WHERE rp.revoked_at IS NULL
                  AND rp.permission_key = 'consignments.view'
                  AND NOT EXISTS (
                      SELECT 1 FROM role_permissions existing
                      WHERE existing.tenant_id = rp.tenant_id
                        AND existing.role_name = rp.role_name
                        AND existing.permission_key = 'delivery_settlement.view'
                        AND existing.revoked_at IS NULL
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse by hard-deleting the rows this migration created. Only
            // drops auto-generated rows (granted_by IS NULL) for the 5 renamed
            // features; any grants added later via admin UI or API (with a
            // real granted_by) are preserved.
            migrationBuilder.Sql(@"
                DELETE FROM role_permissions
                WHERE granted_by IS NULL
                  AND revoked_at IS NULL
                  AND permission_key IN (
                      'master_data.driver_ledger.view',
                      'master_data.vehicle_claims.view',
                      'master_data.vendor_rates.view',
                      'trip_settlement.view',
                      'delivery_settlement.view'
                  );
            ");
        }
    }
}
