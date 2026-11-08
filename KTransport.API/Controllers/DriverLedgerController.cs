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
    public class DriverLedgerController : ControllerBase
    {
        private readonly IDriverLedgerService _service;

        public DriverLedgerController(IDriverLedgerService service)
        {
            _service = service;
        }

        [HttpGet]
        [RequirePermission("master_data.driver_ledger.view")]
        public async Task<ActionResult<List<DriverLedgerEntryDto>>> GetEntries(
            [FromQuery] string? search = null,
            [FromQuery] string? driverName = null)
        {
            return Ok(await _service.GetEntriesAsync(search, driverName));
        }

        [HttpGet("outstanding")]
        [RequirePermission("master_data.driver_ledger.view")]
        public async Task<ActionResult<List<DriverOutstandingDto>>> GetOutstanding()
        {
            return Ok(await _service.GetOutstandingAsync());
        }

        [HttpPost]
        [RequirePermission("master_data.driver_ledger.create")]
        public async Task<ActionResult<DriverLedgerEntryDto>> Create([FromBody] CreateDriverLedgerEntryRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return Ok(result);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("master_data.driver_ledger.edit")]
        public async Task<ActionResult<DriverLedgerEntryDto>> Update(long id, [FromBody] UpdateDriverLedgerEntryRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("master_data.driver_ledger.edit")]
        public async Task<ActionResult> Delete(long id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
