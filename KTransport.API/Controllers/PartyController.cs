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
    public class PartyController : ControllerBase
    {
        private readonly IPartyService _partyService;

        public PartyController(IPartyService partyService)
        {
            _partyService = partyService;
        }

        [HttpGet]
        public async Task<ActionResult<List<PartyDto>>> GetParties(
            [FromQuery] string? search = null,
            [FromQuery] PartyType? partyType = null,
            [FromQuery] bool activeOnly = true)
        {
            var parties = await _partyService.GetPartiesAsync(search, partyType, activeOnly);
            return Ok(parties);
        }

        [HttpGet("lookup")]
        public async Task<ActionResult<List<PartyLookupDto>>> GetPartyLookup(
            [FromQuery] string? q = null,
            [FromQuery] PartyType? partyType = null)
        {
            var results = await _partyService.GetPartyLookupAsync(q, partyType);
            return Ok(results);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<PartyDto>> GetPartyById(long id)
        {
            var party = await _partyService.GetPartyByIdAsync(id);
            if (party == null) return NotFound(new { message = $"Party with ID {id} not found." });
            return Ok(party);
        }

        [HttpPost]
        public async Task<ActionResult<PartyDto>> CreateParty([FromBody] CreatePartyRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var party = await _partyService.CreatePartyAsync(request);
            return CreatedAtAction(nameof(GetPartyById), new { id = party.Id }, party);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<PartyDto>> UpdateParty(long id, [FromBody] UpdatePartyRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var updated = await _partyService.UpdatePartyAsync(id, request);
            if (updated == null) return NotFound(new { message = $"Party with ID {id} not found." });
            return Ok(updated);
        }

        [HttpDelete("{id:long}")]
        public async Task<ActionResult> DeleteParty(long id)
        {
            var success = await _partyService.DeletePartyAsync(id);
            if (!success) return NotFound(new { message = $"Party with ID {id} not found." });
            return NoContent();
        }
    }
}
