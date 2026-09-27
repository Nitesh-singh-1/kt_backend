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
    public class SparePartController : ControllerBase
    {
        private readonly ISparePartService _service;

        public SparePartController(ISparePartService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<List<SparePartDto>>> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] string? category = null,
            [FromQuery] bool lowStockOnly = false)
        {
            return Ok(await _service.GetAllAsync(search, category, lowStockOnly));
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<SparePartDto>> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<SparePartDto>> Create([FromBody] CreateSparePartRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<SparePartDto>> Update(long id, [FromBody] UpdateSparePartRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost("{id:long}/adjust-stock")]
        public async Task<ActionResult<SparePartDto>> AdjustStock(long id, [FromBody] SparePartStockMovementRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.AdjustStockAsync(id, request);
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
