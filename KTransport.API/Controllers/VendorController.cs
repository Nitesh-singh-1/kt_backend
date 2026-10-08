using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.DTOs;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireFeature(FeatureConstants.VENDOR)]
    public class VendorController : ControllerBase
    {
        private readonly IVendorService _vendorService;

        public VendorController(IVendorService vendorService)
        {
            _vendorService = vendorService;
        }

        // Vendor Master
        [HttpGet]
        [RequirePermission("vendors.view")]
        public async Task<ActionResult<List<VendorDto>>> GetVendors(
            [FromQuery] string? search = null,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var vendors = await _vendorService.GetVendorsAsync(search);
            var slice = PaginationHelper.Paginate(vendors, HttpContext, page, pageSize);
            return Ok(slice);
        }

        [HttpGet("lookup")]
        [RequirePermission("vendors.view")]
        public async Task<ActionResult<List<VendorLookupDto>>> GetVendorLookup([FromQuery] string? q = null)
        {
            var results = await _vendorService.GetVendorLookupAsync(q);
            return Ok(results);
        }

        [HttpGet("{id:long}")]
        [RequirePermission("vendors.view")]
        public async Task<ActionResult<VendorDto>> GetVendorById(long id)
        {
            var vendor = await _vendorService.GetVendorByIdAsync(id);
            if (vendor == null) return NotFound(new { message = $"Vendor with ID {id} not found." });
            return Ok(vendor);
        }

        [HttpPost]
        [RequirePermission("vendors.create")]
        public async Task<ActionResult<VendorDto>> CreateVendor([FromBody] CreateVendorRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var vendor = await _vendorService.CreateVendorAsync(request);
            return CreatedAtAction(nameof(GetVendorById), new { id = vendor.Id }, vendor);
        }

        [HttpPut("{id:long}")]
        [RequirePermission("vendors.edit")]
        public async Task<ActionResult<VendorDto>> UpdateVendor(long id, [FromBody] CreateVendorRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var updated = await _vendorService.UpdateVendorAsync(id, request);
            if (updated == null) return NotFound(new { message = $"Vendor with ID {id} not found." });
            return Ok(updated);
        }

        [HttpDelete("{id:long}")]
        [RequirePermission("vendors.delete")]
        public async Task<ActionResult> DeleteVendor(long id)
        {
            var success = await _vendorService.DeleteVendorAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        // Lorry Hire Contracts
        [HttpGet("lorry-hire")]
        [RequirePermission("vendors.view")]
        public async Task<ActionResult<List<LorryHireContractDto>>> GetLorryHireContracts(
            [FromQuery] string? search = null,
            [FromQuery] long? vendorId = null)
        {
            var contracts = await _vendorService.GetLorryHireContractsAsync(search, vendorId);
            return Ok(contracts);
        }

        [HttpGet("lorry-hire/{id:long}")]
        [RequirePermission("vendors.view")]
        public async Task<ActionResult<LorryHireContractDto>> GetLorryHireById(long id)
        {
            var contract = await _vendorService.GetLorryHireContractByIdAsync(id);
            if (contract == null) return NotFound();
            return Ok(contract);
        }

        [HttpPost("lorry-hire")]
        [RequirePermission("vendors.create")]
        public async Task<ActionResult<LorryHireContractDto>> CreateLorryHire([FromBody] CreateLorryHireRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var contract = await _vendorService.CreateLorryHireContractAsync(request, userId);
            return CreatedAtAction(nameof(GetLorryHireById), new { id = contract.Id }, contract);
        }

        [HttpPost("lorry-hire/{id:long}/payments")]
        [RequirePermission("vendors.edit")]
        public async Task<ActionResult<LorryHireContractDto>> RecordLorryHirePayment(
            long id,
            [FromBody] RecordLorryHirePaymentRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int? userId = GetCurrentUserId();
            var updated = await _vendorService.RecordLorryHirePaymentAsync(id, request, userId);
            if (updated == null) return NotFound();
            return Ok(updated);
        }

        [HttpDelete("lorry-hire/{id:long}")]
        [RequirePermission("vendors.delete")]
        public async Task<ActionResult> DeleteLorryHire(long id)
        {
            var success = await _vendorService.DeleteLorryHireContractAsync(id);
            if (!success) return NotFound();
            return NoContent();
        }

        private int? GetCurrentUserId()
        {
            var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(val, out int id) ? id : null;
        }
    }
}
