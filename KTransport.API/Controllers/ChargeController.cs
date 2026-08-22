using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KTransport.API.Services;
using KTransport.API.DTOs;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChargeController : ControllerBase
    {
        private readonly IChargeService _chargeService;
        private readonly ILogger<ChargeController> _logger;

        public ChargeController(IChargeService chargeService, ILogger<ChargeController> logger)
        {
            _chargeService = chargeService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ChargeResponse>> CreateCharge([FromBody] CreateChargeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _chargeService.CreateChargeAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ChargeResponse>> UpdateCharge(int id, [FromBody] UpdateChargeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _chargeService.UpdateChargeAsync(id, request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ChargeResponse>> GetChargeById(int id)
        {
            var response = await _chargeService.GetChargeByIdAsync(id);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpGet("bill/{billId}")]
        public async Task<ActionResult<ChargeResponse>> GetChargeByBillId(int billId)
        {
            var response = await _chargeService.GetChargeByBillIdAsync(billId);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ChargeResponse>> DeleteCharge(int id)
        {
            var response = await _chargeService.DeleteChargeAsync(id);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
}