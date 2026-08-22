namespace KTransport.API.Models
{
    public class DashboardResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public DashboardStats? Data { get; set; }
    }

    public class DashboardStats
    {
        public int TotalGrEntries { get; set; }
        public int PendingDeliveries { get; set; }
        public int CompletedDeliveries { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TodayEntries { get; set; }
        public int ThisMonthEntries { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal CollectedAmount { get; set; }
        public List<RecentBillDto> RecentBills { get; set; } = new();
        public List<DeliveryStatusCount> DeliveryStatusBreakdown { get; set; } = new();
    }

    public class RecentBillDto
    {
        public int Id { get; set; }
        public string? GrNo { get; set; }
        public string? ConsigneeName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? DeliveryStatus { get; set; }
        public decimal? TotalAmount { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class DeliveryStatusCount
    {
        public string? Status { get; set; }
        public int Count { get; set; }
    }

    public class RevenueResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public RevenueStats? Data { get; set; }
    }

    public class RevenueStats
    {
        public decimal TotalRevenue { get; set; }
        public decimal TodayRevenue { get; set; }
        public decimal ThisMonthRevenue { get; set; }
        public decimal ThisYearRevenue { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal ToPayAmount { get; set; }
        public decimal TbbAmount { get; set; }
    }
}