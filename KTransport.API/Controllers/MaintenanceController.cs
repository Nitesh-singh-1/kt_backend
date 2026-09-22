using System.Collections.Generic;
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
    public class MaintenanceController : ControllerBase
    {
        private readonly IMaintenanceService _maintenanceService;

        public MaintenanceController(IMaintenanceService maintenanceService)
        {
            _maintenanceService = maintenanceService;
        }

        [HttpGet]
        public async Task<ActionResult<List<VehicleMaintenanceDto>>> GetMaintenanceRecords(
            [FromQuery] long? vehicleId = null,
            [FromQuery] MaintenanceType? maintenanceType = null)
        {
            var records = await _maintenanceService.GetMaintenanceRecordsAsync(vehicleId, maintenanceType);
            return Ok(records);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleMaintenanceDto>> GetMaintenanceById(long id)
        {
            var record = await _maintenanceService.GetMaintenanceByIdAsync(id);
            if (record == null) return NotFound();
            return Ok(record);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleMaintenanceDto>> CreateMaintenance([FromBody] CreateMaintenanceRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var record = await _maintenanceService.CreateMaintenanceAsync(request);
            return CreatedAtAction(nameof(GetMaintenanceById), new { id = record.Id }, record);
        }

        [HttpDelete("{id:long}")]
        public async Task<ActionResult> DeleteMaintenance(long id)
        {
            var success = await _maintenanceService.DeleteMaintenanceAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
