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
    public class EmptyTripLogController : ControllerBase
    {
        private readonly IEmptyTripLogService _service;

        public EmptyTripLogController(IEmptyTripLogService service)
        {
            _service = service;
        }

        [HttpGet]
        [RequirePermission("empty_trips.view")]
        public async Task<ActionResult<List<EmptyTripLogDto>>> GetAll([FromQuery] string? search = null)
        {
            return Ok(await _service.GetAllAsync(search));
        }

        [HttpGet("{id:long}")]
        [RequirePermission("empty_trips.view")]
        public async Task<ActionResult<EmptyTripLogDto>> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("empty_trips.create")]
        public async Task<ActionResult<EmptyTripLogDto>> Create([FromBody] CreateEmptyTripLogRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("empty_trips.edit")]
        public async Task<ActionResult<EmptyTripLogDto>> Update(long id, [FromBody] UpdateEmptyTripLogRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("empty_trips.delete")]
        public async Task<ActionResult> Delete(long id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
