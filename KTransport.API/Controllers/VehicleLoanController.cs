using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.Authorization;
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
        [RequirePermission("master_data.loans.view")]
        public async Task<ActionResult<List<VehicleLoanDto>>> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] bool activeOnly = false)
        {
            return Ok(await _service.GetAllAsync(search, activeOnly));
        }

        [HttpGet("{id:long}")]
        [RequirePermission("master_data.loans.view")]
        public async Task<ActionResult<VehicleLoanDto>> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("master_data.loans.create")]
        public async Task<ActionResult<VehicleLoanDto>> Create([FromBody] CreateVehicleLoanRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("master_data.loans.edit")]
        public async Task<ActionResult<VehicleLoanDto>> Update(long id, [FromBody] UpdateVehicleLoanRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("{id:long}/pay-emi")]
        [RequirePermission("master_data.loans.edit")]
        public async Task<ActionResult<VehicleLoanDto>> PayEmi(long id)
        {
            var result = await _service.RecordEmiPaidAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("master_data.loans.delete")]
        public async Task<ActionResult> Delete(long id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
