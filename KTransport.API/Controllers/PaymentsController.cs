using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// Payment-ledger endpoints for a bilty: list the money-in history and reverse a single
    /// payment. Reversal needs the same consignment edit permission as editing the bilty — the
    /// action lives on the bilty screen — plus (implicitly) access to it. Bill Book payments are
    /// reversed via the bill, not here.
    /// </summary>
    [ApiController]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentLedgerService _ledger;

        public PaymentsController(IPaymentLedgerService ledger)
        {
            _ledger = ledger;
        }

        [HttpGet("api/shipment/{shipmentId:long}/payments")]
        [RequirePermission("consignments.all.view", "consignments.create.view", "consignments.view", "consignments.create.create")]
        public async Task<ActionResult<List<ShipmentPaymentDto>>> GetForShipment(long shipmentId)
        {
            var payments = await _ledger.GetByShipmentAsync(shipmentId);
            return Ok(payments);
        }

        [HttpPost("api/payment/{id:long}/reverse")]
        [RequirePermission("consignments.create.edit", "consignments.all.edit")]
        public async Task<IActionResult> Reverse(long id, [FromBody] ReversePaymentRequest? request)
        {
            var userId = GetCurrentUserId() ?? 0;
            var (success, message) = await _ledger.ReverseAsync(id, request?.Reason, userId);
            if (!success) return BadRequest(new { success = false, message });
            return Ok(new { success = true, message });
        }

        private int? GetCurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out int id) ? id : null;
        }
    }
}
