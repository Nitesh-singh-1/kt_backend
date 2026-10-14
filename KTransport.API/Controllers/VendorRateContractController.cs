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
    public class VendorRateContractController : ControllerBase
    {
        private readonly IVendorRateContractService _service;

        public VendorRateContractController(IVendorRateContractService service)
        {
            _service = service;
        }

        [HttpGet]
        [RequirePermission("master_data.vendor_rates.view")]
        public async Task<ActionResult<List<VendorRateContractDto>>> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] long? vendorId = null)
        {
            return Ok(await _service.GetAllAsync(search, vendorId));
        }

        [HttpGet("{id:long}")]
        [RequirePermission("master_data.vendor_rates.view")]
        public async Task<ActionResult<VendorRateContractDto>> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("master_data.vendor_rates.create")]
        public async Task<ActionResult<VendorRateContractDto>> Create([FromBody] CreateVendorRateContractRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("master_data.vendor_rates.edit")]
        public async Task<ActionResult<VendorRateContractDto>> Update(long id, [FromBody] UpdateVendorRateContractRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("master_data.vendor_rates.delete")]
        public async Task<ActionResult> Delete(long id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
