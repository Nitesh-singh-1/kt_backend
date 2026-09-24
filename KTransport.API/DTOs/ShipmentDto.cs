using System;
using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class ConsignmentInvoiceReferenceDto
    {
        public long Id { get; set; }
        public string CustomerInvoiceNo { get; set; } = null!;
        public DateOnly CustomerInvoiceDate { get; set; }
        public decimal DeclaredGoodsValue { get; set; }
        public string? EwayBillNo { get; set; }
        public DateOnly? EwayBillDate { get; set; }
        public DateTime? EwayBillValidUpto { get; set; }
        public string? DocumentType { get; set; }
        public int? PackageCount { get; set; }
        public decimal? WeightKg { get; set; }
        public string? CommodityDescription { get; set; }
        public string? DocumentUrl { get; set; }
    }

    public class CreateConsignmentInvoiceReferenceRequest
    {
        public string CustomerInvoiceNo { get; set; } = null!;
        public DateOnly CustomerInvoiceDate { get; set; }
        public decimal DeclaredGoodsValue { get; set; }
        public string? EwayBillNo { get; set; }
        public DateOnly? EwayBillDate { get; set; }
        public DateTime? EwayBillValidUpto { get; set; }
        public string? DocumentType { get; set; } = "TaxInvoice";
        public int? PackageCount { get; set; }
        public decimal? WeightKg { get; set; }
        public string? CommodityDescription { get; set; }
        public string? DocumentUrl { get; set; }
    }

    public class ShipmentItemDto
    {
        public long Id { get; set; }
        public string? Article { get; set; }
        public string? Description { get; set; }
        public decimal Weight { get; set; }
        public decimal Rate { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal TotalAmount { get; set; }
    }

    public class ShipmentChargeItemDto
    {
        public long Id { get; set; }
        public int? ChargeTypeId { get; set; }
        public string ChargeName { get; set; } = null!;
        public decimal Amount { get; set; }
        public bool IsTaxable { get; set; }
    }

    public class ShipmentStatusHistoryDto
    {
        public long Id { get; set; }
        public ShipmentStatus FromStatus { get; set; }
        public ShipmentStatus ToStatus { get; set; }
        public string? Location { get; set; }
        public string? Remarks { get; set; }
        public string? ChangedByUserName { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class ShipmentDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string ShipmentNo { get; set; } = null!;
        public string? InvoiceNo { get; set; }
        public long? InvoiceId { get; set; }
        public DateOnly ShipmentDate { get; set; }
        public DateOnly? InvoiceDate { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? TruckNo { get; set; }
        public TaxTreatment TaxTreatment { get; set; }
        public string? GstPaidBy { get; set; }

        public long? ConsignorPartyId { get; set; }
        public string? ConsignorName { get; set; }
        public string? ConsignorGstNo { get; set; }
        public string? ConsignorMobile { get; set; }
        public string? ConsignorAddress { get; set; }

        public long? ConsigneePartyId { get; set; }
        public string? ConsigneeName { get; set; }
        public string? ConsigneeGstNo { get; set; }
        public string? ConsigneeMobile { get; set; }
        public string? ConsigneeAddress { get; set; }

        public decimal GoodsValue { get; set; }
        public PaymentTerm PaymentTerm { get; set; }
        public decimal TotalFreight { get; set; }
        public decimal TotalOtherCharges { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DueAmount { get; set; }

        // Hubs & Routing
        public long? OriginHubId { get; set; }
        public string? OriginHubName { get; set; }
        public long? DestinationHubId { get; set; }
        public string? DestinationHubName { get; set; }
        public long? CurrentHubId { get; set; }
        public string? CurrentHubName { get; set; }
        public string? DeliveryType { get; set; }
        public string? EwayBillNo { get; set; }
        public DateTime? EwayBillValidUpto { get; set; }

        public ShipmentStatus Status { get; set; }
        public string? Remarks { get; set; }
        public string? BookingClerk { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByName { get; set; }

        public List<ConsignmentInvoiceReferenceDto> InvoiceReferences { get; set; } = new List<ConsignmentInvoiceReferenceDto>();
        public List<ShipmentItemDto> Items { get; set; } = new List<ShipmentItemDto>();
        public List<ShipmentChargeItemDto> ChargeItems { get; set; } = new List<ShipmentChargeItemDto>();
        public List<ShipmentStatusHistoryDto> StatusHistory { get; set; } = new List<ShipmentStatusHistoryDto>();
    }

    public class CreateShipmentRequest
    {
        public string? ShipmentNo { get; set; } // Auto-generated if omitted
        public string? InvoiceNo { get; set; }
        public long? InvoiceId { get; set; }
        public DateOnly? ShipmentDate { get; set; }
        public DateOnly? InvoiceDate { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? TruckNo { get; set; }
        public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.NonTaxable;
        public string? GstPaidBy { get; set; }

        public long? ConsignorPartyId { get; set; }
        public string? ConsignorName { get; set; }
        public string? ConsignorGstNo { get; set; }
        public string? ConsignorMobile { get; set; }
        public string? ConsignorAddress { get; set; }
        public bool SaveConsignorAsParty { get; set; } = false;

        public long? ConsigneePartyId { get; set; }
        public string? ConsigneeName { get; set; }
        public string? ConsigneeGstNo { get; set; }
        public string? ConsigneeMobile { get; set; }
        public string? ConsigneeAddress { get; set; }
        public bool SaveConsigneeAsParty { get; set; } = false;

        public decimal GoodsValue { get; set; }
        public PaymentTerm PaymentTerm { get; set; } = PaymentTerm.ToPay;
        public decimal TotalFreight { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string? Remarks { get; set; }
        public string? BookingClerk { get; set; }

        public long? OriginHubId { get; set; }
        public long? DestinationHubId { get; set; }
        public string? DeliveryType { get; set; }
        public string? EwayBillNo { get; set; }
        public DateTime? EwayBillValidUpto { get; set; }

        public List<CreateConsignmentInvoiceReferenceRequest>? CustomerInvoices { get; set; }
        public List<ShipmentItemDto>? Items { get; set; }
        public List<ShipmentChargeItemDto>? ChargeItems { get; set; }
    }

    public class UpdateShipmentRequest
    {
        public string? InvoiceNo { get; set; }
        public long? InvoiceId { get; set; }
        public DateOnly? ShipmentDate { get; set; }
        public DateOnly? InvoiceDate { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? TruckNo { get; set; }
        public TaxTreatment TaxTreatment { get; set; }
        public string? GstPaidBy { get; set; }

        public long? ConsignorPartyId { get; set; }
        public string? ConsignorName { get; set; }
        public string? ConsignorGstNo { get; set; }
        public string? ConsignorMobile { get; set; }
        public string? ConsignorAddress { get; set; }

        public long? ConsigneePartyId { get; set; }
        public string? ConsigneeName { get; set; }
        public string? ConsigneeGstNo { get; set; }
        public string? ConsigneeMobile { get; set; }
        public string? ConsigneeAddress { get; set; }

        public decimal GoodsValue { get; set; }
        public PaymentTerm PaymentTerm { get; set; }
        public decimal TotalFreight { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public string? Remarks { get; set; }
        public string? BookingClerk { get; set; }

        public long? OriginHubId { get; set; }
        public long? DestinationHubId { get; set; }
        public string? DeliveryType { get; set; }
        public string? EwayBillNo { get; set; }
        public DateTime? EwayBillValidUpto { get; set; }

        public List<CreateConsignmentInvoiceReferenceRequest>? CustomerInvoices { get; set; }
        public List<ShipmentItemDto>? Items { get; set; }
        public List<ShipmentChargeItemDto>? ChargeItems { get; set; }
    }

    public class UpdateShipmentStatusRequest
    {
        public ShipmentStatus NewStatus { get; set; }
        public string? Location { get; set; }
        public string? Remarks { get; set; }
    }

    public class ShipmentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ShipmentDto? Data { get; set; }
    }

    public class ShipmentListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public List<ShipmentDto> Data { get; set; } = new List<ShipmentDto>();
    }
}
