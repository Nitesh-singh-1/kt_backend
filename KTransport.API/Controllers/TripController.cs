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
    public class TripController : ControllerBase
    {
        private readonly ITripService _tripService;

        public TripController(ITripService tripService)
        {
            _tripService = tripService;
        }

        [HttpGet]
        public async Task<ActionResult<List<TripDto>>> GetTrips(
            [FromQuery] string? search = null,
            [FromQuery] TripStatus? status = null)
        {
            var trips = await _tripService.GetTripsAsync(search, status);
            return Ok(trips);
        }

        [HttpGet("lookup")]
        public async Task<ActionResult<List<TripLookupDto>>> GetTripLookup([FromQuery] string? q = null)
        {
            var results = await _tripService.GetTripLookupAsync(q);
            return Ok(results);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<TripDto>> GetTripById(long id)
        {
            var trip = await _tripService.GetTripByIdAsync(id);
            if (trip == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(trip);
        }

        [HttpPost]
        public async Task<ActionResult<TripDto>> CreateTrip([FromBody] CreateTripRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var trip = await _tripService.CreateTripAsync(request, userId);
            return CreatedAtAction(nameof(GetTripById), new { id = trip.Id }, trip);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<TripDto>> UpdateTrip(long id, [FromBody] UpdateTripRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var updated = await _tripService.UpdateTripAsync(id, request, userId);
            if (updated == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(updated);
        }

        [HttpPost("{id:long}/shipments")]
        public async Task<ActionResult<TripDto>> LoadShipments(long id, [FromBody] LoadShipmentsRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var updated = await _tripService.LoadShipmentsAsync(id, request, userId);
            if (updated == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(updated);
        }

        [HttpDelete("{id:long}/shipments/{shipmentId:long}")]
        public async Task<ActionResult<TripDto>> RemoveShipment(long id, long shipmentId)
        {
            int? userId = GetCurrentUserId();
            var updated = await _tripService.RemoveShipmentAsync(id, shipmentId, userId);
            if (updated == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(updated);
        }

        [HttpPost("{id:long}/dispatch")]
        public async Task<ActionResult<TripDto>> DispatchTrip(long id)
        {
            int? userId = GetCurrentUserId();
            var updated = await _tripService.DispatchTripAsync(id, userId);
            if (updated == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(updated);
        }

        [HttpPost("{id:long}/arrive")]
        public async Task<ActionResult<TripDto>> ArriveTrip(long id)
        {
            int? userId = GetCurrentUserId();
            var updated = await _tripService.ArriveTripAsync(id, userId);
            if (updated == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(updated);
        }

        [HttpPost("{id:long}/complete")]
        public async Task<ActionResult<TripDto>> CompleteTrip(long id, [FromQuery] decimal endOdometer = 0)
        {
            int? userId = GetCurrentUserId();
            var updated = await _tripService.CompleteTripAsync(id, endOdometer, userId);
            if (updated == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(updated);
        }

        [HttpPost("{id:long}/cancel")]
        public async Task<ActionResult<TripDto>> CancelTrip(long id, [FromQuery] string? reason = null)
        {
            int? userId = GetCurrentUserId();
            var updated = await _tripService.CancelTripAsync(id, reason, userId);
            if (updated == null) return NotFound(new { message = $"Trip with ID {id} not found." });
            return Ok(updated);
        }

        [HttpPost("{id:long}/expenses")]
        public async Task<ActionResult<TripExpenseDto>> AddTripExpense(long id, [FromBody] AddTripExpenseRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var expense = await _tripService.AddTripExpenseAsync(id, request, userId);
            return Ok(expense);
        }

        [HttpDelete("{id:long}/expenses/{expenseId:long}")]
        public async Task<ActionResult> DeleteTripExpense(long id, long expenseId)
        {
            var success = await _tripService.DeleteTripExpenseAsync(id, expenseId);
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
