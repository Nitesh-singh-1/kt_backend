using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class InvoiceService : IInvoiceService
    {
        private readonly KTransportDbContext _context;
        private readonly INumberingSequenceService _numberingService;
        private readonly ILogger<InvoiceService> _logger;

        public InvoiceService(
            KTransportDbContext context,
            INumberingSequenceService numberingService,
            ILogger<InvoiceService> logger)
        {
            _context = context;
            _numberingService = numberingService;
            _logger = logger;
        }

        public async Task<List<InvoiceDto>> GetInvoicesAsync(string? search = null, InvoicePaymentStatus? paymentStatus = null, long? partyId = null)
        {
            var query = _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Shipments)
                .AsNoTracking()
                .Where(i => i.IsActive);

            if (paymentStatus.HasValue)
            {
                query = query.Where(i => i.PaymentStatus == paymentStatus.Value);
            }

            if (partyId.HasValue)
            {
                query = query.Where(i => i.PartyId == partyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(i =>
                    i.InvoiceNo.ToLower().Contains(s) ||
                    (i.PartyName != null && i.PartyName.ToLower().Contains(s)) ||
                    (i.PartyGstNo != null && i.PartyGstNo.ToLower().Contains(s))
                );
            }

            var invoices = await query.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).ToListAsync();
            return invoices.Select(MapToDto).ToList();
        }

        public async Task<List<InvoiceLookupDto>> GetInvoiceLookupAsync(string? query = null, long? partyId = null)
        {
            var dbQuery = _context.Invoices.AsNoTracking().Where(i => i.IsActive);

            if (partyId.HasValue)
            {
                dbQuery = dbQuery.Where(i => i.PartyId == partyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim().ToLower();
                dbQuery = dbQuery.Where(i =>
                    i.InvoiceNo.ToLower().Contains(s) ||
                    (i.PartyName != null && i.PartyName.ToLower().Contains(s))
                );
            }

            return await dbQuery
                .OrderByDescending(i => i.InvoiceDate)
                .Take(50)
                .Select(i => new InvoiceLookupDto
                {
                    Id = i.Id,
                    InvoiceNo = i.InvoiceNo,
                    InvoiceDate = i.InvoiceDate,
                    PartyId = i.PartyId,
                    PartyName = i.PartyName,
                    GrandTotal = i.GrandTotal,
                    DueAmount = i.DueAmount,
                    PaymentStatus = i.PaymentStatus
                })
                .ToListAsync();
        }

        public async Task<InvoiceDto?> GetInvoiceByIdAsync(long id)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Shipments)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);

            return invoice == null ? null : MapToDto(invoice);
        }

        public async Task<InvoiceDto> CreateInvoiceAsync(CreateInvoiceRequest request, int? userId = null)
        {
            // Auto-generate invoice number if not provided
            string invoiceNo = request.InvoiceNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(invoiceNo))
            {
                invoiceNo = await _numberingService.GetNextNumberAsync("INVOICE", "INV");
            }

            // Resolve party details if partyId provided
            string? partyName = request.PartyName;
            string? partyGstNo = request.PartyGstNo;
            string? partyAddress = request.PartyAddress;

            if (request.PartyId.HasValue)
            {
                var party = await _context.Parties.FindAsync(request.PartyId.Value);
                if (party != null)
                {
                    partyName ??= party.Name;
                    partyGstNo ??= party.GstNo;
                    partyAddress ??= party.Address;
                }
            }

            // Calculate line items
            decimal subTotal = 0;
            var invoiceItems = new List<InvoiceItem>();

            foreach (var itemReq in request.Items)
            {
                var lineAmount = itemReq.Quantity * itemReq.Rate;
                var taxAmt = itemReq.TaxAmount > 0 ? itemReq.TaxAmount : (lineAmount * itemReq.TaxRate / 100m);
                var total = lineAmount + taxAmt;

                subTotal += lineAmount;

                invoiceItems.Add(new InvoiceItem
                {
                    ShipmentId = itemReq.ShipmentId,
                    ShipmentNo = itemReq.ShipmentNo,
                    Description = itemReq.Description,
                    Quantity = itemReq.Quantity,
                    Rate = itemReq.Rate,
                    Amount = lineAmount,
                    TaxRate = itemReq.TaxRate,
                    TaxAmount = taxAmt,
                    TotalAmount = total,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Overall invoice calculations
            decimal overallTaxAmount = invoiceItems.Sum(x => x.TaxAmount);
            if (request.TaxRate > 0 && overallTaxAmount == 0)
            {
                overallTaxAmount = subTotal * request.TaxRate / 100m;
            }

            decimal grandTotal = subTotal + overallTaxAmount + request.OtherCharges - request.Discount;
            if (grandTotal < 0) grandTotal = 0;

            decimal paidAmount = request.PaidAmount;
            decimal dueAmount = grandTotal - paidAmount;
            if (dueAmount < 0) dueAmount = 0;

            var paymentStatus = dueAmount == 0 && grandTotal > 0
                ? InvoicePaymentStatus.Paid
                : (paidAmount > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Unpaid);

            var invoice = new Invoice
            {
                InvoiceNo = invoiceNo,
                InvoiceDate = request.InvoiceDate,
                DueDate = request.DueDate,
                PartyId = request.PartyId,
                PartyName = partyName,
                PartyGstNo = partyGstNo,
                PartyAddress = partyAddress,
                SubTotal = subTotal,
                TaxRate = request.TaxRate,
                TaxAmount = overallTaxAmount,
                Discount = request.Discount,
                OtherCharges = request.OtherCharges,
                GrandTotal = grandTotal,
                PaidAmount = paidAmount,
                DueAmount = dueAmount,
                PaymentStatus = paymentStatus,
                PaymentMode = request.PaymentMode,
                Remarks = request.Remarks,
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                Items = invoiceItems
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // Link shipments if specified
            if (request.ShipmentIdsToLink != null && request.ShipmentIdsToLink.Any())
            {
                var shipments = await _context.Shipments
                    .Where(s => request.ShipmentIdsToLink.Contains(s.Id))
                    .ToListAsync();

                foreach (var s in shipments)
                {
                    s.InvoiceId = invoice.Id;
                    s.InvoiceNo = invoice.InvoiceNo;
                    s.InvoiceDate = invoice.InvoiceDate;
                }
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Created invoice {InvoiceNo} (ID: {Id})", invoice.InvoiceNo, invoice.Id);
            return MapToDto(invoice);
        }

        public async Task<InvoiceDto?> UpdateInvoiceAsync(long id, UpdateInvoiceRequest request, int? userId = null)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Shipments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return null;

            // Update header info
            invoice.InvoiceDate = request.InvoiceDate;
            invoice.DueDate = request.DueDate;
            invoice.PartyId = request.PartyId;
            invoice.PartyName = request.PartyName;
            invoice.PartyGstNo = request.PartyGstNo;
            invoice.PartyAddress = request.PartyAddress;
            invoice.Remarks = request.Remarks;
            invoice.Discount = request.Discount;
            invoice.OtherCharges = request.OtherCharges;
            invoice.TaxRate = request.TaxRate;
            invoice.UpdatedBy = userId;
            invoice.UpdatedAt = DateTime.UtcNow;

            // Rebuild items if provided
            if (request.Items != null && request.Items.Any())
            {
                _context.InvoiceItems.RemoveRange(invoice.Items);
                invoice.Items.Clear();

                decimal subTotal = 0;
                foreach (var itemReq in request.Items)
                {
                    var lineAmount = itemReq.Quantity * itemReq.Rate;
                    var taxAmt = itemReq.TaxAmount > 0 ? itemReq.TaxAmount : (lineAmount * itemReq.TaxRate / 100m);
                    var total = lineAmount + taxAmt;

                    subTotal += lineAmount;

                    invoice.Items.Add(new InvoiceItem
                    {
                        InvoiceId = invoice.Id,
                        ShipmentId = itemReq.ShipmentId,
                        ShipmentNo = itemReq.ShipmentNo,
                        Description = itemReq.Description,
                        Quantity = itemReq.Quantity,
                        Rate = itemReq.Rate,
                        Amount = lineAmount,
                        TaxRate = itemReq.TaxRate,
                        TaxAmount = taxAmt,
                        TotalAmount = total,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                invoice.SubTotal = subTotal;
                decimal overallTax = invoice.Items.Sum(x => x.TaxAmount);
                if (request.TaxRate > 0 && overallTax == 0)
                {
                    overallTax = subTotal * request.TaxRate / 100m;
                }
                invoice.TaxAmount = overallTax;
                invoice.GrandTotal = Math.Max(0, subTotal + overallTax + invoice.OtherCharges - invoice.Discount);
                invoice.DueAmount = Math.Max(0, invoice.GrandTotal - invoice.PaidAmount);

                if (invoice.DueAmount == 0 && invoice.GrandTotal > 0)
                {
                    invoice.PaymentStatus = InvoicePaymentStatus.Paid;
                }
                else if (invoice.PaidAmount > 0)
                {
                    invoice.PaymentStatus = InvoicePaymentStatus.PartiallyPaid;
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated invoice ID: {Id}", invoice.Id);
            return MapToDto(invoice);
        }

        public async Task<InvoiceDto?> RecordPaymentAsync(long id, RecordPaymentRequest request, int? userId = null)
        {
            var invoice = await _context.Invoices
                .Include(i => i.Items)
                .Include(i => i.Shipments)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (invoice == null) return null;

            invoice.PaidAmount += request.PaymentAmount;
            invoice.DueAmount = Math.Max(0, invoice.GrandTotal - invoice.PaidAmount);

            if (invoice.DueAmount == 0)
            {
                invoice.PaymentStatus = InvoicePaymentStatus.Paid;
            }
            else if (invoice.PaidAmount > 0)
            {
                invoice.PaymentStatus = InvoicePaymentStatus.PartiallyPaid;
            }

            if (!string.IsNullOrWhiteSpace(request.PaymentMode))
            {
                invoice.PaymentMode = request.PaymentMode;
            }

            invoice.UpdatedBy = userId;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Recorded payment of {Amount} against invoice {InvoiceNo}", request.PaymentAmount, invoice.InvoiceNo);
            return MapToDto(invoice);
        }

        public async Task<bool> VoidInvoiceAsync(long id, int? userId = null)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null) return false;

            invoice.PaymentStatus = InvoicePaymentStatus.Cancelled;
            invoice.IsActive = false;
            invoice.UpdatedBy = userId;
            invoice.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Voided invoice ID: {Id}", id);
            return true;
        }

        private static InvoiceDto MapToDto(Invoice i)
        {
            return new InvoiceDto
            {
                Id = i.Id,
                TenantId = i.TenantId,
                InvoiceNo = i.InvoiceNo,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate,
                PartyId = i.PartyId,
                PartyName = i.PartyName,
                PartyGstNo = i.PartyGstNo,
                PartyAddress = i.PartyAddress,
                SubTotal = i.SubTotal,
                TaxRate = i.TaxRate,
                TaxAmount = i.TaxAmount,
                Discount = i.Discount,
                OtherCharges = i.OtherCharges,
                GrandTotal = i.GrandTotal,
                PaidAmount = i.PaidAmount,
                DueAmount = i.DueAmount,
                PaymentStatus = i.PaymentStatus,
                PaymentMode = i.PaymentMode,
                Remarks = i.Remarks,
                IsActive = i.IsActive,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt,
                Items = i.Items.Select(item => new InvoiceItemDto
                {
                    Id = item.Id,
                    InvoiceId = item.InvoiceId,
                    ShipmentId = item.ShipmentId,
                    ShipmentNo = item.ShipmentNo,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    Rate = item.Rate,
                    Amount = item.Amount,
                    TaxRate = item.TaxRate,
                    TaxAmount = item.TaxAmount,
                    TotalAmount = item.TotalAmount
                }).ToList(),
                LinkedShipmentNos = i.Shipments.Select(s => s.ShipmentNo).ToList()
            };
        }
    }
}
