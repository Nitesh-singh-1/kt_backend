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
    public class RateCardController : ControllerBase
    {
        private readonly IRateCardService _rateCardService;

        public RateCardController(IRateCardService rateCardService)
        {
            _rateCardService = rateCardService;
        }

        [HttpGet]
        public async Task<ActionResult<List<FreightRateCardDto>>> GetRateCards(
            [FromQuery] string? search = null,
            [FromQuery] long? partyId = null)
        {
            var cards = await _rateCardService.GetRateCardsAsync(search, partyId);
            return Ok(cards);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<FreightRateCardDto>> GetRateCardById(long id)
        {
            var card = await _rateCardService.GetRateCardByIdAsync(id);
            if (card == null) return NotFound();
            return Ok(card);
        }

        [HttpPost]
        public async Task<ActionResult<FreightRateCardDto>> CreateRateCard([FromBody] CreateRateCardRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var card = await _rateCardService.CreateRateCardAsync(request);
            return CreatedAtAction(nameof(GetRateCardById), new { id = card.Id }, card);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<FreightRateCardDto>> UpdateRateCard(long id, [FromBody] CreateRateCardRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var updated = await _rateCardService.UpdateRateCardAsync(id, request);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("{id:long}")]
        public async Task<ActionResult> DeleteRateCard(long id)
        {
            var success = await _rateCardService.DeleteRateCardAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        [HttpPost("calculate")]
        public async Task<ActionResult<CalculatedFreightResponse>> CalculateFreight([FromBody] CalculateFreightRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var result = await _rateCardService.CalculateFreightAsync(request);
            return Ok(result);
        }
    }
}
