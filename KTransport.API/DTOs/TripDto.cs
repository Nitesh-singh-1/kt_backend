using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class TripDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string TripNo { get; set; } = null!;
        public DateOnly TripDate { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public long? DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? DriverMobile { get; set; }
        public long? OriginLocationId { get; set; }
        public string? OriginLocationName { get; set; }
        public long? DestinationLocationId { get; set; }
        public string? DestinationLocationName { get; set; }
        public TripStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public DateTime? DepartureTime { get; set; }
        public DateTime? ArrivalTime { get; set; }
        public decimal StartOdometer { get; set; }
        public decimal EndOdometer { get; set; }
        public string? SealNo { get; set; }
        public string? Remarks { get; set; }
        public decimal TotalWeightTons { get; set; }
        public int TotalPackages { get; set; }
        public decimal TotalFreightRevenue { get; set; }
        public decimal DriverAdvanceCash { get; set; }
        public decimal DriverAdvanceFuel { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfitMargin => TotalFreightRevenue - (DriverAdvanceCash + DriverAdvanceFuel + TotalExpenses);
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<TripShipmentDto> Shipments { get; set; } = new();
        public List<TripExpenseDto> Expenses { get; set; } = new();
    }

    public class TripShipmentDto
    {
        public long Id { get; set; }
        public long TripId { get; set; }
        public long ShipmentId { get; set; }
        public string? ShipmentNo { get; set; }
        public string? ConsignorName { get; set; }
        public string? ConsigneeName { get; set; }
        public PaymentTerm? PaymentTerm { get; set; }
        public string? PaymentTermName => PaymentTerm?.ToString();
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public decimal LoadedWeight { get; set; }
        public int LoadedPackages { get; set; }
        public decimal FreightAmount { get; set; }
        public DateTime LoadedAt { get; set; }
        public DateTime? UnloadedAt { get; set; }
        public string? Remarks { get; set; }
    }

    public class TripExpenseDto
    {
        public long Id { get; set; }
        public long TripId { get; set; }
        public TripExpenseType ExpenseType { get; set; }
        public string ExpenseTypeName => ExpenseType.ToString();
        public decimal Amount { get; set; }
        public string? ReceiptNo { get; set; }
        public string? PaymentMode { get; set; }
        public string? PaidTo { get; set; }
        public string? Remarks { get; set; }
        public DateOnly ExpenseDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateTripRequest
    {
        public string? TripNo { get; set; } // Auto-generated if omitted
        public DateOnly? TripDate { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public long? DriverId { get; set; }
        public string? DriverName { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Driver mobile must be a valid 10-digit Indian mobile number.")]
        public string? DriverMobile { get; set; }
        public long? OriginLocationId { get; set; }
        public string? OriginLocationName { get; set; }
        public long? DestinationLocationId { get; set; }
        public string? DestinationLocationName { get; set; }
        public decimal StartOdometer { get; set; } = 0;
        public string? SealNo { get; set; }
        public string? Remarks { get; set; }
        public decimal DriverAdvanceCash { get; set; } = 0;
        public decimal DriverAdvanceFuel { get; set; } = 0;
        public List<long>? ShipmentIdsToLoad { get; set; }
    }

    public class UpdateTripRequest
    {
        public DateOnly? TripDate { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public long? DriverId { get; set; }
        public string? DriverName { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Driver mobile must be a valid 10-digit Indian mobile number.")]
        public string? DriverMobile { get; set; }
        public long? OriginLocationId { get; set; }
        public string? OriginLocationName { get; set; }
        public long? DestinationLocationId { get; set; }
        public string? DestinationLocationName { get; set; }
        public decimal StartOdometer { get; set; }
        public decimal EndOdometer { get; set; }
        public string? SealNo { get; set; }
        public string? Remarks { get; set; }
        public decimal DriverAdvanceCash { get; set; }
        public decimal DriverAdvanceFuel { get; set; }
    }

    public class LoadShipmentsRequest
    {
        public List<long> ShipmentIds { get; set; } = new();
    }

    public class AddTripExpenseRequest
    {
        public TripExpenseType ExpenseType { get; set; } = TripExpenseType.Fuel;
        public decimal Amount { get; set; }
        public string? ReceiptNo { get; set; }
        public string? PaymentMode { get; set; }
        public string? PaidTo { get; set; }
        public string? Remarks { get; set; }
        public DateOnly? ExpenseDate { get; set; }
    }

    public class TripLookupDto
    {
        public long Id { get; set; }
        public string TripNo { get; set; } = null!;
        public DateOnly TripDate { get; set; }
        public string? VehicleNo { get; set; }
        public string? DriverName { get; set; }
        public string? OriginLocationName { get; set; }
        public string? DestinationLocationName { get; set; }
        public TripStatus Status { get; set; }
    }

    public class TripSettlementSummaryDto
    {
        public long TripId { get; set; }
        public string TripNo { get; set; } = null!;
        public DateOnly TripDate { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public long? DriverId { get; set; }
        public string? DriverName { get; set; }
        public string? DriverMobile { get; set; }
        public string? OriginLocationName { get; set; }
        public string? DestinationLocationName { get; set; }
        public TripStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public decimal StartOdometer { get; set; }
        public decimal EndOdometer { get; set; }
        public decimal TotalKilometers => EndOdometer > StartOdometer ? (EndOdometer - StartOdometer) : 0;
        
        // Consignments metrics
        public int TotalShipments { get; set; }
        public int TotalPackages { get; set; }
        public decimal TotalWeightTons { get; set; }
        public decimal TotalFreightRevenue { get; set; }
        public decimal TotalToPayFreight { get; set; }
        public decimal TotalPaidFreight { get; set; }
        public decimal TotalTbbFreight { get; set; }
        
        // Advances & Cash Handover
        public decimal DriverAdvanceCash { get; set; }
        public decimal DriverAdvanceFuel { get; set; }
        public decimal TotalDriverAdvance => DriverAdvanceCash + DriverAdvanceFuel;
        public decimal CollectedToPayFreight { get; set; } // Cash collected by driver
        public decimal TotalDriverAccountability => TotalDriverAdvance + CollectedToPayFreight;
        
        // Expenses breakdown
        public decimal TotalExpenses { get; set; }
        public decimal FuelExpenses { get; set; }
        public decimal TollExpenses { get; set; }
        public decimal DriverExpenses { get; set; }
        public decimal MaintenanceExpenses { get; set; }
        public decimal OtherExpenses { get; set; }
        
        // Net Driver Balance: positive means driver owes company / returns cash; negative means company owes driver reimbursement
        public decimal NetDriverBalance => TotalDriverAccountability - TotalExpenses;
        
        public bool IsSettled => Status == TripStatus.Completed;
        public DateTime? SettledAt { get; set; }
        public string? SettlementRemarks { get; set; }
        
        public List<TripShipmentDto> Shipments { get; set; } = new();
        public List<TripExpenseDto> Expenses { get; set; } = new();
    }

    public class SettleTripRequestDto
    {
        public long TripId { get; set; }
        public decimal? EndOdometer { get; set; }
        public decimal CollectedToPayFreight { get; set; } = 0;
        public decimal SettledAmount { get; set; } = 0; // Amount collected from / reimbursed to driver
        public string PaymentMode { get; set; } = "CASH"; // CASH, UPI, BANK_TRANSFER
        public string? SettlementRemarks { get; set; }
        public DateOnly SettlementDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public List<AddTripExpenseRequest>? AdditionalExpenses { get; set; }
    }

    public class TripSettlementResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TripSettlementSummaryDto? Data { get; set; }
    }
}
