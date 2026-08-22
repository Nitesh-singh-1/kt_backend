namespace KTransport.API.Models
{
    public class CreateGoodsDetailRequest
    {
        public int BillId { get; set; }
        public string? Article { get; set; }
        public string? Description { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Rate { get; set; }
    }

    public class UpdateGoodsDetailRequest
    {
        public string? Article { get; set; }
        public string? Description { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Rate { get; set; }
    }

    public class GoodsDetailResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public GoodsDetailDto? Data { get; set; }
    }

    public class GoodsDetailListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<GoodsDetailDto> Data { get; set; } = new();
    }

    public class GoodsDetailDto
    {
        public int Id { get; set; }
        public int BillId { get; set; }
        public string? Article { get; set; }
        public string? Description { get; set; }
        public decimal? Weight { get; set; }
        public decimal? Rate { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}