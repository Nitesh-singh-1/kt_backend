using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
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
    [RequireFeature(FeatureConstants.QUOTATION)]
    public class QuotationController : ControllerBase
    {
        private readonly IQuotationService _quotationService;

        public QuotationController(IQuotationService quotationService)
        {
            _quotationService = quotationService;
        }

        [HttpGet]
        [RequirePermission("quotations.view")]
        public async Task<ActionResult<List<QuotationDto>>> GetQuotations(
            [FromQuery] string? search = null,
            [FromQuery] QuotationStatus? status = null)
        {
            var result = await _quotationService.GetQuotationsAsync(search, status);
            return Ok(result);
        }

        [HttpGet("{id:long}")]
        [RequirePermission("quotations.view")]
        public async Task<ActionResult<QuotationDto>> GetQuotationById(long id)
        {
            var result = await _quotationService.GetQuotationByIdAsync(id);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPost]
        [RequirePermission("quotations.create")]
        public async Task<ActionResult<QuotationDto>> CreateQuotation([FromBody] CreateQuotationRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _quotationService.CreateQuotationAsync(request);
            return CreatedAtAction(nameof(GetQuotationById), new { id = result.Id }, result);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("quotations.edit")]
        public async Task<ActionResult<QuotationDto>> UpdateQuotation(long id, [FromBody] UpdateQuotationRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _quotationService.UpdateQuotationAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpPut("{id:long}/status")]
        [RequirePermission("quotations.edit", "quotations.approve")]
        public async Task<ActionResult<QuotationDto>> UpdateStatus(long id, [FromBody] UpdateQuotationStatusRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _quotationService.UpdateStatusAsync(id, request);
            if (result == null) return NotFound();
            return Ok(result);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("quotations.delete")]
        public async Task<ActionResult> DeleteQuotation(long id)
        {
            var success = await _quotationService.DeleteQuotationAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }
    }
}
