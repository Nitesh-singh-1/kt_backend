using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KTransport.API.Migrations
{
    /// <summary>
    /// TASK-041 Phase 1: idempotent seed of the global menu_items catalog.
    ///
    /// FROZEN SNAPSHOT (TASK-054 fresh-replay fix): this migration inlines EXACTLY
    /// the 36 rows that existed when it was authored (2026-10-07). It must NOT read
    /// the mutable <c>MenuCatalogSeedData.Rows</c> list — that list has since grown
    /// to include accounting / roles / audit-log / opening-balances / auto-posting
    /// rows, and emitting those here would insert them BEFORE
    /// <c>20261010103000_MenuRbacNormalization</c> backfills <c>module_id</c> and
    /// BEFORE their modules exist, orphaning the backfill and aborting the replay.
    /// Every late-added row is inserted by its OWN later migration (with module_id
    /// set): trips.challan (20261012180000), system.roles (20261014120000),
    /// system.audit_logs (20261101120000), accounting.* (20261103000000 /
    /// 20261104000000 / 20261105193900). Runtime DataSeeder reconciles the FULL
    /// current catalog on boot; migrations stay frozen-historical.
    ///
    /// Uses ON CONFLICT (key) DO NOTHING so re-running is safe.
    /// </summary>
    public partial class SeedMenuItemsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
INSERT INTO menu_items (""key"", parent_key, title, path, icon, permission_key, badge, display_order, visibility_rule, is_active) VALUES
('dashboard', NULL, 'Dashboard', '/dashboard', 'home', 'dashboard.view', NULL, 0, NULL, TRUE),
('analytics', NULL, 'Business Analytics', '/analytics', 'barChart', 'analytics.view', NULL, 1, NULL, TRUE),
('consignments', NULL, 'Bilty / GR Booking', NULL, 'package', 'consignments.module', NULL, 2, NULL, TRUE),
('quotations', NULL, 'Quotations & Enquiries', '/quotations', 'fileText', 'quotations.view', NULL, 3, NULL, TRUE),
('trips', NULL, 'Manifest & Dispatch', '/trips', 'truck', 'trips.view', NULL, 4, NULL, TRUE),
('pod', NULL, 'POD & Deliveries', '/pod', 'fileText', 'pod.view', NULL, 5, NULL, TRUE),
('billing', NULL, 'Freight Invoicing & Billing', NULL, 'fileText', 'billing.view', NULL, 6, NULL, TRUE),
('master_data', NULL, 'Master Data', NULL, 'cog', 'masterdata.module', NULL, 7, NULL, TRUE),
('vendors', NULL, 'Market Vendors & Hire', '/vendors', 'truck', 'vendors.view', NULL, 8, NULL, TRUE),
('claims', NULL, 'Damage & Claims', '/claims', 'info', 'claims.view', NULL, 9, NULL, TRUE),
('reports', NULL, 'Reports & Analytics', '/reports', 'barChart', 'reports.view', NULL, 10, 'report_entitlement', TRUE),
('tracking', NULL, 'Live GPS Tracker', '/tracking', 'info', 'tracking.view', NULL, 11, NULL, TRUE),
('clients', NULL, 'Client Management', '/clients', 'lock', 'saas.tenants.manage', 'SaaS', 12, NULL, TRUE),
('system', NULL, 'System & Settings', NULL, 'settings', NULL, NULL, 13, NULL, TRUE),
('consignments.create', 'consignments', 'New Bilty (GR Booking)', '/shipments/create', 'package', 'consignments.create', NULL, 0, NULL, TRUE),
('consignments.all', 'consignments', 'All Bilties (GR Registry)', '/shipments', 'fileText', 'consignments.view', NULL, 1, NULL, TRUE),
('delivery_settlement', 'consignments', 'Delivery Settlement', '/delivery-settlement', 'checkCircle', 'delivery_settlement.view', NULL, 2, NULL, TRUE),
('empty_trips', 'trips', 'Empty Trip Log', '/empty-trips', 'truck', 'trips.view', NULL, 1, NULL, TRUE),
('trip_settlement', 'trips', 'Trip Settlement', '/trip-settlement', 'dollarSign', 'trip_settlement.view', NULL, 2, NULL, TRUE),
('billing.bill_book', 'billing', 'Bill Book (Consolidated)', '/bill-book', 'fileText', 'billing.view', 'Freight Bill', 0, NULL, TRUE),
('billing.invoices', 'billing', 'Freight Invoices', '/billing', 'fileText', 'billing.view', NULL, 1, NULL, TRUE),
('billing.receipts', 'billing', 'Money Receipts (MR)', '/receipts', 'fileText', 'billing.view', 'Paid MR', 2, NULL, TRUE),
('master_data.parties', 'master_data', 'Party Directory', '/customers', 'fileText', 'master_data.parties.view', NULL, 0, NULL, TRUE),
('master_data.fleet', 'master_data', 'Fleet & Stations', '/fleet', 'truck', 'master_data.fleet.view', NULL, 1, NULL, TRUE),
('master_data.compliance', 'master_data', 'Fleet Compliance', '/fleet/compliance', 'info', 'master_data.compliance.view', NULL, 2, NULL, TRUE),
('master_data.tyres', 'master_data', 'Tyre Management', '/tyres', 'truck', 'master_data.tyres.view', NULL, 3, NULL, TRUE),
('master_data.spares', 'master_data', 'Spare Parts Stock', '/spare-parts', 'cog', 'master_data.spares.view', NULL, 4, NULL, TRUE),
('master_data.loans', 'master_data', 'Vehicle EMI / Loans', '/vehicle-loans', 'barChart', 'master_data.loans.view', NULL, 5, NULL, TRUE),
('master_data.driver_ledger', 'master_data', 'Driver Ledger', '/driver-ledger', 'users', 'master_data.driver_ledger.view', NULL, 6, NULL, TRUE),
('master_data.vehicle_claims', 'master_data', 'Vehicle Insurance Claims', '/vehicle-claims', 'info', 'master_data.vehicle_claims.view', NULL, 7, NULL, TRUE),
('master_data.rates', 'master_data', 'Rate Contracts', '/rates', 'fileText', 'master_data.rates.view', NULL, 8, NULL, TRUE),
('master_data.vendor_rates', 'master_data', 'Vendor Hire Rates', '/vendor-rates', 'truck', 'master_data.vendor_rates.view', NULL, 9, NULL, TRUE),
('system.users', 'system', 'Manage Users & Access', '/users', 'users', 'users.manage', NULL, 0, NULL, TRUE),
('system.settings', 'system', 'SaaS Configuration', '/settings', 'settings', 'settings.manage', NULL, 2, NULL, TRUE),
('system.onboard', 'system', 'Tenant Onboarding', '/onboard', 'info', 'tenant.onboard', NULL, 3, NULL, TRUE),
('system.forgot_password', 'system', 'Forgot Password', '/forgot-password', 'lock', 'auth.password_reset', NULL, 4, NULL, TRUE)
ON CONFLICT (""key"") DO NOTHING;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM menu_items;");
        }
    }
}
