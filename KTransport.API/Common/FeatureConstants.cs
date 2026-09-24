namespace KTransport.API.Common
{
    public static class FeatureConstants
    {
        // Core Transport & Logistics Modules
        public const string GOOD_RECEIPT = "GOOD_RECEIPT";
        public const string SHIPMENT = "SHIPMENT";
        public const string MANIFEST = "MANIFEST"; // Trip / Challan / Manifest
        public const string POD = "POD";
        public const string BILLING = "BILLING";
        public const string INVOICE = "INVOICE";
        public const string MONEY_RECEIPT = "MONEY_RECEIPT";
        public const string TRACKING = "TRACKING";
        public const string REPORTING = "REPORTING";
        
        // Master Data & Operations
        public const string VEHICLE = "VEHICLE"; // Fleet & Stations
        public const string DRIVER = "DRIVER";
        public const string PARTY = "PARTY"; // Customers / Parties
        public const string VENDOR = "VENDOR"; // Transporters / Market Hire
        public const string CLAIMS = "CLAIMS"; // Damage & Claims
        
        // Administration & SaaS Governance
        public const string USER_MANAGEMENT = "USER_MANAGEMENT";
        public const string SAAS_CONFIGURATION = "SAAS_CONFIGURATION";

        // Standard Default Subscribed Features for starter / pro plans
        public static readonly string[] AllFeatures = new[]
        {
            GOOD_RECEIPT,
            SHIPMENT,
            MANIFEST,
            POD,
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
            USER_MANAGEMENT,
            SAAS_CONFIGURATION
        };

        public static readonly string[] StarterFeatures = new[]
        {
            GOOD_RECEIPT,
            SHIPMENT,
            MANIFEST,
            POD,
            TRACKING,
            PARTY
        };

        public static readonly string[] ProfessionalFeatures = new[]
        {
            GOOD_RECEIPT,
            SHIPMENT,
            MANIFEST,
            POD,
            BILLING,
            INVOICE,
            MONEY_RECEIPT,
            TRACKING,
            REPORTING,
            PARTY,
            VEHICLE,
            DRIVER,
            VENDOR
        };

        public static readonly string[] EnterpriseFeatures = AllFeatures;

        /// <summary>
        /// Normalizes feature aliases (e.g. gr, consignments, challan) into canonical FeatureConstants.
        /// </summary>
        public static string Normalize(string featureOrMenuKey)
        {
            if (string.IsNullOrWhiteSpace(featureOrMenuKey)) return string.Empty;

            var key = featureOrMenuKey.Trim().ToUpperInvariant();

            return key switch
            {
                "GR" or "GR.LIST" or "GR.ENTRY" or "CONSIGNMENTS" or "CONSIGNMENTS.CREATE" or "CONSIGNMENTS.ALL" or "GOOD_RECEIPT" => GOOD_RECEIPT,
                "SHIPMENT" or "SHIPMENTS" => SHIPMENT,
                "CHALLAN" or "CHALLAN.LIST" or "CHALLAN.ENTRY" or "TRIPS" or "MANIFEST" or "MANIFESTS" => MANIFEST,
                "POD" or "DELIVERIES" => POD,
                "BILLING" or "BILLING.INVOICES" or "BILLING.RECEIPTS" or "INVOICE" or "INVOICES" => BILLING,
                "RECEIPTS" or "MONEY_RECEIPT" or "MONEY_RECEIPTS" => MONEY_RECEIPT,
                "TRACKING" or "GPS" => TRACKING,
                "REPORTS" or "REPORTING" or "ANALYTICS" => REPORTING,
                "FLEET" or "VEHICLES" or "VEHICLE" or "MASTER_DATA.FLEET" => VEHICLE,
                "DRIVER" or "DRIVERS" => DRIVER,
                "CUSTOMERS" or "PARTIES" or "PARTY" or "MASTER_DATA.PARTIES" or "MASTER_DATA" => PARTY,
                "VENDORS" or "VENDOR" => VENDOR,
                "CLAIMS" => CLAIMS,
                "USERS" or "USER_MANAGEMENT" or "SUB_USERS" => USER_MANAGEMENT,
                "SETTINGS" or "SAAS" or "SAAS_CONFIGURATION" or "SYSTEM.SETTINGS" or "CLIENTS" => SAAS_CONFIGURATION,
                _ => key
            };
        }
    }
}
