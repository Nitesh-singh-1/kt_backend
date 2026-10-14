using System;
using System.Collections.Generic;
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
    [RequireFeature(FeatureConstants.BILLING)]
    public class MoneyReceiptController : ControllerBase
    {
        private readonly IMoneyReceiptService _receiptService;

        public MoneyReceiptController(IMoneyReceiptService receiptService)
        {
            _receiptService = receiptService;
        }

        [HttpGet]
        [RequirePermission("billing.receipts.view")]
        public async Task<ActionResult<List<MoneyReceiptDto>>> GetReceipts(
            [FromQuery] DateOnly? startDate = null,
            [FromQuery] DateOnly? endDate = null,
            [FromQuery] string? search = null,
            [FromQuery] string? paymentMode = null,
            [FromQuery] string? status = null)
        {
            var filter = new MoneyReceiptFilterRequest
            {
                StartDate = startDate,
                EndDate = endDate,
                Search = search,
                PaymentMode = paymentMode,
                Status = status
            };

            var receipts = await _receiptService.GetReceiptsAsync(filter);
            return Ok(receipts);
        }

        [HttpGet("{id:long}")]
        [RequirePermission("billing.receipts.view")]
        public async Task<ActionResult<MoneyReceiptDto>> GetReceiptById(long id)
        {
            var receipt = await _receiptService.GetReceiptByIdAsync(id);
            if (receipt == null)
            {
                return NotFound(new { message = $"Money Receipt with ID {id} not found." });
            }
            return Ok(receipt);
        }

        [HttpGet("shipment/{shipmentId:long}")]
        [RequirePermission("billing.receipts.view")]
        public async Task<ActionResult<MoneyReceiptDto>> GetReceiptByShipmentId(long shipmentId)
        {
            var receipt = await _receiptService.GetReceiptByShipmentIdAsync(shipmentId);
            if (receipt == null)
            {
                return NotFound(new { message = $"Money Receipt for Bilty {shipmentId} not found or Bilty is not Paid." });
            }
            return Ok(receipt);
        }

        [HttpPost("collect/{shipmentId:long}")]
        [RequirePermission("billing.receipts.create")]
        public async Task<ActionResult<MoneyReceiptDto>> CollectPayment(long shipmentId, [FromBody] CollectMoneyReceiptRequest request)
        {
            var receipt = await _receiptService.CollectPaymentAsync(shipmentId, request);
            if (receipt == null)
            {
                return NotFound(new { message = $"Paid Bilty with ID {shipmentId} not found." });
            }
            return Ok(receipt);
        }
    }
}
