using System;
using System.Collections.Generic;
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
}
