using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    public class BookingRegisterReportDto
    {
        public int TotalBookings { get; set; }
        public decimal TotalFreightAmount { get; set; }
        public decimal TotalOtherCharges { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal TotalGrandTotal { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalDueAmount { get; set; }
        public List<ShipmentDto> Records { get; set; } = new();
    }

    public class TripProfitabilityReportDto
    {
        public int TotalTrips { get; set; }
        public decimal TotalFreightRevenue { get; set; }
        public decimal TotalDriverCashAdvance { get; set; }
        public decimal TotalDieselAdvance { get; set; }
        public decimal TotalOnRoadExpenses { get; set; }
        public decimal TotalLorryHireVendorCost { get; set; }
        public decimal NetTripProfit { get; set; }
        public decimal ProfitMarginPercentage { get; set; }
        public List<TripProfitabilityItemDto> TripDetails { get; set; } = new();
    }

    public class TripProfitabilityItemDto
    {
        public long TripId { get; set; }
        public string TripNo { get; set; } = null!;
        public DateOnly TripDate { get; set; }
        public string? VehicleNo { get; set; }
        public string? DriverName { get; set; }
        public string? OriginLocation { get; set; }
        public string? DestinationLocation { get; set; }
        public decimal Revenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitMarginPct { get; set; }
    }

    public class TaxSummaryReportDto
    {
        public decimal TotalTaxableFreight { get; set; }
        public decimal TotalNonTaxableFreight { get; set; }
        public decimal TotalGstRcmFreight { get; set; }
        public decimal TotalTaxCollected { get; set; }
        public int TotalShipmentsCount { get; set; }
    }

    public class PartyOutstandingReportDto
    {
        public long PartyId { get; set; }
        public string PartyName { get; set; } = null!;
        public string? Mobile { get; set; }
        public string? GstNo { get; set; }
        public decimal TotalBilledAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalOutstandingDue { get; set; }
        public int PendingShipmentsCount { get; set; }
    }

    public class VendorPayableReportDto
    {
        public long VendorId { get; set; }
        public string VendorName { get; set; } = null!;
        public string? Mobile { get; set; }
        public string? PanNo { get; set; }
        public decimal TotalHireAmount { get; set; }
        public decimal TotalAdvancePaid { get; set; }
        public decimal TotalTdsDeducted { get; set; }
        public decimal TotalBalancePayable { get; set; }
        public int ActiveContractsCount { get; set; }
    }
}
