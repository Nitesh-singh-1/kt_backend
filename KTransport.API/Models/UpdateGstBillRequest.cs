namespace KTransport.API.Models
{
    public class UpdateGstBillRequest
    {
        public string? InvoiceNo { get; set; }

        public string? FromLocation { get; set; }

        public string? ToLocation { get; set; }

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
    }
}