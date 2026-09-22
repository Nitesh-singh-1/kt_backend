using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
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
    public class PodController : ControllerBase
    {
        private readonly IPodService _podService;

        public PodController(IPodService podService)
        {
            _podService = podService;
        }

        [HttpGet]
        public async Task<ActionResult<List<PodRecordDto>>> GetPods(
            [FromQuery] PodStatus? status = null,
            [FromQuery] string? search = null)
        {
            var pods = await _podService.GetPodsAsync(status, search);
            return Ok(pods);
        }

        [HttpGet("shipment/{shipmentId:long}")]
        public async Task<ActionResult<PodRecordDto>> GetPodByShipmentId(long shipmentId)
        {
            var pod = await _podService.GetPodByShipmentIdAsync(shipmentId);
            if (pod == null) return NotFound(new { message = $"POD for shipment {shipmentId} not found." });
            return Ok(pod);
        }

        [HttpPost("upload")]
        public async Task<ActionResult<PodRecordDto>> UploadPod([FromBody] UploadPodRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var pod = await _podService.UploadPodAsync(request, userId);
            return Ok(pod);
        }

        [HttpPost("{id:long}/verify")]
        public async Task<ActionResult<PodRecordDto>> VerifyPod(long id, [FromBody] VerifyPodRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var pod = await _podService.VerifyPodAsync(id, request, userId);
            if (pod == null) return NotFound(new { message = $"POD with ID {id} not found." });
            return Ok(pod);
        }

        [HttpDelete("{id:long}")]
        public async Task<ActionResult> DeletePod(long id)
        {
            var success = await _podService.DeletePodAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        private int? GetCurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out int id) ? id : null;
        }
    }
}
