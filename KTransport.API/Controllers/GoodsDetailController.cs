using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KTransport.API.Models;
using KTransport.API.Services;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GoodsDetailController : ControllerBase
    {
        private readonly IGoodsDetailService _goodsDetailService;
        private readonly ILogger<GoodsDetailController> _logger;

        public GoodsDetailController(IGoodsDetailService goodsDetailService, ILogger<GoodsDetailController> logger)
        {
            _goodsDetailService = goodsDetailService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<GoodsDetailResponse>> CreateGoodsDetail([FromBody] CreateGoodsDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _goodsDetailService.CreateGoodsDetailAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<GoodsDetailResponse>> UpdateGoodsDetail(int id, [FromBody] UpdateGoodsDetailRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _goodsDetailService.UpdateGoodsDetailAsync(id, request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<GoodsDetailResponse>> GetGoodsDetailById(int id)
        {
            var response = await _goodsDetailService.GetGoodsDetailByIdAsync(id);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpGet("bill/{billId}")]
        public async Task<ActionResult<GoodsDetailListResponse>> GetGoodsDetailsByBillId(int billId)
        {
            var response = await _goodsDetailService.GetGoodsDetailsByBillIdAsync(billId);

            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult<GoodsDetailResponse>> DeleteGoodsDetail(int id)
        {
            var response = await _goodsDetailService.DeleteGoodsDetailAsync(id);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }
    }
}