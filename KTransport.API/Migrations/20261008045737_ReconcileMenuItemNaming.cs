using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-043 Phase 2: reconciles the menu_items catalog's non-canonical keys
    /// (seeded in TASK-041 Phase 1 to mirror the C# tree verbatim) to the
    /// authoritative permissions-catalog form seeded by SeedPermissionsCatalog.
    ///
    /// Renames applied (all idempotent via WHERE-match):
    ///   master_data.driverledger        -> master_data.driver_ledger
    ///   master_data.vehicleclaims       -> master_data.vehicle_claims
    ///   master_data.vendorrates         -> master_data.vendor_rates
    ///   trips.settlement                -> trip_settlement
    ///   consignments.delivery_settlement -> delivery_settlement
    ///
    /// Also:
    ///   DELETE trips.all                (collapsed into the trips parent;
    ///                                    trips.path set to '/trips' so the
    ///                                    parent group remains clickable)
    ///   UPDATE permission_key values for renamed rows to match the catalog
    ///   (trips.settle -> trip_settlement.view,
    ///    consignments.settle -> delivery_settlement.view).
    /// </summary>
    public partial class ReconcileMenuItemNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Pre-verified via the Step 1 inventory in .agent/status/backend.md:
            //   * every NEW key already exists in `permissions` as a feature_key
            //   * every OLD key is absent from `permissions`
            //   * no role_permissions row references the OLD action-split form

            migrationBuilder.Sql(@"
                -- 1. Rename child keys (parent_key = 'master_data')
                UPDATE menu_items SET key = 'master_data.driver_ledger',  permission_key = 'master_data.driver_ledger.view'  WHERE key = 'master_data.driverledger';
                UPDATE menu_items SET key = 'master_data.vehicle_claims', permission_key = 'master_data.vehicle_claims.view' WHERE key = 'master_data.vehicleclaims';
                UPDATE menu_items SET key = 'master_data.vendor_rates',   permission_key = 'master_data.vendor_rates.view'   WHERE key = 'master_data.vendorrates';

                -- 2. Rename trip_settlement (parent_key = 'trips')
                UPDATE menu_items SET key = 'trip_settlement', permission_key = 'trip_settlement.view' WHERE key = 'trips.settlement';

                -- 3. Rename delivery_settlement (parent_key stays 'consignments')
                UPDATE menu_items SET key = 'delivery_settlement', permission_key = 'delivery_settlement.view' WHERE key = 'consignments.delivery_settlement';

                -- 4. Collapse trips.all into the trips parent, make parent clickable
                DELETE FROM menu_items WHERE key = 'trips.all';
                UPDATE menu_items SET path = '/trips' WHERE key = 'trips' AND (path IS NULL OR path = '');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE menu_items SET key = 'master_data.driverledger',  permission_key = 'fleet.view'   WHERE key = 'master_data.driver_ledger';
                UPDATE menu_items SET key = 'master_data.vehicleclaims', permission_key = 'fleet.view'   WHERE key = 'master_data.vehicle_claims';
                UPDATE menu_items SET key = 'master_data.vendorrates',   permission_key = 'vendors.view' WHERE key = 'master_data.vendor_rates';
                UPDATE menu_items SET key = 'trips.settlement',          permission_key = 'trips.settle' WHERE key = 'trip_settlement';
                UPDATE menu_items SET key = 'consignments.delivery_settlement', permission_key = 'consignments.settle' WHERE key = 'delivery_settlement';
                UPDATE menu_items SET path = NULL WHERE key = 'trips';
                INSERT INTO menu_items (""key"", parent_key, title, path, icon, permission_key, badge, display_order, visibility_rule, is_active)
                VALUES ('trips.all', 'trips', 'Manifest & Dispatch (Challans)', '/trips', 'truck', 'trips.view', NULL, 0, NULL, TRUE)
                ON CONFLICT (""key"") DO NOTHING;
            ");
        }
    }
}
