using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackingController : ControllerBase
    {
        private readonly ITrackingService _trackingService;
        private readonly KTransportDbContext _context;
        private readonly ITenantContext _tenantContext;

        public TrackingController(ITrackingService trackingService, KTransportDbContext context, ITenantContext tenantContext)
        {
            _trackingService = trackingService;
            _context = context;
            _tenantContext = tenantContext;
        }

        /// <summary>
        /// Public tracking by Bilty / Consignment / Invoice Number.
        /// </summary>
        [HttpGet("{trackingNo}")]
        [AllowAnonymous]
        public async Task<ActionResult<PublicTrackingDto>> TrackShipment(string trackingNo)
        {
            var info = await _trackingService.GetTrackingInfoAsync(trackingNo);
            if (info == null)
            {
                return NotFound(new { message = $"No shipment found with tracking number '{trackingNo}'." });
            }

            return Ok(info);
        }

        /// <summary>
        /// Internal live tracking dashboard feed for the authenticated tenant (Protected by TRACKING module subscription and user permission).
        /// </summary>
        [HttpGet]
        [Authorize]
        [RequireFeature(FeatureConstants.TRACKING)]
        public async Task<IActionResult> GetLiveTrackingList([FromQuery] string? search = null)
        {
            var tenantId = _tenantContext.CurrentTenantId;
            var query = _context.Shipments
                .Where(s => s.TenantId == tenantId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(x => x.ShipmentNo.ToLower().Contains(s) || 
                                         (x.TruckNo != null && x.TruckNo.ToLower().Contains(s)) ||
                                         (x.ConsigneeName != null && x.ConsigneeName.ToLower().Contains(s)));
            }

            var shipments = await query
                .OrderByDescending(s => s.ShipmentDate)
                .Take(50)
                .Select(s => new
                {
                    s.Id,
                    s.ShipmentNo,
                    s.ShipmentDate,
                    s.TruckNo,
                    s.FromLocation,
                    s.ToLocation,
                    s.ConsignorName,
                    s.ConsigneeName,
                    Status = s.Status.ToString(),
                    s.GrandTotal
                })
                .ToListAsync();

            return Ok(shipments);
        }
    }
}
