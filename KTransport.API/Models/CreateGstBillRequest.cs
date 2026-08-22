using System.ComponentModel.DataAnnotations;

namespace KTransport.API.Models
{
    public class CreateGstBillRequest
    {
        
        public string GrNo { get; set; } = string.Empty;

        public string? InvoiceNo { get; set; }

        [Required]
        public string FromLocation { get; set; } = string.Empty;

        [Required]
        public string ToLocation { get; set; } = string.Empty;

        public DateOnly? GrDate { get; set; }

        public DateOnly? InvoiceDate { get; set; }

        public decimal? GoodsValue { get; set; }

        public string? GstPaidBy { get; set; }

        public string? ConsignerName { get; set; }

        public string? ConsignerGstNo { get; set; }

        public string? ConsignerMobile { get; set; }

        public string? ConsigneeName { get; set; }

        public string? ConsigneeGstNo { get; set; }

        public string? ConsigneeMobile { get; set; }

        public string? ConsigneeAddress { get; set; }

        public string? TruckNo { get; set; }

        public string? DeliveryStatus { get; set; }

        public string? Remarks { get; set; }

        public decimal? Paid { get; set; }

        public decimal? Tbb { get; set; }

        public decimal? ToPay { get; set; }

        public decimal? TotalAmount { get; set; }

        public string? BookingClerk { get; set; }
        public string? consigneeraddress { get; set; }
    }
}