using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KTransport.API.Models;
using KTransport.API.Services;
using System.Security.Claims;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GstBillController : ControllerBase
    {
        private readonly IGstBillService _gstBillService;
        private readonly ILogger<GstBillController> _logger;

        public GstBillController(IGstBillService gstBillService, ILogger<GstBillController> logger)
        {
            _gstBillService = gstBillService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<GstBillResponse>> CreateGstBill([FromBody] CreateGstBillRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var response = await _gstBillService.CreateGstBillAsync(request, userId);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<GstBillResponse>> UpdateGstBill(int id, [FromBody] UpdateGstBillRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var response = await _gstBillService.UpdateGstBillAsync(id, request, userId);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GstBillResponse>> GetGstBillById(int id)
        {
            var response = await _gstBillService.GetGstBillByIdAsync(id);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpGet("grno/{grNo}")]
        public async Task<ActionResult<GstBillResponse>> GetGstBillByGrNo(string grNo)
        {
            var response = await _gstBillService.GetGstBillByGrNoAsync(grNo);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpGet]
        public async Task<ActionResult<GstBillListResponse>> GetAllGstBills([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _gstBillService.GetAllGstBillsAsync(page, pageSize);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<GstBillResponse>> DeleteGstBill(int id)
        {
            var userId = GetCurrentUserId();
            var response = await _gstBillService.DeleteGstBillAsync(id, userId);

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