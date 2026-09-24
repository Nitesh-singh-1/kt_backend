using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireFeature(FeatureConstants.VEHICLE)]
    public class FleetController : ControllerBase
    {
        private readonly IFleetService _fleetService;

        public FleetController(IFleetService fleetService)
        {
            _fleetService = fleetService;
        }

        // Vehicles
        [HttpGet("vehicles")]
        public async Task<ActionResult<List<VehicleDto>>> GetVehicles([FromQuery] string? search = null)
        {
            var result = await _fleetService.GetVehiclesAsync(search);
            return Ok(result);
        }

        [HttpGet("vehicles/lookup")]
        public async Task<ActionResult<List<VehicleLookupDto>>> GetVehicleLookup([FromQuery] string? q = null)
        {
            var result = await _fleetService.GetVehicleLookupAsync(q);
            return Ok(result);
        }

        [HttpGet("vehicles/{id:long}")]
        public async Task<ActionResult<VehicleDto>> GetVehicleById(long id)
        {
            var result = await _fleetService.GetVehicleByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("vehicles")]
        public async Task<ActionResult<VehicleDto>> CreateVehicle([FromBody] CreateVehicleRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.CreateVehicleAsync(request);
            return CreatedAtAction(nameof(GetVehicleById), new { id = result.Id }, result);
        }

        [HttpDelete("vehicles/{id:long}")]
        public async Task<ActionResult> DeleteVehicle(long id)
        {
            var success = await _fleetService.DeleteVehicleAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // Drivers
        [HttpGet("drivers")]
        public async Task<ActionResult<List<DriverDto>>> GetDrivers([FromQuery] string? search = null)
        {
            var result = await _fleetService.GetDriversAsync(search);
            return Ok(result);
        }

        [HttpGet("drivers/lookup")]
        public async Task<ActionResult<List<DriverLookupDto>>> GetDriverLookup([FromQuery] string? q = null)
        {
            var result = await _fleetService.GetDriverLookupAsync(q);
            return Ok(result);
        }

        [HttpGet("drivers/{id:long}")]
        public async Task<ActionResult<DriverDto>> GetDriverById(long id)
        {
            var result = await _fleetService.GetDriverByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("drivers")]
        public async Task<ActionResult<DriverDto>> CreateDriver([FromBody] CreateDriverRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.CreateDriverAsync(request);
            return CreatedAtAction(nameof(GetDriverById), new { id = result.Id }, result);
        }

        [HttpDelete("drivers/{id:long}")]
        public async Task<ActionResult> DeleteDriver(long id)
        {
            var success = await _fleetService.DeleteDriverAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // Locations
        [HttpGet("locations")]
        public async Task<ActionResult<List<LocationDto>>> GetLocations([FromQuery] string? search = null)
        {
            var result = await _fleetService.GetLocationsAsync(search);
            return Ok(result);
        }

        [HttpGet("locations/lookup")]
        public async Task<ActionResult<List<LocationLookupDto>>> GetLocationLookup([FromQuery] string? q = null)
        {
            var result = await _fleetService.GetLocationLookupAsync(q);
            return Ok(result);
        }

        [HttpGet("locations/{id:long}")]
        public async Task<ActionResult<LocationDto>> GetLocationById(long id)
        {
            var result = await _fleetService.GetLocationByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("locations")]
        public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] CreateLocationRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.CreateLocationAsync(request);
            return CreatedAtAction(nameof(GetLocationById), new { id = result.Id }, result);
        }

        [HttpDelete("locations/{id:long}")]
        public async Task<ActionResult> DeleteLocation(long id)
        {
            var success = await _fleetService.DeleteLocationAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
