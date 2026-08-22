namespace KTransport.API.DTOs
{
    public class CreateChargeRequest
    {
        public int BillId { get; set; }
        public decimal? Freight { get; set; }
        public decimal? ServiceCharge { get; set; }
        public decimal? DdCharge { get; set; }
        public decimal? Hamali { get; set; }
        public decimal? OtherCharge { get; set; }
        public decimal? StCharge { get; set; }
        public decimal? GrandTotal { get; set; }
    }

    public class UpdateChargeRequest
    {
        public decimal? Freight { get; set; }
        public decimal? ServiceCharge { get; set; }
        public decimal? DdCharge { get; set; }
        public decimal? Hamali { get; set; }
        public decimal? OtherCharge { get; set; }
        public decimal? StCharge { get; set; }
        public decimal? GrandTotal { get; set; }
    }

    public class ChargeResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ChargeDto? Data { get; set; }
    }

    public class ChargeDto
    {
        public int Id { get; set; }
        public int BillId { get; set; }
        public decimal? Freight { get; set; }
        public decimal? ServiceCharge { get; set; }
        public decimal? DdCharge { get; set; }
        public decimal? Hamali { get; set; }
        public decimal? OtherCharge { get; set; }
        public decimal? StCharge { get; set; }
        public decimal? GrandTotal { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}