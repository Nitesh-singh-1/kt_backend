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
        [RequirePermission("master_data.fleet.view")]
        public async Task<ActionResult<List<VehicleDto>>> GetVehicles(
            [FromQuery] string? search = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var result = await _fleetService.GetVehiclesAsync(search);
            var slice = PaginationHelper.Paginate(result, HttpContext, page, pageSize);
            return Ok(slice);
        }

        [HttpGet("vehicles/lookup")]
        [RequirePermission("master_data.fleet.view")]
        public async Task<ActionResult<List<VehicleLookupDto>>> GetVehicleLookup([FromQuery] string? q = null)
        {
            var result = await _fleetService.GetVehicleLookupAsync(q);
            return Ok(result);
        }

        [HttpGet("vehicles/{id:long}")]
        [RequirePermission("master_data.fleet.view")]
        public async Task<ActionResult<VehicleDto>> GetVehicleById(long id)
        {
            var result = await _fleetService.GetVehicleByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("vehicles")]
        [RequirePermission("master_data.fleet.create")]
        public async Task<ActionResult<VehicleDto>> CreateVehicle([FromBody] CreateVehicleRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.CreateVehicleAsync(request);
            return CreatedAtAction(nameof(GetVehicleById), new { id = result.Id }, result);
        }

        [HttpPut("vehicles/{id:long}")]
        [RequirePermission("master_data.fleet.edit")]
        public async Task<ActionResult<VehicleDto>> UpdateVehicle(long id, [FromBody] UpdateVehicleRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.UpdateVehicleAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("vehicles/{id:long}")]
        [RequirePermission("master_data.fleet.delete")]
        public async Task<ActionResult> DeleteVehicle(long id)
        {
            var success = await _fleetService.DeleteVehicleAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // Drivers
        [HttpGet("drivers")]
        [RequirePermission("master_data.fleet.view")]
        public async Task<ActionResult<List<DriverDto>>> GetDrivers(
            [FromQuery] string? search = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var result = await _fleetService.GetDriversAsync(search);
            var slice = PaginationHelper.Paginate(result, HttpContext, page, pageSize);
            return Ok(slice);
        }

        [HttpGet("drivers/lookup")]
        [RequirePermission("master_data.fleet.view")]
        public async Task<ActionResult<List<DriverLookupDto>>> GetDriverLookup([FromQuery] string? q = null)
        {
            var result = await _fleetService.GetDriverLookupAsync(q);
            return Ok(result);
        }

        [HttpGet("drivers/{id:long}")]
        [RequirePermission("master_data.fleet.view")]
        public async Task<ActionResult<DriverDto>> GetDriverById(long id)
        {
            var result = await _fleetService.GetDriverByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("drivers")]
        [RequirePermission("master_data.fleet.create")]
        public async Task<ActionResult<DriverDto>> CreateDriver([FromBody] CreateDriverRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.CreateDriverAsync(request);
            return CreatedAtAction(nameof(GetDriverById), new { id = result.Id }, result);
        }

        [HttpPut("drivers/{id:long}")]
        [RequirePermission("master_data.fleet.edit")]
        public async Task<ActionResult<DriverDto>> UpdateDriver(long id, [FromBody] UpdateDriverRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.UpdateDriverAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("drivers/{id:long}")]
        [RequirePermission("master_data.fleet.delete")]
        public async Task<ActionResult> DeleteDriver(long id)
        {
            var success = await _fleetService.DeleteDriverAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // Compliance — expiring / expired statutory documents across the fleet
        [HttpGet("compliance")]
        [RequirePermission("master_data.compliance.view", "master_data.fleet.view")]
        public async Task<ActionResult<ComplianceOverviewDto>> GetComplianceAlerts([FromQuery] int withinDays = 30)
        {
            var result = await _fleetService.GetComplianceAlertsAsync(withinDays);
            return Ok(result);
        }

        // Locations
        [HttpGet("locations")]
        [RequirePermission("master_data.fleet.view", "master_data.parties.view")]
        public async Task<ActionResult<List<LocationDto>>> GetLocations(
            [FromQuery] string? search = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var result = await _fleetService.GetLocationsAsync(search);
            var slice = PaginationHelper.Paginate(result, HttpContext, page, pageSize);
            return Ok(slice);
        }

        [HttpGet("locations/lookup")]
        [RequirePermission("master_data.fleet.view", "master_data.parties.view")]
        public async Task<ActionResult<List<LocationLookupDto>>> GetLocationLookup([FromQuery] string? q = null)
        {
            var result = await _fleetService.GetLocationLookupAsync(q);
            return Ok(result);
        }

        [HttpGet("locations/{id:long}")]
        [RequirePermission("master_data.fleet.view", "master_data.parties.view")]
        public async Task<ActionResult<LocationDto>> GetLocationById(long id)
        {
            var result = await _fleetService.GetLocationByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("locations")]
        [RequirePermission("master_data.fleet.create", "master_data.parties.create")]
        public async Task<ActionResult<LocationDto>> CreateLocation([FromBody] CreateLocationRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.CreateLocationAsync(request);
            return CreatedAtAction(nameof(GetLocationById), new { id = result.Id }, result);
        }

        [HttpPut("locations/{id:long}")]
        [RequirePermission("master_data.fleet.edit", "master_data.parties.edit")]
        public async Task<ActionResult<LocationDto>> UpdateLocation(long id, [FromBody] UpdateLocationRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _fleetService.UpdateLocationAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("locations/{id:long}")]
        [RequirePermission("master_data.fleet.delete", "master_data.parties.delete")]
        public async Task<ActionResult> DeleteLocation(long id)
        {
            var success = await _fleetService.DeleteLocationAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
