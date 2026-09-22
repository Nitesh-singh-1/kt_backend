using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("booking-register")]
        public async Task<ActionResult<BookingRegisterReportDto>> GetBookingRegister(
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null)
        {
            var report = await _reportService.GetBookingRegisterAsync(fromDate, toDate);
            return Ok(report);
        }

        [HttpGet("trip-profitability")]
        public async Task<ActionResult<TripProfitabilityReportDto>> GetTripProfitability(
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null)
        {
            var report = await _reportService.GetTripProfitabilityReportAsync(fromDate, toDate);
            return Ok(report);
        }

        [HttpGet("tax-summary")]
        public async Task<ActionResult<TaxSummaryReportDto>> GetTaxSummary(
            [FromQuery] DateOnly? fromDate = null,
            [FromQuery] DateOnly? toDate = null)
        {
            var report = await _reportService.GetTaxSummaryReportAsync(fromDate, toDate);
            return Ok(report);
        }

        [HttpGet("party-outstanding")]
        public async Task<ActionResult<List<PartyOutstandingReportDto>>> GetPartyOutstanding()
        {
            var report = await _reportService.GetPartyOutstandingReportAsync();
            return Ok(report);
        }

        [HttpGet("vendor-payables")]
        public async Task<ActionResult<List<VendorPayableReportDto>>> GetVendorPayables()
        {
            var report = await _reportService.GetVendorPayableReportAsync();
            return Ok(report);
        }
    }
}
