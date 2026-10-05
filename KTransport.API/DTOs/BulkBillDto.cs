using System;
using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    /// <summary>An unbilled consignment (no invoice linked yet) eligible for billing / Bill Book.</summary>
    public class UnbilledShipmentDto
    {
        public long Id { get; set; }
        public string ShipmentNo { get; set; } = null!;
        public DateOnly ShipmentDate { get; set; }
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
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public int TotalPackages { get; set; }
        public decimal TotalWeightKg { get; set; }
        public decimal Rate { get; set; }
        public decimal GoodsValue { get; set; }
        public decimal TotalFreight { get; set; }
        public decimal TotalOtherCharges { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DueAmount { get; set; }
        public PaymentTerm PaymentTerm { get; set; }
        public string PaymentTermName => PaymentTerm.ToString();
        public ShipmentStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public string? CustomerInvoiceNo { get; set; }
        public string? EwayBillNo { get; set; }
        public string? Remarks { get; set; }
        public DateOnly? DeliveryDate { get; set; }
        public string? DeliveryDateFormatted => DeliveryDate?.ToString("dd/MM/yyyy") ?? "-";
    }

    public class BulkBillRequest
    {
        public List<long> ShipmentIds { get; set; } = new();
        public DateOnly? InvoiceDate { get; set; }
        /// <summary>GST rate to apply to every generated invoice (0 for RCM / exempt freight).</summary>
        public decimal TaxRate { get; set; } = 0;
    }

    public class BulkBillResultDto
    {
        public int InvoicesCreated { get; set; }
        public int ShipmentsBilled { get; set; }
        public decimal TotalAmount { get; set; }
        public List<string> InvoiceNos { get; set; } = new();
        public string? Message { get; set; }
    }

    public class PartyUnbilledSummaryDto
    {
        public long? PartyId { get; set; }
        public string PartyName { get; set; } = null!;
        public string? PartyGstNo { get; set; }
        public string? PartyAddress { get; set; }
        public int UnbilledCount { get; set; }
        public decimal TotalUnbilledAmount { get; set; }
    }

    public class CreateBillBookRequestDto
    {
        public string? InvoiceNo { get; set; } // Auto-generated if null
        public long? PartyId { get; set; }
        public string PartyName { get; set; } = null!;
        public string? PartyGstNo { get; set; }
        public string? PartyAddress { get; set; }
        public string? BillingMonth { get; set; } // e.g. "October 2026"
        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly? DueDate { get; set; }
        public List<long> ShipmentIds { get; set; } = new();
        public decimal TaxRate { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public decimal OtherCharges { get; set; } = 0;
        public decimal PaidAmount { get; set; } = 0;
        public string? PaymentMode { get; set; }
        public string? Remarks { get; set; }
        public string? PreparedBy { get; set; }
        public string? CheckedBy { get; set; }
    }

    public class BillBookInvoiceResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public InvoiceDto? Data { get; set; }
    }
}
