using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KTransport.API.Services;
using System.Security.Claims;
using KTransport.API.DTOs;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChallanController : ControllerBase
    {
        private readonly IChallanService _challanService;
        private readonly ILogger<ChallanController> _logger;

        public ChallanController(IChallanService challanService, ILogger<ChallanController> logger)
        {
            _challanService = challanService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<ChallanResponse>> CreateChallan([FromBody] CreateChallanRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var response = await _challanService.CreateChallanAsync(request, userId);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ChallanResponse>> UpdateChallan(long id, [FromBody] UpdateChallanRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var response = await _challanService.UpdateChallanAsync(id, request, userId);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ChallanResponse>> GetChallanById(long id)
        {
            var response = await _challanService.GetChallanByIdAsync(id);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpGet]
        public async Task<ActionResult<ChallanListResponse>> GetAllChallans([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _challanService.GetAllChallansAsync(page, pageSize);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet("search")]
        public async Task<ActionResult<ChallanListResponse>> SearchChallans(
            [FromQuery] string? searchTerm,
            [FromQuery] DateOnly? startDate,
            [FromQuery] DateOnly? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var response = await _challanService.SearchChallansAsync(searchTerm, startDate, endDate, page, pageSize);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<ChallanResponse>> DeleteChallan(long id)
        {
            var userId = GetCurrentUserId();
            var response = await _challanService.DeleteChallanAsync(id, userId);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}