using System;
using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class ManifestItemDto
    {
        public long Id { get; set; }
        public long ManifestId { get; set; }
        public long ShipmentId { get; set; }
        public string ShipmentNo { get; set; } = null!;
        public string? ConsignorName { get; set; }
        public string? ConsigneeName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public long? TargetDestinationHubId { get; set; }
        public string? TargetDestinationHubName { get; set; }
        public int LoadedPackages { get; set; }
        public decimal LoadedWeightKg { get; set; }
        public ManifestItemStatus UnloadingStatus { get; set; }
        public int? ReceivedPackages { get; set; }
        public int? ShortagePackages { get; set; }
        public int? DamagedPackages { get; set; }
        public long? UnloadedAtHubId { get; set; }
        public string? UnloadedAtHubName { get; set; }
        public DateTime? UnloadedDate { get; set; }
        public string? DiscrepancyRemarks { get; set; }
        public decimal TotalFreight { get; set; }
        public PaymentTerm PaymentTerm { get; set; }
        public string? EwayBillNo { get; set; }
    }

    public class ManifestDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string ManifestNo { get; set; } = null!;
        public DateOnly ManifestDate { get; set; }
        public long? OriginHubId { get; set; }
        public string? OriginHubName { get; set; }
        public long? DestinationHubId { get; set; }
        public string? DestinationHubName { get; set; }
        public long? TripId { get; set; }
        public string? TripNo { get; set; }
        public string? VehicleNo { get; set; }
        public string? DriverName { get; set; }
        public string? ConsolidatedEwayBillNo { get; set; }
        public DateTime? ConsolidatedEwayBillDate { get; set; }
        public string? SealNo { get; set; }
        public string? LoadingSupervisorName { get; set; }
        public string? Remarks { get; set; }
        public ManifestStatus Status { get; set; }
        public int TotalConsignments { get; set; }
        public int TotalPackages { get; set; }
        public decimal TotalWeightKg { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByName { get; set; }

        public List<ManifestItemDto> Items { get; set; } = new List<ManifestItemDto>();
    }

    public class CreateManifestItemRequest
    {
        public long ShipmentId { get; set; }
        public long? TargetDestinationHubId { get; set; }
        public int? LoadedPackages { get; set; }
        public decimal? LoadedWeightKg { get; set; }
    }

    public class CreateManifestRequest
    {
        public string? ManifestNo { get; set; } // Auto-generated if omitted
        public DateOnly? ManifestDate { get; set; }
        public long? OriginHubId { get; set; }
        public long? DestinationHubId { get; set; }
        public long? TripId { get; set; }
        public string? ConsolidatedEwayBillNo { get; set; }
        public string? SealNo { get; set; }
        public string? LoadingSupervisorName { get; set; }
        public string? Remarks { get; set; }
        public List<CreateManifestItemRequest> Items { get; set; } = new List<CreateManifestItemRequest>();
    }

    public class UpdateManifestRequest
    {
        public DateOnly? ManifestDate { get; set; }
        public long? OriginHubId { get; set; }
        public long? DestinationHubId { get; set; }
        public long? TripId { get; set; }
        public string? ConsolidatedEwayBillNo { get; set; }
        public string? SealNo { get; set; }
        public string? LoadingSupervisorName { get; set; }
        public string? Remarks { get; set; }
        public ManifestStatus? Status { get; set; }
        public List<CreateManifestItemRequest>? Items { get; set; }
    }

    public class UnloadManifestItemDiscrepancyRequest
    {
        public long ManifestItemId { get; set; }
        public ManifestItemStatus UnloadingStatus { get; set; } = ManifestItemStatus.ReceivedIntact;
        public int ReceivedPackages { get; set; }
        public int ShortagePackages { get; set; } = 0;
        public int DamagedPackages { get; set; } = 0;
        public string? DiscrepancyRemarks { get; set; }
    }

    public class UnloadManifestRequest
    {
        public long UnloadedAtHubId { get; set; }
        public DateTime? UnloadedDate { get; set; }
        public string? Remarks { get; set; }
        public List<UnloadManifestItemDiscrepancyRequest>? Items { get; set; }
    }

    public class AssignTripToManifestRequest
    {
        public long TripId { get; set; }
    }

    public class ManifestFilterRequest
    {
        public string? Search { get; set; }
        public ManifestStatus? Status { get; set; }
        public long? OriginHubId { get; set; }
        public long? DestinationHubId { get; set; }
        public long? TripId { get; set; }
        public DateOnly? FromDate { get; set; }
        public DateOnly? ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public class ManifestResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ManifestDto? Data { get; set; }
    }

    public class ManifestListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public List<ManifestDto> Data { get; set; } = new List<ManifestDto>();
    }
}
