using System;
using System.Collections.Generic;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class InvoiceDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string InvoiceNo { get; set; } = null!;
        public DateOnly InvoiceDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public string? PartyGstNo { get; set; }
        public string? PartyAddress { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Discount { get; set; }
        public decimal OtherCharges { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal DueAmount { get; set; }
        public InvoicePaymentStatus PaymentStatus { get; set; }
        public string PaymentStatusName => PaymentStatus.ToString();
        public string? PaymentMode { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<InvoiceItemDto> Items { get; set; } = new();
        public List<string> LinkedShipmentNos { get; set; } = new();
    }

    public class InvoiceItemDto
    {
        public long Id { get; set; }
        public long InvoiceId { get; set; }
        public long? ShipmentId { get; set; }
        public string? ShipmentNo { get; set; }
        public string Description { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class CreateInvoiceRequest
    {
        public string? InvoiceNo { get; set; } // If blank, auto-generated via NumberingSequence
        public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public DateOnly? DueDate { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public string? PartyGstNo { get; set; }
        public string? PartyAddress { get; set; }
        public decimal TaxRate { get; set; } = 0;
        public decimal Discount { get; set; } = 0;
        public decimal OtherCharges { get; set; } = 0;
        public decimal PaidAmount { get; set; } = 0;
        public string? PaymentMode { get; set; }
        public string? Remarks { get; set; }
        public List<CreateInvoiceItemRequest> Items { get; set; } = new();
        public List<long>? ShipmentIdsToLink { get; set; }
    }

    public class CreateInvoiceItemRequest
    {
        public long? ShipmentId { get; set; }
        public string? ShipmentNo { get; set; }
        public string Description { get; set; } = null!;
        public decimal Quantity { get; set; } = 1;
        public decimal Rate { get; set; } = 0;
        public decimal Amount { get; set; } = 0;
        public decimal TaxRate { get; set; } = 0;
        public decimal TaxAmount { get; set; } = 0;
    }

    public class UpdateInvoiceRequest
    {
        public DateOnly InvoiceDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public string? PartyGstNo { get; set; }
        public string? PartyAddress { get; set; }
        public decimal TaxRate { get; set; }
        public decimal Discount { get; set; }
        public decimal OtherCharges { get; set; }
        public string? Remarks { get; set; }
        public List<CreateInvoiceItemRequest> Items { get; set; } = new();
    }

    public class InvoiceLookupDto
    {
        public long Id { get; set; }
        public string InvoiceNo { get; set; } = null!;
        public DateOnly InvoiceDate { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal DueAmount { get; set; }
        public InvoicePaymentStatus PaymentStatus { get; set; }
    }

    public class RecordPaymentRequest
    {
        public decimal PaymentAmount { get; set; }
        public string? PaymentMode { get; set; } // Cash, UPI, NEFT, Cheque
        public string? ReferenceNo { get; set; }
        public string? Remarks { get; set; }
    }
}
