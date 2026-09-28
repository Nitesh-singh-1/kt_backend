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

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid Consigner GSTIN format.")]
        public string? ConsignerGstNo { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Consigner mobile must be a valid 10-digit Indian mobile number.")]
        public string? ConsignerMobile { get; set; }

        public string? ConsigneeName { get; set; }

        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$", ErrorMessage = "Invalid Consignee GSTIN format.")]
        public string? ConsigneeGstNo { get; set; }

        [RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Consignee mobile must be a valid 10-digit Indian mobile number.")]
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