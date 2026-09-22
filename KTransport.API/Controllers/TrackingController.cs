using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TrackingController : ControllerBase
    {
        private readonly ITrackingService _trackingService;

        public TrackingController(ITrackingService trackingService)
        {
            _trackingService = trackingService;
        }

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
    }
}
