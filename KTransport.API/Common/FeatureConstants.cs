namespace KTransport.API.Common
{
    public static class FeatureConstants
    {
        // Core Transport & Logistics Modules
        public const string GOOD_RECEIPT = "GOOD_RECEIPT";
        public const string SHIPMENT = "SHIPMENT";
        public const string MANIFEST = "MANIFEST"; // Trip / Challan / Manifest
        public const string POD = "POD";
        public const string DELIVERY_SETTLEMENT = "DELIVERY_SETTLEMENT";
        public const string TRIP_SETTLEMENT = "TRIP_SETTLEMENT";
        public const string BILL_BOOK = "BILL_BOOK";
        public const string BILLING = "BILLING";
        public const string INVOICE = "INVOICE";
        public const string MONEY_RECEIPT = "MONEY_RECEIPT";
        public const string TRACKING = "TRACKING";
        public const string REPORTING = "REPORTING";
        public const string ACCOUNTING = "ACCOUNTING"; // paid add-on: double-entry ledger (TASK-016)

        // Master Data & Operations
        public const string VEHICLE = "VEHICLE"; // Fleet & Stations
        public const string DRIVER = "DRIVER";
        public const string PARTY = "PARTY"; // Customers / Parties
        public const string VENDOR = "VENDOR"; // Transporters / Market Hire
        public const string CLAIMS = "CLAIMS"; // Damage & Claims
        public const string QUOTATION = "QUOTATION"; // Quotations / Price Quotes
        
        // Administration & SaaS Governance
        public const string USER_MANAGEMENT = "USER_MANAGEMENT";
        public const string SAAS_CONFIGURATION = "SAAS_CONFIGURATION";

        // Standard Default Subscribed Features for starter / pro plans.
        // NOTE: ACCOUNTING is deliberately NOT here. It is a paid add-on gated solely through
        // tenant_modules (ADR accounting-ledger-architecture decision #4). AllFeatures doubles as
        // the no-subscription fallback in FeatureAuthorizationService, so listing it here would
        // hand the add-on to every unconfigured tenant for free.
        public static readonly string[] AllFeatures = new[]
        {
            GOOD_RECEIPT,
            SHIPMENT,
            MANIFEST,
            POD,
            DELIVERY_SETTLEMENT,
            TRIP_SETTLEMENT,
            BILL_BOOK,
            BILLING,
            INVOICE,
            MONEY_RECEIPT,
            TRACKING,
            REPORTING,
            VEHICLE,
            DRIVER,
            PARTY,
            VENDOR,
            CLAIMS,
            QUOTATION,
            USER_MANAGEMENT,
            SAAS_CONFIGURATION
        };

        public static readonly string[] StarterFeatures = new[]
        {
            GOOD_RECEIPT,
            SHIPMENT,
            MANIFEST,
            POD,
            DELIVERY_SETTLEMENT,
            TRACKING,
            PARTY
        };

        public static readonly string[] ProfessionalFeatures = new[]
        {
            GOOD_RECEIPT,
            SHIPMENT,
            MANIFEST,
            POD,
            DELIVERY_SETTLEMENT,
            TRIP_SETTLEMENT,
            BILL_BOOK,
            BILLING,
            INVOICE,
            MONEY_RECEIPT,
            TRACKING,
            REPORTING,
            PARTY,
            VEHICLE,
            DRIVER,
            VENDOR,
            QUOTATION
        };

        public static readonly string[] EnterpriseFeatures = AllFeatures;

        // RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate): the legacy
        // alias normalizer (gr/challan/consignments/bill_book/masterdata → canonical)
        // is DELETED. Module codes are canonical everywhere; no string normalization.
    }
}
