using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class MoneyReceiptService : IMoneyReceiptService
    {
        private readonly KTransportDbContext _context;

        public MoneyReceiptService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<List<MoneyReceiptDto>> GetReceiptsAsync(MoneyReceiptFilterRequest filter)
        {
            // Money receipts are only generated for PAID bilties
            var query = _context.Shipments
                .Include(s => s.ChargeItems)
                .Include(s => s.Items)
                .Where(s => s.IsActive && s.PaymentTerm == PaymentTerm.Paid && s.PaidAmount > 0);

            if (filter.StartDate.HasValue)
            {
                query = query.Where(s => s.ShipmentDate >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(s => s.ShipmentDate <= filter.EndDate.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var q = filter.Search.Trim().ToLower();
                query = query.Where(s =>
                    s.ShipmentNo.ToLower().Contains(q) ||
                    (s.ConsignorName != null && s.ConsignorName.ToLower().Contains(q)) ||
                    (s.ConsigneeName != null && s.ConsigneeName.ToLower().Contains(q)) ||
                    (s.ToLocation != null && s.ToLocation.ToLower().Contains(q)));
            }

            var shipments = await query
                .OrderByDescending(s => s.ShipmentDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync();

            return shipments.Select(MapToReceiptDto).ToList();
        }

        public async Task<MoneyReceiptDto?> GetReceiptByIdAsync(long id)
        {
            var shipment = await _context.Shipments
                .Include(s => s.ChargeItems)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive && s.PaymentTerm == PaymentTerm.Paid);

            return shipment == null ? null : MapToReceiptDto(shipment);
        }

        public async Task<MoneyReceiptDto?> GetReceiptByShipmentIdAsync(long shipmentId)
        {
            var shipment = await _context.Shipments
                .Include(s => s.ChargeItems)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == shipmentId && s.IsActive && s.PaymentTerm == PaymentTerm.Paid);

            return shipment == null ? null : MapToReceiptDto(shipment);
        }

        private static MoneyReceiptDto MapToReceiptDto(Shipment s)
        {
            var charges = s.ChargeItems ?? new List<ShipmentChargeItem>();
            var items = s.Items ?? new List<ShipmentItem>();

            decimal hamali = charges
                .Where(c => c.ChargeName.ToLower().Contains("hamali") || c.ChargeName.ToLower().Contains("loading"))
                .Sum(c => c.Amount);

            decimal dd = charges
                .Where(c => c.ChargeName.ToLower().Contains("dd") || c.ChargeName.ToLower().Contains("door"))
                .Sum(c => c.Amount);

            decimal st = charges
                .Where(c => c.ChargeName.ToLower().Contains("st.") || c.ChargeName.ToLower().Contains("stationery"))
                .Sum(c => c.Amount);

            decimal surcharge = charges
                .Where(c => c.ChargeName.ToLower().Contains("sur") || c.ChargeName.ToLower().Contains("s."))
                .Sum(c => c.Amount);

            decimal other = charges
                .Where(c => !c.ChargeName.ToLower().Contains("hamali") &&
                            !c.ChargeName.ToLower().Contains("loading") &&
                            !c.ChargeName.ToLower().Contains("dd") &&
                            !c.ChargeName.ToLower().Contains("door") &&
                            !c.ChargeName.ToLower().Contains("st.") &&
                            !c.ChargeName.ToLower().Contains("stationery") &&
                            !c.ChargeName.ToLower().Contains("sur") &&
                            !c.ChargeName.ToLower().Contains("s."))
                .Sum(c => c.Amount);

            int pkgs = items.Sum(it => it.Quantity);
            if (pkgs <= 0) pkgs = 1;

            decimal weight = items.Sum(it => it.Weight);

            return new MoneyReceiptDto
            {
                Id = s.Id,
                ReceiptNo = $"MR-{s.ShipmentNo}",
                ReceiptDate = s.ShipmentDate,
                ShipmentId = s.Id,
                ShipmentNo = s.ShipmentNo,
                PayerName = s.ConsignorName ?? "Cash Customer",
                PayerGstNo = s.ConsignorGstNo,
                PayerMobile = s.ConsignorMobile,
                FromLocation = s.FromLocation ?? "Zero Mile, Pahari, Patna-7",
                ToLocation = s.ToLocation,
                TotalPackages = pkgs,
                TotalWeightKg = weight,
                BaseFreight = s.TotalFreight,
                HamaliCharges = hamali,
                DoorDeliveryCharges = dd,
                StationeryCharges = st,
                Surcharges = surcharge,
                OtherCharges = other,
                GstAmount = s.TotalTaxAmount,
                TotalAmount = s.GrandTotal > 0 ? s.GrandTotal : s.PaidAmount,
                PaymentMode = "Cash",
                CollectedBy = s.BookingClerk ?? "Counter Cashier",
                Remarks = s.Remarks,
                CreatedAt = s.CreatedAt
            };
        }
    }
}
