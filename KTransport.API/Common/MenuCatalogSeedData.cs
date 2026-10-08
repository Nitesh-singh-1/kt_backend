namespace KTransport.API.Common
{
    /// <summary>
    /// TASK-041 Phase 1 seed, reconciled to canonical form by TASK-043 Phase 2.
    /// 36 rows (down from 37: trips.all collapsed into the clickable trips
    /// parent). Row keys and permission_key values match the permissions
    /// catalog (SeedPermissionsCatalog). Tests reference this list so they
    /// stay in sync with the live DB after both seed + reconcile migrations.
    /// </summary>
    public static class MenuCatalogSeedData
    {
        public record Row(
            string Key,
            string? ParentKey,
            string Title,
            string? Path,
            string? Icon,
            string? PermissionKey,
            string? Badge,
            int DisplayOrder,
            string? VisibilityRule);

        public static readonly Row[] Rows =
        {
            // ---- Top-level items (parent_key = NULL) ----
            new("dashboard",     null, "Dashboard",                   "/dashboard",  "home",     "dashboard.view",      null,     0, null),
            new("analytics",     null, "Business Analytics",          "/analytics",  "barChart", "dashboard.view",      null,     1, null),
            new("consignments",  null, "Bilty / GR Booking",          null,          "package",  "consignments.module", null,     2, null),
            new("quotations",    null, "Quotations & Enquiries",      "/quotations", "fileText", "quotations.view",     null,     3, null),
            new("trips",         null, "Manifest & Dispatch",         "/trips",      "truck",    "trips.view",          null,     4, null),
            new("pod",           null, "POD & Deliveries",            "/pod",        "fileText", "pod.view",            null,     5, null),
            new("billing",       null, "Freight Invoicing & Billing", null,          "fileText", "billing.view",        null,     6, null),
            new("master_data",   null, "Master Data",                 null,          "cog",      "masterdata.module",   null,     7, null),
            new("vendors",       null, "Market Vendors & Hire",       "/vendors",    "truck",    "vendors.view",        null,     8, null),
            new("claims",        null, "Damage & Claims",             "/claims",     "info",     "claims.view",         null,     9, null),
            new("reports",       null, "Reports & Analytics",         "/reports",    "barChart", "reports.view",        null,    10, "report_entitlement"),
            new("tracking",      null, "Live GPS Tracker",            "/tracking",   "info",     "tracking.view",       null,    11, null),
            new("clients",       null, "Client Management",           "/clients",    "lock",     "saas.tenants.manage", "SaaS",  12, null),
            new("system",        null, "System & Settings",           null,          "settings", null,                  null,    13, null),

            // ---- consignments children ----
            new("consignments.create",   "consignments", "New Bilty (GR Booking)",    "/shipments/create",    "package",     "consignments.create",       null, 0, null),
            new("consignments.all",      "consignments", "All Bilties (GR Registry)", "/shipments",           "fileText",    "consignments.view",         null, 1, null),
            new("delivery_settlement",   "consignments", "Delivery Settlement",       "/delivery-settlement", "checkCircle", "delivery_settlement.view",  null, 2, null),

            // ---- trips children ----
            new("empty_trips",      "trips", "Empty Trip Log",   "/empty-trips",     "truck",      "trips.view",             null, 1, null),
            new("trip_settlement",  "trips", "Trip Settlement",  "/trip-settlement", "dollarSign", "trip_settlement.view",   null, 2, null),

            // ---- billing children ----
            new("billing.bill_book", "billing", "Bill Book (Consolidated)", "/bill-book", "fileText", "billing.view", "Freight Bill", 0, null),
            new("billing.invoices",  "billing", "Freight Invoices",         "/billing",   "fileText", "billing.view", null,           1, null),
            new("billing.receipts",  "billing", "Money Receipts (MR)",      "/receipts",  "fileText", "billing.view", "Paid MR",      2, null),

            // ---- master_data children ----
            new("master_data.parties",        "master_data", "Party Directory",          "/customers",        "fileText", "parties.view",                     null, 0, null),
            new("master_data.fleet",          "master_data", "Fleet & Stations",         "/fleet",            "truck",    "fleet.view",                       null, 1, null),
            new("master_data.compliance",     "master_data", "Fleet Compliance",         "/fleet/compliance", "info",     "fleet.view",                       null, 2, null),
            new("master_data.tyres",          "master_data", "Tyre Management",          "/tyres",            "truck",    "fleet.view",                       null, 3, null),
            new("master_data.spares",         "master_data", "Spare Parts Stock",        "/spare-parts",      "cog",      "fleet.view",                       null, 4, null),
            new("master_data.loans",          "master_data", "Vehicle EMI / Loans",      "/vehicle-loans",    "barChart", "fleet.view",                       null, 5, null),
            new("master_data.driver_ledger",  "master_data", "Driver Ledger",            "/driver-ledger",    "users",    "master_data.driver_ledger.view",   null, 6, null),
            new("master_data.vehicle_claims", "master_data", "Vehicle Insurance Claims", "/vehicle-claims",   "info",     "master_data.vehicle_claims.view",  null, 7, null),
            new("master_data.rates",          "master_data", "Rate Contracts",           "/rates",            "fileText", "rates.view",                       null, 8, null),
            new("master_data.vendor_rates",   "master_data", "Vendor Hire Rates",        "/vendor-rates",     "truck",    "master_data.vendor_rates.view",    null, 9, null),

            // ---- system children ----
            new("system.users",           "system", "Manage Users & Access", "/users",           "users",    "users.manage",        null, 0, null),
            new("system.settings",        "system", "SaaS Configuration",    "/settings",        "settings", "settings.manage",     null, 1, null),
            new("system.onboard",         "system", "Tenant Onboarding",     "/onboard",         "info",     "tenant.onboard",      null, 2, null),
            new("system.forgot_password", "system", "Forgot Password",       "/forgot-password", "lock",     "auth.password_reset", null, 3, null),
        };
    }
}
