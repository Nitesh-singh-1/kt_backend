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
    public class ClaimController : ControllerBase
    {
        private readonly ICargoClaimService _claimService;

        public ClaimController(ICargoClaimService claimService)
        {
            _claimService = claimService;
        }

        [HttpGet]
        public async Task<ActionResult<List<CargoClaimDto>>> GetClaims(
            [FromQuery] ClaimStatus? status = null,
            [FromQuery] string? search = null)
        {
            var claims = await _claimService.GetClaimsAsync(status, search);
            return Ok(claims);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<CargoClaimDto>> GetClaimById(long id)
        {
            var claim = await _claimService.GetClaimByIdAsync(id);
            if (claim == null) return NotFound();
            return Ok(claim);
        }

        [HttpPost]
        public async Task<ActionResult<CargoClaimDto>> CreateClaim([FromBody] CreateClaimRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var claim = await _claimService.CreateClaimAsync(request);
            return CreatedAtAction(nameof(GetClaimById), new { id = claim.Id }, claim);
        }

        [HttpPost("{id:long}/settle")]
        public async Task<ActionResult<CargoClaimDto>> SettleClaim(long id, [FromBody] SettleClaimRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var claim = await _claimService.SettleClaimAsync(id, request);
            if (claim == null) return NotFound();
            return Ok(claim);
        }

        [HttpDelete("{id:long}")]
        public async Task<ActionResult> DeleteClaim(long id)
        {
            var success = await _claimService.DeleteClaimAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
