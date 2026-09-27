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
    public class VehicleInsuranceClaimController : ControllerBase
    {
        private readonly IVehicleInsuranceClaimService _service;

        public VehicleInsuranceClaimController(IVehicleInsuranceClaimService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<VehicleInsuranceClaimDto>>> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] VehicleClaimStatus? status = null)
        {
            return Ok(await _service.GetAllAsync(search, status));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleInsuranceClaimDto>> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleInsuranceClaimDto>> Create([FromBody] CreateVehicleInsuranceClaimRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleInsuranceClaimDto>> Update(long id, [FromBody] UpdateVehicleInsuranceClaimRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id:long}")]
        public async Task<ActionResult> Delete(long id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
