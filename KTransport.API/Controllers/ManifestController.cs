using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireFeature(FeatureConstants.MANIFEST)]
    public class ManifestController : ControllerBase
    {
        private readonly IManifestService _manifestService;

        public ManifestController(IManifestService manifestService)
        {
            _manifestService = manifestService;
        }

        [HttpPost]
        public async Task<ActionResult<ManifestResponse>> CreateManifest([FromBody] CreateManifestRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _manifestService.CreateManifestAsync(request, userId);

            if (!response.Success) return BadRequest(response);

            return CreatedAtAction(nameof(GetManifestById), new { id = response.Data?.Id }, response);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<ManifestResponse>> GetManifestById(long id)
        {
            var response = await _manifestService.GetManifestByIdAsync(id);
            if (!response.Success) return NotFound(response);
            return Ok(response);
        }

        [HttpGet("by-no/{manifestNo}")]
        public async Task<ActionResult<ManifestResponse>> GetManifestByNo(string manifestNo)
        {
            var response = await _manifestService.GetManifestByNoAsync(manifestNo);
            if (!response.Success) return NotFound(response);
            return Ok(response);
        }

        [HttpGet]
        public async Task<ActionResult<ManifestListResponse>> GetManifests([FromQuery] ManifestFilterRequest filter)
        {
            var response = await _manifestService.GetManifestsAsync(filter);
            return Ok(response);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<ManifestResponse>> UpdateManifest(long id, [FromBody] UpdateManifestRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _manifestService.UpdateManifestAsync(id, request, userId);

            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpPost("{id:long}/assign-trip")]
        public async Task<ActionResult<ManifestResponse>> AssignTrip(long id, [FromBody] AssignTripToManifestRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _manifestService.AssignTripAsync(id, request.TripId, userId);

            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpPost("{id:long}/dispatch")]
        public async Task<ActionResult<ManifestResponse>> DispatchManifest(long id)
        {
            var userId = GetCurrentUserId();
            var response = await _manifestService.DispatchManifestAsync(id, userId);

            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpPost("{id:long}/unload")]
        public async Task<ActionResult<ManifestResponse>> UnloadManifest(long id, [FromBody] UnloadManifestRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _manifestService.UnloadManifestAsync(id, request, userId);

            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        public async Task<IActionResult> DeleteManifest(long id)
        {
            var userId = GetCurrentUserId();
            var success = await _manifestService.DeleteManifestAsync(id, userId);
            if (!success) return NotFound(new { message = "Manifest not found." });

            return Ok(new { success = true, message = "Manifest cancelled successfully." });
        }

        [HttpGet("available-shipments")]
        public async Task<ActionResult<List<ShipmentDto>>> GetAvailableShipments(
            [FromQuery] long? originHubId, 
            [FromQuery] long? destinationHubId)
        {
            var shipments = await _manifestService.GetAvailableShipmentsForManifestAsync(originHubId, destinationHubId);
            return Ok(shipments);
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : 1;
        }
    }
}
