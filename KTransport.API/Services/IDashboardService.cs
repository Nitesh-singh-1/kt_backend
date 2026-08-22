using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IDashboardService
    {
        Task<DashboardResponse> GetDashboardStatsAsync();
        Task<RevenueResponse> GetRevenueStatsAsync();
        Task<DashboardResponse> GetDashboardStatsByDateRangeAsync(DateTime startDate, DateTime endDate);
    }
}