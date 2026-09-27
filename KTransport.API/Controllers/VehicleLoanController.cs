using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VehicleLoanController : ControllerBase
    {
        private readonly IVehicleLoanService _service;

        public VehicleLoanController(IVehicleLoanService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<VehicleLoanDto>>> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] bool activeOnly = false)
        {
            return Ok(await _service.GetAllAsync(search, activeOnly));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VehicleLoanDto>> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<VehicleLoanDto>> Create([FromBody] CreateVehicleLoanRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<VehicleLoanDto>> Update(long id, [FromBody] UpdateVehicleLoanRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("{id:long}/pay-emi")]
        public async Task<ActionResult<VehicleLoanDto>> PayEmi(long id)
        {
            var result = await _service.RecordEmiPaidAsync(id);
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
