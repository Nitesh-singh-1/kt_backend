using System.Collections.Generic;
using System.Security.Claims;
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
    [RequireFeature(FeatureConstants.BILLING)]
    public class InvoiceController : ControllerBase
    {
        private readonly IInvoiceService _invoiceService;

        public InvoiceController(IInvoiceService invoiceService)
        {
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<ActionResult<List<InvoiceDto>>> GetInvoices(
            [FromQuery] string? search = null,
            [FromQuery] InvoicePaymentStatus? paymentStatus = null,
            [FromQuery] long? partyId = null)
        {
            var invoices = await _invoiceService.GetInvoicesAsync(search, paymentStatus, partyId);
            return Ok(invoices);
        }

        [HttpGet("lookup")]
        public async Task<ActionResult<List<InvoiceLookupDto>>> GetInvoiceLookup(
            [FromQuery] string? q = null,
            [FromQuery] long? partyId = null)
        {
            var results = await _invoiceService.GetInvoiceLookupAsync(q, partyId);
            return Ok(results);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<InvoiceDto>> GetInvoiceById(long id)
        {
            var invoice = await _invoiceService.GetInvoiceByIdAsync(id);
            if (invoice == null) return NotFound(new { message = $"Invoice with ID {id} not found." });
            return Ok(invoice);
        }

        [HttpPost]
        public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var invoice = await _invoiceService.CreateInvoiceAsync(request, userId);
            return CreatedAtAction(nameof(GetInvoiceById), new { id = invoice.Id }, invoice);
        }

        [HttpPut("{id:long}")]
        public async Task<ActionResult<InvoiceDto>> UpdateInvoice(long id, [FromBody] UpdateInvoiceRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var updated = await _invoiceService.UpdateInvoiceAsync(id, request, userId);
            if (updated == null) return NotFound(new { message = $"Invoice with ID {id} not found." });
            return Ok(updated);
        }

        [HttpPost("{id:long}/payments")]
        public async Task<ActionResult<InvoiceDto>> RecordPayment(long id, [FromBody] RecordPaymentRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var updated = await _invoiceService.RecordPaymentAsync(id, request, userId);
            if (updated == null) return NotFound(new { message = $"Invoice with ID {id} not found." });
            return Ok(updated);
        }

        [HttpDelete("{id:long}")]
        public async Task<ActionResult> VoidInvoice(long id)
        {
            int? userId = GetCurrentUserId();
            var success = await _invoiceService.VoidInvoiceAsync(id, userId);
            if (!success) return NotFound(new { message = $"Invoice with ID {id} not found." });
            return NoContent();
        }

        private int? GetCurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out int id) ? id : null;
        }
    }
}
