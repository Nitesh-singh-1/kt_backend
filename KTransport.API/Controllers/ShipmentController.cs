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
    [RequireFeature(FeatureConstants.GOOD_RECEIPT, FeatureConstants.SHIPMENT, "bilty", "consignments")]
    public class ShipmentController : ControllerBase
    {
        private readonly IShipmentService _shipmentService;

        public ShipmentController(IShipmentService shipmentService)
        {
            _shipmentService = shipmentService;
        }

        [HttpPost]
        [RequirePermission("consignments.create.create")]
        public async Task<ActionResult<ShipmentResponse>> CreateShipment([FromBody] CreateShipmentRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _shipmentService.CreateShipmentAsync(request, userId);

            if (!response.Success) return BadRequest(response);

            return CreatedAtAction(nameof(GetShipmentById), new { id = response.Data?.Id }, response);
        }

        [HttpGet("{id:long}")]
        [RequirePermission("consignments.all.view", "consignments.create.view", "consignments.view", "consignments.create.create")]
        public async Task<ActionResult<ShipmentResponse>> GetShipmentById(long id)
        {
            var response = await _shipmentService.GetShipmentByIdAsync(id);
            if (!response.Success) return NotFound(response);
            return Ok(response);
        }

        [HttpGet("by-no/{shipmentNo}")]
        [RequirePermission("consignments.all.view", "consignments.create.view", "consignments.view", "consignments.create.create")]
        public async Task<ActionResult<ShipmentResponse>> GetShipmentByNo(string shipmentNo)
        {
            var response = await _shipmentService.GetShipmentByNoAsync(shipmentNo);
            if (!response.Success) return NotFound(response);
            return Ok(response);
        }

        [HttpGet]
        [RequirePermission("consignments.all.view", "consignments.create.view", "consignments.view", "consignments.create.create")]
        public async Task<ActionResult<ShipmentListResponse>> GetAllShipments(
            [FromQuery] ShipmentStatus? status,
            [FromQuery] TaxTreatment? taxTreatment,
            [FromQuery] PaymentTerm? paymentTerm,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var response = await _shipmentService.GetAllShipmentsAsync(status, taxTreatment, paymentTerm, search, page, pageSize);
            return Ok(response);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("consignments.create.edit", "consignments.all.edit")]
        public async Task<ActionResult<ShipmentResponse>> UpdateShipment(long id, [FromBody] UpdateShipmentRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _shipmentService.UpdateShipmentAsync(id, request, userId);

            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpPatch("{id:long}/status")]
        [RequirePermission("consignments.create.edit", "consignments.all.edit")]
        public async Task<ActionResult<ShipmentResponse>> UpdateShipmentStatus(long id, [FromBody] UpdateShipmentStatusRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _shipmentService.UpdateShipmentStatusAsync(id, request, userId);

            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("consignments.all.delete", "consignments.create.delete")]
        public async Task<IActionResult> DeleteShipment(long id)
        {
            var userId = GetCurrentUserId();
            var success = await _shipmentService.DeleteShipmentAsync(id, userId);
            if (!success) return NotFound(new { message = "Shipment not found." });

            return Ok(new { success = true, message = "Shipment cancelled successfully." });
        }

        [HttpPost("settle-delivery")]
        [RequirePermission("delivery_settlement.create", "delivery_settlement.edit")]
        public async Task<ActionResult<SettleDeliveryResponseDto>> SettleDelivery([FromBody] SettleDeliveryRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var response = await _shipmentService.SettleDeliveryAsync(request, userId);

            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpGet("delivery-settlement-summary")]
        [RequirePermission("delivery_settlement.view")]
        public async Task<ActionResult<DeliverySettlementSummaryResponse>> GetDeliverySettlementSummary()
        {
            var response = await _shipmentService.GetDeliverySettlementSummaryAsync();
            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out var id) ? id : 1;
        }
    }
}
