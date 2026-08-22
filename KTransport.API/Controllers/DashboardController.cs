using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KTransport.API.Models;
using KTransport.API.Services;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IDashboardService dashboardService, ILogger<DashboardController> logger)
        {
            _dashboardService = dashboardService;
            _logger = logger;
        }

        /// <summary>
        /// Get overall dashboard statistics
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<DashboardResponse>> GetDashboardStats()
        {
            var response = await _dashboardService.GetDashboardStatsAsync();

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get revenue statistics
        /// </summary>
        [HttpGet("revenue")]
        public async Task<ActionResult<RevenueResponse>> GetRevenueStats()
        {
            var response = await _dashboardService.GetRevenueStatsAsync();

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>
        /// Get dashboard statistics for a specific date range
        /// </summary>
        [HttpGet("stats/daterange")]
        public async Task<ActionResult<DashboardResponse>> GetDashboardStatsByDateRange(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            if (startDate > endDate)
            {
                return BadRequest(new DashboardResponse
                {
                    Success = false,
                    Message = "Start date cannot be greater than end date"
                });
            }

            var response = await _dashboardService.GetDashboardStatsByDateRangeAsync(startDate, endDate);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
}