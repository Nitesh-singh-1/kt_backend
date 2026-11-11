using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs.Gst;

/// <summary>
/// Generate-EWB request. Field names mirror the NIC EWB API payload
/// (camelCase on the wire) so the adapter can forward it as-is after
/// local validation. Any field our Bilty UI already captures should be
/// mapped by the caller; the controller does not synthesize values from
/// the Shipment.
/// </summary>
public class GenerateEwbRequestDto
{
    public string SupplyType { get; set; } = "O";          // Outward by default
    public string SubSupplyType { get; set; } = "1";       // Supply
    public string SubSupplyDesc { get; set; } = string.Empty;
    public string DocType { get; set; } = "INV";
    public string DocNo { get; set; } = string.Empty;
    public DateOnly DocDate { get; set; }

    public string FromGstin { get; set; } = string.Empty;
    public string FromTrdName { get; set; } = string.Empty;
    public string FromAddr1 { get; set; } = string.Empty;
    public string? FromAddr2 { get; set; }
    public string FromPlace { get; set; } = string.Empty;
    public int FromPincode { get; set; }
    public int FromStateCode { get; set; }
    public int ActFromStateCode { get; set; }              // actual despatch state

    public string ToGstin { get; set; } = string.Empty;
    public string ToTrdName { get; set; } = string.Empty;
    public string ToAddr1 { get; set; } = string.Empty;
    public string? ToAddr2 { get; set; }
    public string ToPlace { get; set; } = string.Empty;
    public int ToPincode { get; set; }
    public int ToStateCode { get; set; }
    public int ActToStateCode { get; set; }                // actual delivery state

    public int TransactionType { get; set; } = 1;          // 1=Regular, 2=BillTo-ShipTo
    public string? ShipToGstin { get; set; }               // MANDATORY when TransactionType=2 (NIC 2026-06-15)

    public decimal TotalValue { get; set; }
    public decimal CgstValue { get; set; }
    public decimal SgstValue { get; set; }
    public decimal IgstValue { get; set; }
    public decimal CessValue { get; set; }
    public decimal CessNonAdvolValue { get; set; }
    public decimal OtherValue { get; set; }
    public decimal TotInvValue { get; set; }

    public decimal TransDistance { get; set; }
    public string? TransporterId { get; set; }
    public string? TransporterName { get; set; }
    public string? TransDocNo { get; set; }
    public DateOnly? TransDocDate { get; set; }
    public string? VehicleNo { get; set; }
    public string VehicleType { get; set; } = "R";
    public string TransMode { get; set; } = "1";            // 1=Road

    public List<GenerateEwbItemDto> ItemList { get; set; } = new();

    /// <summary>Optional link back to the Bilty/Shipment this EWB was generated for.</summary>
    public long? ShipmentId { get; set; }
}

public class GenerateEwbItemDto
{
    public string ProductName { get; set; } = string.Empty;
    public string? ProductDesc { get; set; }
    public string HsnCode { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string QtyUnit { get; set; } = "NOS";
    public decimal CgstRate { get; set; }
    public decimal SgstRate { get; set; }
    public decimal IgstRate { get; set; }
    public decimal CessRate { get; set; }
    public decimal CessNonAdvol { get; set; }
    public decimal TaxableAmount { get; set; }
}

public class EwayBillDto
{
    public Guid Id { get; set; }
    public string EwbNumber { get; set; } = string.Empty;
    public DateTime EwbDate { get; set; }
    public DateTime? ValidUpto { get; set; }
    public string? Status { get; set; }
    public string SyncStatus { get; set; } = string.Empty;

    public string? SupplyType { get; set; }
    public string? SubSupplyType { get; set; }
    public string? DocType { get; set; }
    public string? DocNo { get; set; }
    public DateOnly? DocDate { get; set; }

    public string? FromGstin { get; set; }
    public string? ToGstin { get; set; }
    public string? ShipToGstin { get; set; }

    public int? TransactionType { get; set; }
    public decimal? TotalInvoiceValue { get; set; }
    public decimal? TransDistance { get; set; }
    public string? VehicleNo { get; set; }
    public string? TransporterId { get; set; }
    public string? TransporterName { get; set; }

    public long? ShipmentId { get; set; }

    public string PayloadJson { get; set; } = "{}";
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateVehicleRequestDto
{
    public string EwbNumber { get; set; } = string.Empty;
    public string VehicleNo { get; set; } = string.Empty;
    public string FromPlace { get; set; } = string.Empty;
    public int FromState { get; set; }
    public string ReasonCode { get; set; } = "1";          // 1=Due to break down (etc.)
    public string? ReasonRemark { get; set; }
    public string TransMode { get; set; } = "1";
    public string VehicleType { get; set; } = "R";
    public string? TransDocNo { get; set; }
    public DateOnly? TransDocDate { get; set; }
}

public class CancelEwbRequestDto
{
    public string EwbNumber { get; set; } = string.Empty;
    public string CancelReasonCode { get; set; } = "1";    // 1=Duplicate, 2=Order Cancelled, 3=Data Entry mistake, 4=Others
    public string? CancelRemarks { get; set; }
}

public class ExtendValidityRequestDto
{
    public string EwbNumber { get; set; } = string.Empty;
    public string VehicleNo { get; set; } = string.Empty;
    public string FromPlace { get; set; } = string.Empty;
    public int FromState { get; set; }
    public int RemainingDistance { get; set; }
    public string ExtnRsnCode { get; set; } = "1";
    public string? ExtnRemarks { get; set; }
    public string TransMode { get; set; } = "1";
    public string VehicleType { get; set; } = "R";
    public string? ConsignmentStatus { get; set; } = "M";  // M=In Movement, T=In Transit
    public string? TransitType { get; set; }               // R=Road, W=Warehouse, O=Others
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? AddressLine3 { get; set; }
}
