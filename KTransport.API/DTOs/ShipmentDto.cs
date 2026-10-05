using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        public string? PrivateMarka { get; set; }
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
        public string? PrivateMarka { get; set; }
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

        // Settlement & Delivery Reconciliation
        public bool IsSettled { get; set; }
        public bool IsPartialPayment { get; set; }
        public decimal? SettledReceivedAmount { get; set; }
        public decimal? SettledDiscountAmount { get; set; }
        public string? DiscountReason { get; set; }
        public string? DiscountRemarks { get; set; }
        public string? SettledPaymentMode { get; set; }
        public string? SettlementReferenceNo { get; set; }
        public string? DeliveredTo { get; set; }
        public DateOnly? DeliveryDate { get; set; }

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

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid Consignor GSTIN format.")]
        public string? ConsignorGstNo { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Consignor mobile must be a valid 10-digit Indian mobile number.")]
        public string? ConsignorMobile { get; set; }

        public string? ConsignorAddress { get; set; }
        public bool SaveConsignorAsParty { get; set; } = false;

        public long? ConsigneePartyId { get; set; }
        public string? ConsigneeName { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid Consignee GSTIN format.")]
        public string? ConsigneeGstNo { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Consignee mobile must be a valid 10-digit Indian mobile number.")]
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

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid Consignor GSTIN format.")]
        public string? ConsignorGstNo { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Consignor mobile must be a valid 10-digit Indian mobile number.")]
        public string? ConsignorMobile { get; set; }

        public string? ConsignorAddress { get; set; }

        public long? ConsigneePartyId { get; set; }
        public string? ConsigneeName { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid Consignee GSTIN format.")]
        public string? ConsigneeGstNo { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Consignee mobile must be a valid 10-digit Indian mobile number.")]
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
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public List<ShipmentDto> Data { get; set; } = new List<ShipmentDto>();
    }

    public class SettleDeliveryRequestDto
    {
        public List<long> ShipmentIds { get; set; } = new List<long>();
        public decimal? ReceivedAmount { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string? DiscountReason { get; set; } // RoundOff, ShortageClaim, DamageDeduction, RateDifference, DeliveryDeduction, Other
        public string? DiscountRemarks { get; set; }
        public string PaymentMode { get; set; } = "CASH";
        public bool IsPartialPayment { get; set; } = false;
        public string? PaymentReference { get; set; }
        public string? DeliveredTo { get; set; }
        public DateOnly DeliveryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public string? Remarks { get; set; }
    }

    public class SettleDeliveryResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SettledCount { get; set; }
        public decimal TotalAmountSettled { get; set; }
        public decimal TotalDiscountGiven { get; set; }
    }

    public class DeliverySettlementSummaryDto
    {
        public int TotalConsignments { get; set; }
        public decimal TotalConsignmentsAmount { get; set; }
        public int PendingDeliveriesCount { get; set; }
        public decimal PendingDeliveriesAmount { get; set; }
        public int ToPayCollectiblesCount { get; set; }
        public decimal ToPayCollectiblesAmount { get; set; }
        public int DeliveredAndSettledCount { get; set; }
        public decimal DeliveredAndSettledAmount { get; set; }
    }

    public class DeliverySettlementSummaryResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DeliverySettlementSummaryDto? Data { get; set; }
    }
}

