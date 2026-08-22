using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ILogger<DashboardService> _logger;
        private readonly KTransportDbContext _context;

        public DashboardService(ILogger<DashboardService> logger, KTransportDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<DashboardResponse> GetDashboardStatsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving dashboard statistics");

                var today = DateTime.Today;
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                // Get all active GST bills
                var allBills = await _context.GstBills
                    .Where(b => b.IsActive == true)
                    .ToListAsync();

                // 1. Total GR Entries
                var totalGrEntries = allBills.Count;

                // 2. Pending Deliveries (status != "Delivered" or "Completed")
                var pendingDeliveries = allBills
                    .Count(b => b.DeliveryStatus != null && 
                           !b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) &&
                           !b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase));

                // 3. Completed Deliveries (status = "Delivered" or "Completed")
                var completedDeliveries = allBills
                    .Count(b => b.DeliveryStatus != null && 
                           (b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) ||
                            b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)));

                // 4. Total Revenue (sum of all total amounts)
                var totalRevenue = allBills.Sum(b => b.TotalAmount ?? 0);

                // Additional stats
                var todayEntries = allBills.Count(b => b.CreatedAt.HasValue && b.CreatedAt.Value.Date == today);
                var thisMonthEntries = allBills.Count(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfMonth);

                // Pending and collected amounts
                var pendingAmount = allBills.Sum(b => b.ToPay ?? 0);
                var collectedAmount = allBills.Sum(b => b.Paid ?? 0);

                // Recent bills (last 10)
                var recentBills = await _context.GstBills
                    .Where(b => b.IsActive == true)
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(10)
                    .Select(b => new RecentBillDto
                    {
                        Id = b.Id,
                        GrNo = b.GrNo,
                        ConsigneeName = b.ConsigneeName,
                        FromLocation = b.FromLocation,
                        ToLocation = b.ToLocation,
                        DeliveryStatus = b.DeliveryStatus,
                        TotalAmount = b.TotalAmount,
                        CreatedAt = b.CreatedAt
                    })
                    .ToListAsync();

                // Delivery status breakdown
                var statusBreakdown = allBills
                    .GroupBy(b => b.DeliveryStatus ?? "Unknown")
                    .Select(g => new DeliveryStatusCount
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var dashboardStats = new DashboardStats
                {
                    TotalGrEntries = totalGrEntries,
                    PendingDeliveries = pendingDeliveries,
                    CompletedDeliveries = completedDeliveries,
                    TotalRevenue = totalRevenue,
                    TodayEntries = todayEntries,
                    ThisMonthEntries = thisMonthEntries,
                    PendingAmount = pendingAmount,
                    CollectedAmount = collectedAmount,
                    RecentBills = recentBills,
                    DeliveryStatusBreakdown = statusBreakdown
                };

                return new DashboardResponse
                {
                    Success = true,
                    Message = "Dashboard statistics retrieved successfully",
                    Data = dashboardStats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard statistics");
                return new DashboardResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving dashboard statistics"
                };
            }
        }

        public async Task<RevenueResponse> GetRevenueStatsAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving revenue statistics");

                var today = DateTime.Today;
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var startOfYear = new DateTime(today.Year, 1, 1);

                var allBills = await _context.GstBills
                    .Where(b => b.IsActive == true)
                    .ToListAsync();

                var totalRevenue = allBills.Sum(b => b.TotalAmount ?? 0);
                var todayRevenue = allBills
                    .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value.Date == today)
                    .Sum(b => b.TotalAmount ?? 0);
                var thisMonthRevenue = allBills
                    .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfMonth)
                    .Sum(b => b.TotalAmount ?? 0);
                var thisYearRevenue = allBills
                    .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfYear)
                    .Sum(b => b.TotalAmount ?? 0);

                var paidAmount = allBills.Sum(b => b.Paid ?? 0);
                var toPayAmount = allBills.Sum(b => b.ToPay ?? 0);
                var tbbAmount = allBills.Sum(b => b.Tbb ?? 0);

                var revenueStats = new RevenueStats
                {
                    TotalRevenue = totalRevenue,
                    TodayRevenue = todayRevenue,
                    ThisMonthRevenue = thisMonthRevenue,
                    ThisYearRevenue = thisYearRevenue,
                    PaidAmount = paidAmount,
                    ToPayAmount = toPayAmount,
                    TbbAmount = tbbAmount
                };

                return new RevenueResponse
                {
                    Success = true,
                    Message = "Revenue statistics retrieved successfully",
                    Data = revenueStats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving revenue statistics");
                return new RevenueResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving revenue statistics"
                };
            }
        }

        public async Task<DashboardResponse> GetDashboardStatsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Retrieving dashboard statistics for date range: {StartDate} to {EndDate}", startDate, endDate);

                var billsInRange = await _context.GstBills
                    .Where(b => b.IsActive == true && 
                           b.CreatedAt.HasValue && 
                           b.CreatedAt.Value.Date >= startDate.Date && 
                           b.CreatedAt.Value.Date <= endDate.Date)
                    .ToListAsync();

                var totalGrEntries = billsInRange.Count;

                var pendingDeliveries = billsInRange
                    .Count(b => b.DeliveryStatus != null && 
                           !b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) &&
                           !b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase));

                var completedDeliveries = billsInRange
                    .Count(b => b.DeliveryStatus != null && 
                           (b.DeliveryStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase) ||
                            b.DeliveryStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)));

                var totalRevenue = billsInRange.Sum(b => b.TotalAmount ?? 0);
                var pendingAmount = billsInRange.Sum(b => b.ToPay ?? 0);
                var collectedAmount = billsInRange.Sum(b => b.Paid ?? 0);

                var recentBills = billsInRange
                    .OrderByDescending(b => b.CreatedAt)
                    .Take(10)
                    .Select(b => new RecentBillDto
                    {
                        Id = b.Id,
                        GrNo = b.GrNo,
                        ConsigneeName = b.ConsigneeName,
                        FromLocation = b.FromLocation,
                        ToLocation = b.ToLocation,
                        DeliveryStatus = b.DeliveryStatus,
                        TotalAmount = b.TotalAmount,
                        CreatedAt = b.CreatedAt
                    })
                    .ToList();

                var statusBreakdown = billsInRange
                    .GroupBy(b => b.DeliveryStatus ?? "Unknown")
                    .Select(g => new DeliveryStatusCount
                    {
                        Status = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Count)
                    .ToList();

                var dashboardStats = new DashboardStats
                {
                    TotalGrEntries = totalGrEntries,
                    PendingDeliveries = pendingDeliveries,
                    CompletedDeliveries = completedDeliveries,
                    TotalRevenue = totalRevenue,
                    TodayEntries = 0,
                    ThisMonthEntries = 0,
                    PendingAmount = pendingAmount,
                    CollectedAmount = collectedAmount,
                    RecentBills = recentBills,
                    DeliveryStatusBreakdown = statusBreakdown
                };

                return new DashboardResponse
                {
                    Success = true,
                    Message = $"Dashboard statistics for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} retrieved successfully",
                    Data = dashboardStats
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard statistics by date range");
                return new DashboardResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving dashboard statistics"
                };
            }
        }
    }
}