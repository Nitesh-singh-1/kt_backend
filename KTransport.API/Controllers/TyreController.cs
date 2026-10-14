using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.Authorization;
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
    public class TyreController : ControllerBase
    {
        private readonly ITyreService _service;

        public TyreController(ITyreService service)
        {
            _service = service;
        }

        [HttpGet]
        [RequirePermission("master_data.tyres.view")]
        public async Task<ActionResult<List<TyreDto>>> GetAll(
            [FromQuery] string? search = null,
            [FromQuery] TyreStatus? status = null,
            [FromQuery] long? vehicleId = null)
        {
            return Ok(await _service.GetAllAsync(search, status, vehicleId));
        }

        [HttpGet("{id:long}")]
        [RequirePermission("master_data.tyres.view")]
        public async Task<ActionResult<TyreDto>> GetById(long id)
        {
            var result = await _service.GetByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("master_data.tyres.create")]
        public async Task<ActionResult<TyreDto>> Create([FromBody] CreateTyreRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("master_data.tyres.edit")]
        public async Task<ActionResult<TyreDto>> Update(long id, [FromBody] UpdateTyreRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _service.UpdateAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("master_data.tyres.delete")]
        public async Task<ActionResult> Delete(long id)
        {
            var success = await _service.DeleteAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
