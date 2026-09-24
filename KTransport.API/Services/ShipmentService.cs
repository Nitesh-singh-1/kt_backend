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
    public class ShipmentService : IShipmentService
    {
        private readonly ILogger<ShipmentService> _logger;
        private readonly KTransportDbContext _context;
        private readonly INumberingSequenceService _sequenceService;
        private readonly ITenantContext _tenantContext;

        public ShipmentService(
            ILogger<ShipmentService> logger,
            KTransportDbContext context,
            INumberingSequenceService sequenceService,
            ITenantContext tenantContext)
        {
            _logger = logger;
            _context = context;
            _sequenceService = sequenceService;
            _tenantContext = tenantContext;
        }

        public async Task<ShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, int userId)
        {
            try
            {
                var tenantId = _tenantContext.CurrentTenantId;

                // Generate GR / Shipment Number if not supplied
                var shipmentNo = string.IsNullOrWhiteSpace(request.ShipmentNo)
                    ? await _sequenceService.GetNextNumberAsync("SHIPMENT", "GR")
                    : request.ShipmentNo.Trim();

                // Check for duplicates within tenant
                var exists = await _context.Set<Shipment>()
                    .AnyAsync(s => s.ShipmentNo.ToUpper() == shipmentNo.ToUpper());

                if (exists)
                {
                    return new ShipmentResponse
                    {
                        Success = false,
                        Message = $"Shipment / GR Number '{shipmentNo}' already exists."
                    };
                }

                // Calculate other charges sum
                decimal otherCharges = request.ChargeItems?.Sum(c => c.Amount) ?? 0;
                decimal grandTotal = request.TotalFreight + otherCharges + request.TotalTaxAmount;
                decimal paid = request.PaidAmount;
                decimal due = grandTotal - paid;

                // Resolve or auto-save Consignor Party
                long? consignorPartyId = request.ConsignorPartyId;
                string? consignorName = request.ConsignorName;
                string? consignorGstNo = request.ConsignorGstNo;
                string? consignorMobile = request.ConsignorMobile;
                string? consignorAddress = request.ConsignorAddress;

                if (consignorPartyId.HasValue)
                {
                    var party = await _context.Parties.FindAsync(consignorPartyId.Value);
                    if (party != null)
                    {
                        consignorName ??= party.Name;
                        consignorGstNo ??= party.GstNo;
                        consignorMobile ??= party.Mobile;
                        consignorAddress ??= party.Address;
                    }
                }
                else if (request.SaveConsignorAsParty && !string.IsNullOrWhiteSpace(consignorName))
                {
                    var newParty = new Party
                    {
                        Name = consignorName.Trim(),
                        GstNo = consignorGstNo?.Trim(),
                        Mobile = consignorMobile?.Trim(),
                        Address = consignorAddress?.Trim(),
                        PartyType = PartyType.Both,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Parties.Add(newParty);
                    await _context.SaveChangesAsync();
                    consignorPartyId = newParty.Id;
                }

                // Resolve or auto-save Consignee Party
                long? consigneePartyId = request.ConsigneePartyId;
                string? consigneeName = request.ConsigneeName;
                string? consigneeGstNo = request.ConsigneeGstNo;
                string? consigneeMobile = request.ConsigneeMobile;
                string? consigneeAddress = request.ConsigneeAddress;

                if (consigneePartyId.HasValue)
                {
                    var party = await _context.Parties.FindAsync(consigneePartyId.Value);
                    if (party != null)
                    {
                        consigneeName ??= party.Name;
                        consigneeGstNo ??= party.GstNo;
                        consigneeMobile ??= party.Mobile;
                        consigneeAddress ??= party.Address;
                    }
                }
                else if (request.SaveConsigneeAsParty && !string.IsNullOrWhiteSpace(consigneeName))
                {
                    var newParty = new Party
                    {
                        Name = consigneeName.Trim(),
                        GstNo = consigneeGstNo?.Trim(),
                        Mobile = consigneeMobile?.Trim(),
                        Address = consigneeAddress?.Trim(),
                        PartyType = PartyType.Both,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Parties.Add(newParty);
                    await _context.SaveChangesAsync();
                    consigneePartyId = newParty.Id;
                }

                // Auto-consolidate GoodsValue and InvoiceNo if multiple customer invoices provided
                var goodsValue = request.GoodsValue;
                var invoiceNo = request.InvoiceNo;
                var ewayBillNo = request.EwayBillNo;
                var ewayBillValidUpto = request.EwayBillValidUpto;

                if (request.CustomerInvoices != null && request.CustomerInvoices.Any())
                {
                    if (goodsValue <= 0)
                    {
                        goodsValue = request.CustomerInvoices.Sum(ci => ci.DeclaredGoodsValue);
                    }
                    if (string.IsNullOrWhiteSpace(invoiceNo))
                    {
                        invoiceNo = string.Join(", ", request.CustomerInvoices.Select(ci => ci.CustomerInvoiceNo));
                    }
                    if (string.IsNullOrWhiteSpace(ewayBillNo))
                    {
                        var firstEwb = request.CustomerInvoices.FirstOrDefault(ci => !string.IsNullOrWhiteSpace(ci.EwayBillNo));
                        if (firstEwb != null)
                        {
                            ewayBillNo = firstEwb.EwayBillNo;
                            ewayBillValidUpto = firstEwb.EwayBillValidUpto;
                        }
                    }
                }

                var shipment = new Shipment
                {
                    TenantId = tenantId,
                    ShipmentNo = shipmentNo,
                    InvoiceNo = invoiceNo,
                    InvoiceId = request.InvoiceId,
                    ShipmentDate = request.ShipmentDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                    InvoiceDate = request.InvoiceDate,
                    FromLocation = request.FromLocation,
                    ToLocation = request.ToLocation,
                    TruckNo = request.TruckNo,
                    TaxTreatment = request.TaxTreatment,
                    GstPaidBy = request.GstPaidBy,
                    ConsignorPartyId = consignorPartyId,
                    ConsignorName = consignorName,
                    ConsignorGstNo = consignorGstNo,
                    ConsignorMobile = consignorMobile,
                    ConsignorAddress = consignorAddress,
                    ConsigneePartyId = consigneePartyId,
                    ConsigneeName = consigneeName,
                    ConsigneeGstNo = consigneeGstNo,
                    ConsigneeMobile = consigneeMobile,
                    ConsigneeAddress = consigneeAddress,
                    GoodsValue = goodsValue,
                    PaymentTerm = request.PaymentTerm,
                    TotalFreight = request.TotalFreight,
                    TotalOtherCharges = otherCharges,
                    TotalTaxAmount = request.TotalTaxAmount,
                    GrandTotal = grandTotal,
                    PaidAmount = paid,
                    DueAmount = due,
                    Status = ShipmentStatus.Booked,
                    Remarks = request.Remarks,
                    BookingClerk = request.BookingClerk,
                    OriginHubId = request.OriginHubId,
                    DestinationHubId = request.DestinationHubId,
                    CurrentHubId = request.OriginHubId,
                    DeliveryType = request.DeliveryType,
                    EwayBillNo = ewayBillNo,
                    EwayBillValidUpto = ewayBillValidUpto,
                    CreatedBy = userId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                // Add customer invoice references (N:M support)
                if (request.CustomerInvoices != null && request.CustomerInvoices.Any())
                {
                    foreach (var inv in request.CustomerInvoices)
                    {
                        shipment.InvoiceReferences.Add(new ConsignmentInvoiceReference
                        {
                            TenantId = tenantId,
                            CustomerInvoiceNo = inv.CustomerInvoiceNo.Trim(),
                            CustomerInvoiceDate = inv.CustomerInvoiceDate,
                            DeclaredGoodsValue = inv.DeclaredGoodsValue,
                            EwayBillNo = inv.EwayBillNo?.Trim(),
                            EwayBillDate = inv.EwayBillDate,
                            EwayBillValidUpto = inv.EwayBillValidUpto,
                            DocumentType = inv.DocumentType ?? "TaxInvoice",
                            PackageCount = inv.PackageCount,
                            WeightKg = inv.WeightKg,
                            CommodityDescription = inv.CommodityDescription,
                            DocumentUrl = inv.DocumentUrl,
                            CreatedBy = userId,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
                else if (!string.IsNullOrWhiteSpace(request.InvoiceNo))
                {
                    // Backward-compatible auto-creation of single invoice reference
                    shipment.InvoiceReferences.Add(new ConsignmentInvoiceReference
                    {
                        TenantId = tenantId,
                        CustomerInvoiceNo = request.InvoiceNo.Trim(),
                        CustomerInvoiceDate = request.InvoiceDate ?? shipment.ShipmentDate,
                        DeclaredGoodsValue = request.GoodsValue,
                        EwayBillNo = request.EwayBillNo?.Trim(),
                        EwayBillValidUpto = request.EwayBillValidUpto,
                        DocumentType = "TaxInvoice",
                        CreatedBy = userId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Add cargo items
                if (request.Items != null && request.Items.Any())
                {
                    foreach (var item in request.Items)
                    {
                        shipment.Items.Add(new ShipmentItem
                        {
                            TenantId = tenantId,
                            Article = item.Article,
                            Description = item.Description,
                            Weight = item.Weight,
                            Rate = item.Rate,
                            Quantity = item.Quantity > 0 ? item.Quantity : 1,
                            TotalAmount = item.TotalAmount > 0 ? item.TotalAmount : (item.Weight * item.Rate),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Add dynamic charge items
                if (request.ChargeItems != null && request.ChargeItems.Any())
                {
                    foreach (var charge in request.ChargeItems)
                    {
                        shipment.ChargeItems.Add(new ShipmentChargeItem
                        {
                            TenantId = tenantId,
                            ChargeTypeId = charge.ChargeTypeId,
                            ChargeName = charge.ChargeName,
                            Amount = charge.Amount,
                            IsTaxable = charge.IsTaxable,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Record initial status history
                shipment.StatusHistory.Add(new ShipmentStatusHistory
                {
                    TenantId = tenantId,
                    FromStatus = ShipmentStatus.Draft,
                    ToStatus = ShipmentStatus.Booked,
                    Location = request.FromLocation,
                    Remarks = "Consignment / LR booked into system.",
                    ChangedByUserId = userId,
                    ChangedAt = DateTime.UtcNow
                });

                _context.Set<Shipment>().Add(shipment);
                await _context.SaveChangesAsync();

                var created = await GetShipmentEntityByIdAsync(shipment.Id) ?? shipment;
                return new ShipmentResponse
                {
                    Success = true,
                    Message = "Shipment / LR created successfully.",
                    Data = MapToDto(created)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shipment / LR");
                return new ShipmentResponse
                {
                    Success = false,
                    Message = "An unexpected error occurred while creating shipment."
                };
            }
        }

        public async Task<ShipmentResponse> UpdateShipmentAsync(long id, UpdateShipmentRequest request, int userId)
        {
            try
            {
                var shipment = await _context.Set<Shipment>()
                    .Include(s => s.Items)
                    .Include(s => s.ChargeItems)
                    .Include(s => s.InvoiceReferences)
                    .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

                if (shipment == null)
                {
                    return new ShipmentResponse { Success = false, Message = "Shipment not found." };
                }

                // Recalculate financial totals
                decimal otherCharges = request.ChargeItems?.Sum(c => c.Amount) ?? 0;
                decimal grandTotal = request.TotalFreight + otherCharges + request.TotalTaxAmount;
                decimal paid = request.PaidAmount;
                decimal due = grandTotal - paid;

                var goodsValue = request.GoodsValue;
                var invoiceNo = request.InvoiceNo;
                var ewayBillNo = request.EwayBillNo;
                var ewayBillValidUpto = request.EwayBillValidUpto;

                if (request.CustomerInvoices != null && request.CustomerInvoices.Any())
                {
                    if (goodsValue <= 0)
                    {
                        goodsValue = request.CustomerInvoices.Sum(ci => ci.DeclaredGoodsValue);
                    }
                    if (string.IsNullOrWhiteSpace(invoiceNo))
                    {
                        invoiceNo = string.Join(", ", request.CustomerInvoices.Select(ci => ci.CustomerInvoiceNo));
                    }
                    if (string.IsNullOrWhiteSpace(ewayBillNo))
                    {
                        var firstEwb = request.CustomerInvoices.FirstOrDefault(ci => !string.IsNullOrWhiteSpace(ci.EwayBillNo));
                        if (firstEwb != null)
                        {
                            ewayBillNo = firstEwb.EwayBillNo;
                            ewayBillValidUpto = firstEwb.EwayBillValidUpto;
                        }
                    }
                }

                shipment.InvoiceNo = invoiceNo;
                shipment.InvoiceId = request.InvoiceId;
                if (request.ShipmentDate.HasValue) shipment.ShipmentDate = request.ShipmentDate.Value;
                shipment.InvoiceDate = request.InvoiceDate;
                shipment.FromLocation = request.FromLocation;
                shipment.ToLocation = request.ToLocation;
                shipment.TruckNo = request.TruckNo;
                shipment.TaxTreatment = request.TaxTreatment;
                shipment.GstPaidBy = request.GstPaidBy;
                shipment.ConsignorPartyId = request.ConsignorPartyId;
                shipment.ConsignorName = request.ConsignorName;
                shipment.ConsignorGstNo = request.ConsignorGstNo;
                shipment.ConsignorMobile = request.ConsignorMobile;
                shipment.ConsignorAddress = request.ConsignorAddress;
                shipment.ConsigneePartyId = request.ConsigneePartyId;
                shipment.ConsigneeName = request.ConsigneeName;
                shipment.ConsigneeGstNo = request.ConsigneeGstNo;
                shipment.ConsigneeMobile = request.ConsigneeMobile;
                shipment.ConsigneeAddress = request.ConsigneeAddress;
                shipment.GoodsValue = goodsValue;
                shipment.PaymentTerm = request.PaymentTerm;
                shipment.TotalFreight = request.TotalFreight;
                shipment.TotalOtherCharges = otherCharges;
                shipment.TotalTaxAmount = request.TotalTaxAmount;
                shipment.GrandTotal = grandTotal;
                shipment.PaidAmount = paid;
                shipment.DueAmount = due;
                shipment.Remarks = request.Remarks;
                shipment.BookingClerk = request.BookingClerk;
                shipment.OriginHubId = request.OriginHubId;
                shipment.DestinationHubId = request.DestinationHubId;
                shipment.DeliveryType = request.DeliveryType;
                shipment.EwayBillNo = ewayBillNo;
                shipment.EwayBillValidUpto = ewayBillValidUpto;
                shipment.UpdatedBy = userId;
                shipment.UpdatedAt = DateTime.UtcNow;

                // Sync customer invoice references
                if (request.CustomerInvoices != null)
                {
                    _context.Set<ConsignmentInvoiceReference>().RemoveRange(shipment.InvoiceReferences);
                    foreach (var inv in request.CustomerInvoices)
                    {
                        shipment.InvoiceReferences.Add(new ConsignmentInvoiceReference
                        {
                            TenantId = shipment.TenantId,
                            ShipmentId = shipment.Id,
                            CustomerInvoiceNo = inv.CustomerInvoiceNo.Trim(),
                            CustomerInvoiceDate = inv.CustomerInvoiceDate,
                            DeclaredGoodsValue = inv.DeclaredGoodsValue,
                            EwayBillNo = inv.EwayBillNo?.Trim(),
                            EwayBillDate = inv.EwayBillDate,
                            EwayBillValidUpto = inv.EwayBillValidUpto,
                            DocumentType = inv.DocumentType ?? "TaxInvoice",
                            PackageCount = inv.PackageCount,
                            WeightKg = inv.WeightKg,
                            CommodityDescription = inv.CommodityDescription,
                            CreatedBy = userId,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Sync items
                if (request.Items != null)
                {
                    _context.Set<ShipmentItem>().RemoveRange(shipment.Items);
                    foreach (var item in request.Items)
                    {
                        shipment.Items.Add(new ShipmentItem
                        {
                            TenantId = shipment.TenantId,
                            ShipmentId = shipment.Id,
                            Article = item.Article,
                            Description = item.Description,
                            Weight = item.Weight,
                            Rate = item.Rate,
                            Quantity = item.Quantity > 0 ? item.Quantity : 1,
                            TotalAmount = item.TotalAmount > 0 ? item.TotalAmount : (item.Weight * item.Rate),
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                // Sync charge items
                if (request.ChargeItems != null)
                {
                    _context.Set<ShipmentChargeItem>().RemoveRange(shipment.ChargeItems);
                    foreach (var charge in request.ChargeItems)
                    {
                        shipment.ChargeItems.Add(new ShipmentChargeItem
                        {
                            TenantId = shipment.TenantId,
                            ShipmentId = shipment.Id,
                            ChargeTypeId = charge.ChargeTypeId,
                            ChargeName = charge.ChargeName,
                            Amount = charge.Amount,
                            IsTaxable = charge.IsTaxable,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                await _context.SaveChangesAsync();

                var updated = await GetShipmentEntityByIdAsync(shipment.Id) ?? shipment;
                return new ShipmentResponse
                {
                    Success = true,
                    Message = "Shipment updated successfully.",
                    Data = MapToDto(updated)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shipment {Id}", id);
                return new ShipmentResponse { Success = false, Message = "An error occurred while updating shipment." };
            }
        }

        public async Task<ShipmentResponse> UpdateShipmentStatusAsync(long id, UpdateShipmentStatusRequest request, int userId)
        {
            try
            {
                var shipment = await _context.Set<Shipment>()
                    .Include(s => s.StatusHistory)
                    .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

                if (shipment == null)
                {
                    return new ShipmentResponse { Success = false, Message = "Shipment not found." };
                }

                var currentStatus = shipment.Status;
                var targetStatus = request.NewStatus;

                // Validate state machine transition
                if (!IsValidTransition(currentStatus, targetStatus))
                {
                    return new ShipmentResponse
                    {
                        Success = false,
                        Message = $"Cannot transition shipment from '{currentStatus}' to '{targetStatus}'."
                    };
                }

                shipment.Status = targetStatus;
                shipment.UpdatedBy = userId;
                shipment.UpdatedAt = DateTime.UtcNow;

                // Add audit history
                shipment.StatusHistory.Add(new ShipmentStatusHistory
                {
                    TenantId = shipment.TenantId,
                    ShipmentId = shipment.Id,
                    FromStatus = currentStatus,
                    ToStatus = targetStatus,
                    Location = request.Location,
                    Remarks = request.Remarks,
                    ChangedByUserId = userId,
                    ChangedAt = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                var updated = await GetShipmentEntityByIdAsync(shipment.Id) ?? shipment;
                return new ShipmentResponse
                {
                    Success = true,
                    Message = $"Shipment status updated to '{targetStatus}'.",
                    Data = MapToDto(updated)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating shipment status {Id}", id);
                return new ShipmentResponse { Success = false, Message = "An error occurred while updating status." };
            }
        }

        public async Task<ShipmentResponse> GetShipmentByIdAsync(long id)
        {
            var shipment = await GetShipmentEntityByIdAsync(id);
            if (shipment == null) return new ShipmentResponse { Success = false, Message = "Shipment not found." };
            return new ShipmentResponse { Success = true, Data = MapToDto(shipment) };
        }

        public async Task<ShipmentResponse> GetShipmentByNoAsync(string shipmentNo)
        {
            var shipment = await _context.Set<Shipment>()
                .Include(s => s.CreatedByNavigation)
                .Include(s => s.OriginHub)
                .Include(s => s.DestinationHub)
                .Include(s => s.CurrentHub)
                .Include(s => s.InvoiceReferences)
                .Include(s => s.Items)
                .Include(s => s.ChargeItems)
                .Include(s => s.StatusHistory)
                    .ThenInclude(h => h.ChangedByUser)
                .FirstOrDefaultAsync(s => s.ShipmentNo == shipmentNo && s.IsActive);

            if (shipment == null) return new ShipmentResponse { Success = false, Message = "Shipment not found." };
            return new ShipmentResponse { Success = true, Data = MapToDto(shipment) };
        }

        public async Task<ShipmentListResponse> GetAllShipmentsAsync(
            ShipmentStatus? status = null, 
            TaxTreatment? taxTreatment = null, 
            string? search = null, 
            int page = 1, 
            int pageSize = 50)
        {
            var query = _context.Set<Shipment>()
                .Where(s => s.IsActive)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(s => s.Status == status.Value);
            }

            if (taxTreatment.HasValue)
            {
                query = query.Where(s => s.TaxTreatment == taxTreatment.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(s =>
                    s.ShipmentNo.ToLower().Contains(term) ||
                    (s.InvoiceNo != null && s.InvoiceNo.ToLower().Contains(term)) ||
                    (s.EwayBillNo != null && s.EwayBillNo.ToLower().Contains(term)) ||
                    (s.ConsignorName != null && s.ConsignorName.ToLower().Contains(term)) ||
                    (s.ConsigneeName != null && s.ConsigneeName.ToLower().Contains(term)) ||
                    (s.TruckNo != null && s.TruckNo.ToLower().Contains(term)) ||
                    (s.FromLocation != null && s.FromLocation.ToLower().Contains(term)) ||
                    (s.ToLocation != null && s.ToLocation.ToLower().Contains(term)) ||
                    s.InvoiceReferences.Any(ir => ir.CustomerInvoiceNo.ToLower().Contains(term) || (ir.EwayBillNo != null && ir.EwayBillNo.ToLower().Contains(term))));
            }

            var total = await query.CountAsync();
            var list = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(s => s.CreatedByNavigation)
                .Include(s => s.OriginHub)
                .Include(s => s.DestinationHub)
                .Include(s => s.CurrentHub)
                .Include(s => s.InvoiceReferences)
                .Include(s => s.Items)
                .Include(s => s.ChargeItems)
                .ToListAsync();

            return new ShipmentListResponse
            {
                Success = true,
                TotalCount = total,
                Data = list.Select(MapToDto).Where(x => x != null).Select(x => x!).ToList()
            };
        }

        public async Task<bool> DeleteShipmentAsync(long id, int userId)
        {
            var shipment = await _context.Set<Shipment>().FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
            if (shipment == null) return false;

            shipment.IsActive = false;
            shipment.Status = ShipmentStatus.Cancelled;
            shipment.UpdatedBy = userId;
            shipment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<Shipment?> GetShipmentEntityByIdAsync(long id)
        {
            return await _context.Set<Shipment>()
                .Include(s => s.CreatedByNavigation)
                .Include(s => s.OriginHub)
                .Include(s => s.DestinationHub)
                .Include(s => s.CurrentHub)
                .Include(s => s.InvoiceReferences)
                .Include(s => s.Items)
                .Include(s => s.ChargeItems)
                .Include(s => s.StatusHistory)
                    .ThenInclude(h => h.ChangedByUser)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        }

        private static bool IsValidTransition(ShipmentStatus from, ShipmentStatus to)
        {
            if (from == to) return true;

            return from switch
            {
                ShipmentStatus.Draft => to is ShipmentStatus.Booked or ShipmentStatus.Cancelled,
                ShipmentStatus.Booked => to is ShipmentStatus.Manifested or ShipmentStatus.Cancelled or ShipmentStatus.InTransit,
                ShipmentStatus.Manifested => to is ShipmentStatus.InTransit or ShipmentStatus.Booked or ShipmentStatus.Cancelled,
                ShipmentStatus.InTransit => to is ShipmentStatus.OutForDelivery or ShipmentStatus.Returned or ShipmentStatus.Delivered,
                ShipmentStatus.OutForDelivery => to is ShipmentStatus.Delivered or ShipmentStatus.Returned or ShipmentStatus.InTransit,
                ShipmentStatus.Delivered => to is ShipmentStatus.Returned,
                ShipmentStatus.Returned => to is ShipmentStatus.Booked, // allow re-manifesting
                ShipmentStatus.Cancelled => false,
                _ => false
            };
        }

        private static ShipmentDto? MapToDto(Shipment? s)
        {
            if (s == null) return null;

            return new ShipmentDto
            {
                Id = s.Id,
                TenantId = s.TenantId,
                ShipmentNo = s.ShipmentNo,
                InvoiceNo = s.InvoiceNo,
                InvoiceId = s.InvoiceId,
                ShipmentDate = s.ShipmentDate,
                InvoiceDate = s.InvoiceDate,
                FromLocation = s.FromLocation,
                ToLocation = s.ToLocation,
                TruckNo = s.TruckNo,
                TaxTreatment = s.TaxTreatment,
                GstPaidBy = s.GstPaidBy,
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
                GoodsValue = s.GoodsValue,
                PaymentTerm = s.PaymentTerm,
                TotalFreight = s.TotalFreight,
                TotalOtherCharges = s.TotalOtherCharges,
                TotalTaxAmount = s.TotalTaxAmount,
                GrandTotal = s.GrandTotal,
                PaidAmount = s.PaidAmount,
                DueAmount = s.DueAmount,
                OriginHubId = s.OriginHubId,
                OriginHubName = s.OriginHub?.Name,
                DestinationHubId = s.DestinationHubId,
                DestinationHubName = s.DestinationHub?.Name,
                CurrentHubId = s.CurrentHubId,
                CurrentHubName = s.CurrentHub?.Name,
                DeliveryType = s.DeliveryType,
                EwayBillNo = s.EwayBillNo,
                EwayBillValidUpto = s.EwayBillValidUpto,
                Status = s.Status,
                Remarks = s.Remarks,
                BookingClerk = s.BookingClerk,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                CreatedByName = s.CreatedByNavigation?.FullName ?? s.CreatedByNavigation?.Username,
                InvoiceReferences = (s.InvoiceReferences ?? Enumerable.Empty<ConsignmentInvoiceReference>()).Where(ir => ir.IsActive).Select(ir => new ConsignmentInvoiceReferenceDto
                {
                    Id = ir.Id,
                    CustomerInvoiceNo = ir.CustomerInvoiceNo,
                    CustomerInvoiceDate = ir.CustomerInvoiceDate,
                    DeclaredGoodsValue = ir.DeclaredGoodsValue,
                    EwayBillNo = ir.EwayBillNo,
                    EwayBillDate = ir.EwayBillDate,
                    EwayBillValidUpto = ir.EwayBillValidUpto,
                    DocumentType = ir.DocumentType,
                    PackageCount = ir.PackageCount,
                    WeightKg = ir.WeightKg,
                    CommodityDescription = ir.CommodityDescription,
                    DocumentUrl = ir.DocumentUrl
                }).ToList(),
                Items = (s.Items ?? Enumerable.Empty<ShipmentItem>()).Select(i => new ShipmentItemDto
                {
                    Id = i.Id,
                    Article = i.Article,
                    Description = i.Description,
                    Weight = i.Weight,
                    Rate = i.Rate,
                    Quantity = i.Quantity,
                    TotalAmount = i.TotalAmount
                }).ToList(),
                ChargeItems = (s.ChargeItems ?? Enumerable.Empty<ShipmentChargeItem>()).Select(c => new ShipmentChargeItemDto
                {
                    Id = c.Id,
                    ChargeTypeId = c.ChargeTypeId,
                    ChargeName = c.ChargeName,
                    Amount = c.Amount,
                    IsTaxable = c.IsTaxable
                }).ToList(),
                StatusHistory = (s.StatusHistory ?? Enumerable.Empty<ShipmentStatusHistory>()).OrderBy(h => h.ChangedAt).Select(h => new ShipmentStatusHistoryDto
                {
                    Id = h.Id,
                    FromStatus = h.FromStatus,
                    ToStatus = h.ToStatus,
                    Location = h.Location,
                    Remarks = h.Remarks,
                    ChangedByUserName = h.ChangedByUser?.FullName ?? h.ChangedByUser?.Username,
                    ChangedAt = h.ChangedAt
                }).ToList()
            };
        }
    }
}
