using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    public class PublicTenantConfigDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string TenantCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? FaviconUrl { get; set; }
        public string ThemeColor { get; set; } = "#4f46e5";
        public string CurrencyCode { get; set; } = "INR";
        public string CurrencySymbol { get; set; } = "₹";
    }

    public class GeneralSettingsDto
    {
        public string CompanyName { get; set; } = "K-Transport Logistics";
        public string LegalName { get; set; } = "K-Transport Logistics Private Limited";
        public string SupportEmail { get; set; } = "support@ktransport.com";
        public string SupportPhone { get; set; } = "+91 98765 43210";
        public string? LogoUrl { get; set; }
        public string? FaviconUrl { get; set; }
        public string ThemeColor { get; set; } = "#4f46e5";
        public string CurrencyCode { get; set; } = "INR";
        public string CurrencySymbol { get; set; } = "₹";
        public string TimeZone { get; set; } = "Asia/Kolkata";
        public string DateFormat { get; set; } = "DD/MM/YYYY";
        public string TimeFormat { get; set; } = "12h";
        public string Address { get; set; } = "123 Logistics Park, Transport Nagar";
    }

    public class BillingAndTaxSettingsDto
    {
        public bool IsGstEnabled { get; set; } = true;
        public string Gstin { get; set; } = string.Empty;
        public string PanNumber { get; set; } = string.Empty;
        public decimal DefaultCgstRate { get; set; } = 2.5m;
        public decimal DefaultSgstRate { get; set; } = 2.5m;
        public decimal DefaultIgstRate { get; set; } = 5.0m;
        public bool EnableRcm { get; set; } = true;
        public decimal EWayBillThresholdAmount { get; set; } = 50000.0m;
        public bool IsTdsEnabled { get; set; } = false;
        public decimal TdsPercentage { get; set; } = 2.0m;
        public bool IsTcsEnabled { get; set; } = false;
        public decimal TcsPercentage { get; set; } = 0.1m;
    }

    public class DocumentSequenceDto
    {
        public string DocType { get; set; } = string.Empty; // Invoice, GR, Challan, Trip, Claim
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public int PaddingDigits { get; set; } = 5;
        public long NextNumber { get; set; } = 1;
        public string ResetPeriod { get; set; } = "Never"; // Never, Yearly, Monthly
    }

    public class OperationalWorkflowSettingsDto
    {
        public bool MandatoryDriverPhone { get; set; } = true;
        public bool MandatoryPodBeforeSettlement { get; set; } = true;
        public bool MandatoryEWayBillForDispatch { get; set; } = true;
        public decimal AllowOverweightTolerancePercentage { get; set; } = 5.0m;
        public int MaxDetentionFreeHours { get; set; } = 24;
        public bool AutoCloseCompletedTrips { get; set; } = true;
    }

    public class TenantFeatureFlagsDto
    {
        public bool GstBilling { get; set; } = true;
        public bool WithoutGstBilling { get; set; } = true;
        public bool ChallanManagement { get; set; } = true;
        public bool TripManagement { get; set; } = true;
        public bool FleetManagement { get; set; } = true;
        public bool VehicleMaintenance { get; set; } = true;
        public bool CargoClaims { get; set; } = true;
        public bool GpsTracking { get; set; } = true;
        public bool ReportsAndAnalytics { get; set; } = true;
        public bool FreightRateCards { get; set; } = true;
        public bool VendorManagement { get; set; } = true;
        public bool CustomerPortal { get; set; } = false;
    }

    public class IntegrationSettingsDto
    {
        public bool WhatsAppEnabled { get; set; } = false;
        public bool SmsEnabled { get; set; } = false;
        public string GpsProvider { get; set; } = "None"; // None, Custom, TrackSolid, WheelsEye
        public bool FastagEnabled { get; set; } = false;
        public string? WebhookUrl { get; set; }
    }

    public class TenantConfigurationDto
    {
        public Guid TenantId { get; set; }
        public GeneralSettingsDto General { get; set; } = new();
        public BillingAndTaxSettingsDto BillingAndTax { get; set; } = new();
        public List<DocumentSequenceDto> DocumentSequences { get; set; } = new();
        public OperationalWorkflowSettingsDto OperationalWorkflows { get; set; } = new();
        public TenantFeatureFlagsDto FeatureFlags { get; set; } = new();
        public IntegrationSettingsDto Integrations { get; set; } = new();
        public DateTime? UpdatedAt { get; set; }
    }

    public class UpdateTenantConfigurationDto
    {
        public GeneralSettingsDto? General { get; set; }
        public BillingAndTaxSettingsDto? BillingAndTax { get; set; }
        public List<DocumentSequenceDto>? DocumentSequences { get; set; }
        public OperationalWorkflowSettingsDto? OperationalWorkflows { get; set; }
        public TenantFeatureFlagsDto? FeatureFlags { get; set; }
        public IntegrationSettingsDto? Integrations { get; set; }
    }

    public class TenantSubscriptionDto
    {
        public string PlanName { get; set; } = "Enterprise Scale";
        public string PlanTier { get; set; } = "Enterprise";
        public string Status { get; set; } = "Active";
        public DateTime? ExpiresAt { get; set; }
        public int MaxVehicles { get; set; } = 500;
        public int CurrentVehicles { get; set; } = 0;
        public int MaxUsers { get; set; } = 100;
        public int CurrentUsers { get; set; } = 0;
        public int MaxMonthlyShipments { get; set; } = 10000;
        public int CurrentMonthlyShipments { get; set; } = 0;
    }
}
