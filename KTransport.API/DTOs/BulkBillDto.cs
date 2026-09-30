using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    /// <summary>An unbilled consignment (no invoice linked yet) eligible for bulk billing.</summary>
    public class UnbilledShipmentDto
    {
        public long Id { get; set; }
        public string? ShipmentNo { get; set; }
        public DateOnly ShipmentDate { get; set; }
        public long? ConsignorPartyId { get; set; }
        public string? ConsignorName { get; set; }
        public string? ConsigneeName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public decimal TotalFreight { get; set; }
        public decimal GrandTotal { get; set; }
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
}
