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

        public async Task<List<UnbilledShipmentDto>> GetUnbilledShipmentsAsync(string? search = null)
        {
            var query = _context.Shipments
                .Include(s => s.Items)
                .Include(s => s.InvoiceReferences)
                .AsNoTracking()
                .Where(s => s.IsActive && s.InvoiceId == null
                    && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Draft);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var t = search.Trim().ToLower();
                query = query.Where(s =>
                    (s.ShipmentNo != null && s.ShipmentNo.ToLower().Contains(t)) ||
                    (s.ConsignorName != null && s.ConsignorName.ToLower().Contains(t)) ||
                    (s.ConsigneeName != null && s.ConsigneeName.ToLower().Contains(t)) ||
                    (s.FromLocation != null && s.FromLocation.ToLower().Contains(t)) ||
                    (s.ToLocation != null && s.ToLocation.ToLower().Contains(t)));
            }

            var list = await query.OrderBy(s => s.ConsignorName).ThenByDescending(s => s.ShipmentDate).ToListAsync();
            return list.Select(MapToUnbilledDto).ToList();
        }

        public async Task<List<PartyUnbilledSummaryDto>> GetUnbilledPartiesSummaryAsync(string? search = null)
        {
            var query = _context.Shipments.AsNoTracking()
                .Where(s => s.IsActive && s.InvoiceId == null
                    && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Draft);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var t = search.Trim().ToLower();
                query = query.Where(s =>
                    (s.ConsignorName != null && s.ConsignorName.ToLower().Contains(t)) ||
                    (s.ConsigneeName != null && s.ConsigneeName.ToLower().Contains(t)));
            }

            var shipments = await query.ToListAsync();

            var groups = shipments
                .GroupBy(s => (s.ConsignorName ?? "Unknown").Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g =>
                {
                    var first = g.First();
                    return new PartyUnbilledSummaryDto
                    {
                        PartyId = first.ConsignorPartyId,
                        PartyName = g.Key,
                        PartyGstNo = first.ConsignorGstNo,
                        PartyAddress = first.ConsignorAddress,
                        UnbilledCount = g.Count(),
                        TotalUnbilledAmount = g.Sum(x => x.GrandTotal > 0 ? x.GrandTotal : x.TotalFreight)
                    };
                })
                .OrderByDescending(p => p.TotalUnbilledAmount)
                .ToList();

            return groups;
        }

        public async Task<List<UnbilledShipmentDto>> GetUnbilledShipmentsByPartyAsync(string? partyName = null, long? partyId = null)
        {
            var query = _context.Shipments
                .Include(s => s.Items)
                .Include(s => s.InvoiceReferences)
                .AsNoTracking()
                .Where(s => s.IsActive && s.InvoiceId == null
                    && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Draft);

            if (partyId.HasValue && partyId.Value > 0)
            {
                query = query.Where(s => s.ConsignorPartyId == partyId.Value || s.ConsigneePartyId == partyId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(partyName))
            {
                var p = partyName.Trim().ToLower();
                query = query.Where(s =>
                    (s.ConsignorName != null && s.ConsignorName.ToLower() == p) ||
                    (s.ConsigneeName != null && s.ConsigneeName.ToLower() == p));
            }

            var list = await query.OrderByDescending(s => s.ShipmentDate).ThenByDescending(s => s.Id).ToListAsync();
            return list.Select(MapToUnbilledDto).ToList();
        }

        public async Task<BillBookInvoiceResponseDto> CreateBillBookInvoiceAsync(CreateBillBookRequestDto request, int? userId = null)
        {
            if (request.ShipmentIds == null || request.ShipmentIds.Count == 0)
            {
                return new BillBookInvoiceResponseDto
                {
                    Success = false,
                    Message = "Please select at least one Bilty / Consignment to generate the Freight Bill."
                };
            }

            // Retrieve eligible, still-unbilled consignments
            var shipments = await _context.Shipments
                .Include(s => s.Items)
                .Include(s => s.InvoiceReferences)
                .Where(s => request.ShipmentIds.Contains(s.Id) && s.IsActive && s.InvoiceId == null
                    && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Draft)
                .ToListAsync();

            if (shipments.Count == 0)
            {
                return new BillBookInvoiceResponseDto
                {
                    Success = false,
                    Message = "Selected consignments are already billed or no longer eligible."
                };
            }

            var invoiceNo = string.IsNullOrWhiteSpace(request.InvoiceNo)
                ? await _numberingService.GetNextNumberAsync("INVOICE", "INV")
                : request.InvoiceNo.Trim();

            // Build invoice items from selected bilties
            var invoiceItems = new List<InvoiceItem>();
            decimal subTotal = 0;

            foreach (var s in shipments)
            {
                var pkgCount = s.Items?.Sum(i => i.Quantity) ?? 1;
                var totalWeight = s.Items?.Sum(i => i.Weight) ?? 0;
                var biltyAmt = s.GrandTotal > 0 ? s.GrandTotal : s.TotalFreight;
                var rate = totalWeight > 0 ? Math.Round(biltyAmt / totalWeight, 2) : biltyAmt;

                var lineAmount = biltyAmt;
                var taxAmt = (lineAmount * request.TaxRate) / 100m;
                var total = lineAmount + taxAmt;
                subTotal += lineAmount;

                var desc = $"GR No: {s.ShipmentNo} | Route: {s.FromLocation} to {s.ToLocation} | Pkg: {pkgCount} | Wt: {totalWeight} Kg" +
                           (!string.IsNullOrWhiteSpace(s.Remarks) ? $" | {s.Remarks}" : "");

                invoiceItems.Add(new InvoiceItem
                {
                    ShipmentId = s.Id,
                    ShipmentNo = s.ShipmentNo,
                    Description = desc,
                    Quantity = pkgCount > 0 ? pkgCount : 1,
                    Rate = rate,
                    Amount = lineAmount,
                    TaxRate = request.TaxRate,
                    TaxAmount = taxAmt,
                    TotalAmount = total,
                    CreatedAt = DateTime.UtcNow
                });
            }

            decimal taxAmount = subTotal * request.TaxRate / 100m;
            decimal grandTotal = Math.Max(0, subTotal + taxAmount + request.OtherCharges - request.Discount);
            decimal paidAmount = request.PaidAmount;
            decimal dueAmount = Math.Max(0, grandTotal - paidAmount);

            var paymentStatus = dueAmount == 0 && grandTotal > 0
                ? InvoicePaymentStatus.Paid
                : (paidAmount > 0 ? InvoicePaymentStatus.PartiallyPaid : InvoicePaymentStatus.Unpaid);

            var firstParty = shipments.FirstOrDefault();
            var partyGst = !string.IsNullOrWhiteSpace(request.PartyGstNo) ? request.PartyGstNo : firstParty?.ConsignorGstNo;
            var partyAddr = !string.IsNullOrWhiteSpace(request.PartyAddress) ? request.PartyAddress : firstParty?.ConsignorAddress;

            var invoice = new Invoice
            {
                InvoiceNo = invoiceNo,
                InvoiceDate = request.InvoiceDate,
                DueDate = request.DueDate ?? request.InvoiceDate.AddDays(10), // Standard 10 days payment term
                PartyId = request.PartyId ?? firstParty?.ConsignorPartyId,
                PartyName = request.PartyName.Trim(),
                PartyGstNo = partyGst,
                PartyAddress = partyAddr,
                SubTotal = subTotal,
                TaxRate = request.TaxRate,
                TaxAmount = taxAmount,
                Discount = request.Discount,
                OtherCharges = request.OtherCharges,
                GrandTotal = grandTotal,
                PaidAmount = paidAmount,
                DueAmount = dueAmount,
                PaymentStatus = paymentStatus,
                PaymentMode = request.PaymentMode,
                Remarks = request.Remarks,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                Items = invoiceItems
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            // Link all shipments to this invoice and mark them as paid if invoice is settled
            foreach (var s in shipments)
            {
                s.InvoiceId = invoice.Id;
                s.InvoiceNo = invoice.InvoiceNo;
                s.InvoiceDate = invoice.InvoiceDate;

                // When settling the bill book or invoice, bilty is marked as paid
                if (paymentStatus == InvoicePaymentStatus.Paid)
                {
                    s.PaidAmount = s.GrandTotal;
                    s.DueAmount = 0;
                    if (s.Status == ShipmentStatus.InTransit || s.Status == ShipmentStatus.OutForDelivery)
                    {
                        s.Status = ShipmentStatus.Delivered;
                    }
                }
                s.UpdatedAt = DateTime.UtcNow;
                s.UpdatedBy = userId;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Generated Bill Book Freight Invoice {InvoiceNo} with {Count} bilties", invoice.InvoiceNo, shipments.Count);

            var invoiceDto = MapToDto(invoice);
            return new BillBookInvoiceResponseDto
            {
                Success = true,
                Message = $"Freight Bill {invoice.InvoiceNo} created successfully with {shipments.Count} bilty consignment(s).",
                Data = invoiceDto
            };
        }

        private static UnbilledShipmentDto MapToUnbilledDto(Shipment s)
        {
            var totalPkg = s.Items?.Sum(i => i.Quantity) ?? 1;
            var totalWt = s.Items?.Sum(i => i.Weight) ?? 0;
            var custInv = s.InvoiceReferences?.FirstOrDefault()?.CustomerInvoiceNo ?? s.Remarks;
            var eway = s.InvoiceReferences?.FirstOrDefault()?.EwayBillNo ?? s.EwayBillNo;
            var biltyAmt = s.GrandTotal > 0 ? s.GrandTotal : s.TotalFreight;
            var rate = totalWt > 0 ? Math.Round(biltyAmt / totalWt, 2) : (totalPkg > 0 ? Math.Round(biltyAmt / totalPkg, 2) : biltyAmt);

            return new UnbilledShipmentDto
            {
                Id = s.Id,
                ShipmentNo = s.ShipmentNo,
                ShipmentDate = s.ShipmentDate,
                ConsignorPartyId = s.ConsignorPartyId,
                ConsignorName = s.ConsignorName,
                ConsignorGstNo = s.ConsignorGstNo,
                ConsignorMobile = s.ConsignorMobile,
                ConsignorAddress = s.ConsignorAddress,
                ConsigneePartyId = s.ConsigneePartyId,
                ConsigneeName = s.ConsigneeName,
                ConsigneeGstNo = s.ConsigneeGstNo,
                ConsigneeMobile = s.ConsigneeMobile,
                ConsigneeAddress = s.ConsigneeAddress,
                FromLocation = s.FromLocation,
                ToLocation = s.ToLocation,
                TotalPackages = totalPkg,
                TotalWeightKg = totalWt,
                Rate = rate,
                GoodsValue = s.GoodsValue,
                TotalFreight = s.TotalFreight,
                TotalOtherCharges = s.TotalOtherCharges,
                TotalTaxAmount = s.TotalTaxAmount,
                GrandTotal = s.GrandTotal,
                PaidAmount = s.PaidAmount,
                DueAmount = s.DueAmount,
                PaymentTerm = s.PaymentTerm,
                Status = s.Status,
                CustomerInvoiceNo = custInv,
                EwayBillNo = eway,
                Remarks = s.Remarks,
                DeliveryDate = s.Status == ShipmentStatus.Delivered ? DateOnly.FromDateTime(s.UpdatedAt ?? s.CreatedAt) : null
            };
        }

        public async Task<BulkBillResultDto> BulkBillAsync(BulkBillRequest request, int? userId = null)
        {
            var result = new BulkBillResultDto();
            if (request.ShipmentIds == null || request.ShipmentIds.Count == 0)
            {
                result.Message = "No consignments selected.";
                return result;
            }

            // Only bill eligible, still-unbilled consignments (guards against double-billing).
            var shipments = await _context.Shipments
                .Where(s => request.ShipmentIds.Contains(s.Id) && s.IsActive && s.InvoiceId == null
                    && s.Status != ShipmentStatus.Cancelled && s.Status != ShipmentStatus.Draft)
                .ToListAsync();

            if (shipments.Count == 0)
            {
                result.Message = "Selected consignments are already billed or not eligible.";
                return result;
            }

            var invoiceDate = request.InvoiceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // One invoice per consignor (grouped by party id, else by name).
            var groups = shipments.GroupBy(s => s.ConsignorPartyId.HasValue
                ? $"P:{s.ConsignorPartyId.Value}"
                : $"N:{(s.ConsignorName ?? "Unknown").ToLowerInvariant()}");

            foreach (var group in groups)
            {
                var first = group.First();
                var items = group.Select(s => new CreateInvoiceItemRequest
                {
                    ShipmentId = s.Id,
                    ShipmentNo = s.ShipmentNo,
                    Description = $"Freight — GR {s.ShipmentNo} ({s.FromLocation} → {s.ToLocation})",
                    Quantity = 1,
                    Rate = s.GrandTotal > 0 ? s.GrandTotal : s.TotalFreight,
                    TaxRate = request.TaxRate
                }).ToList();

                var createReq = new CreateInvoiceRequest
                {
                    InvoiceDate = invoiceDate,
                    PartyId = first.ConsignorPartyId,
                    PartyName = first.ConsignorName,
                    TaxRate = request.TaxRate,
                    Items = items,
                    ShipmentIdsToLink = group.Select(s => s.Id).ToList()
                };

                var inv = await CreateInvoiceAsync(createReq, userId);
                result.InvoicesCreated++;
                result.ShipmentsBilled += group.Count();
                result.TotalAmount += inv.GrandTotal;
                result.InvoiceNos.Add(inv.InvoiceNo);
            }

            _logger.LogInformation("Bulk-billed {Count} shipments into {Invoices} invoices", result.ShipmentsBilled, result.InvoicesCreated);
            result.Message = $"Generated {result.InvoicesCreated} invoice(s) for {result.ShipmentsBilled} consignment(s).";
            return result;
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
