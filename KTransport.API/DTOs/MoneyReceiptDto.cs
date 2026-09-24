using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    public class MoneyReceiptDto
    {
        public long Id { get; set; }
        public string ReceiptNo { get; set; } = null!;
        public DateOnly ReceiptDate { get; set; }
        public long ShipmentId { get; set; }
        public string ShipmentNo { get; set; } = null!;
        public string PayerName { get; set; } = null!;
        public string? PayerGstNo { get; set; }
        public string? PayerMobile { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public int TotalPackages { get; set; }
        public decimal TotalWeightKg { get; set; }

        public decimal BaseFreight { get; set; }
        public decimal HamaliCharges { get; set; }
        public decimal DoorDeliveryCharges { get; set; }
        public decimal StationeryCharges { get; set; }
        public decimal Surcharges { get; set; }
        public decimal OtherCharges { get; set; }
        public decimal GstAmount { get; set; }
        public decimal TotalAmount { get; set; }

        public string PaymentMode { get; set; } = "Cash";
        public string? TransactionRef { get; set; }
        public string? CollectedBy { get; set; }
        public string? Remarks { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class MoneyReceiptFilterRequest
    {
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? Search { get; set; }
        public string? PaymentMode { get; set; }
    }
}
