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
            // Money receipts are generated for PAID bilties (both collected on spot and pending collection)
            var query = _context.Shipments
                .Include(s => s.ChargeItems)
                .Include(s => s.Items)
                .Where(s => s.IsActive && s.PaymentTerm == PaymentTerm.Paid);

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var st = filter.Status.Trim().ToUpper();
                if (st == "PENDING")
                {
                    query = query.Where(s => s.DueAmount > 0 || !s.IsSettled || s.PaidAmount == 0);
                }
                else if (st == "COLLECTED")
                {
                    query = query.Where(s => s.PaidAmount > 0 && s.IsSettled);
                }
            }

            if (!string.IsNullOrWhiteSpace(filter.PaymentMode) && filter.PaymentMode != "ALL")
            {
                var pm = filter.PaymentMode.Trim().ToLower();
                query = query.Where(s => (s.SettledPaymentMode ?? "cash").ToLower().Contains(pm));
            }

            if (filter.StartDate.HasValue)
            {
                query = query.Where(s => (s.DeliveryDate ?? s.ShipmentDate) >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(s => (s.DeliveryDate ?? s.ShipmentDate) <= filter.EndDate.Value);
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
                .OrderByDescending(s => s.DeliveryDate ?? s.ShipmentDate)
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

        public async Task<MoneyReceiptDto?> CollectPaymentAsync(long shipmentId, CollectMoneyReceiptRequest request)
        {
            var shipment = await _context.Shipments
                .Include(s => s.ChargeItems)
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == shipmentId && s.IsActive && s.PaymentTerm == PaymentTerm.Paid);

            if (shipment == null) return null;

            decimal grandTotal = shipment.GrandTotal > 0 ? shipment.GrandTotal : shipment.TotalFreight;
            decimal amt = request.ReceivedAmount.HasValue && request.ReceivedAmount.Value > 0
                ? request.ReceivedAmount.Value
                : (shipment.DueAmount > 0 ? shipment.DueAmount : grandTotal);

            shipment.PaidAmount = Math.Min(grandTotal, shipment.PaidAmount + amt);
            shipment.DueAmount = Math.Max(0, grandTotal - shipment.PaidAmount);
            shipment.SettledReceivedAmount = shipment.PaidAmount;
            shipment.SettledPaymentMode = request.PaymentMode;
            shipment.SettlementReferenceNo = request.ReferenceNo;
            shipment.DeliveryDate = request.PaymentDate;
            shipment.IsSettled = shipment.DueAmount == 0;
            if (!string.IsNullOrWhiteSpace(request.Remarks))
            {
                shipment.Remarks = string.IsNullOrWhiteSpace(shipment.Remarks)
                    ? request.Remarks
                    : $"{shipment.Remarks} | MR Collection: {request.Remarks}";
            }

            await _context.SaveChangesAsync();
            return MapToReceiptDto(shipment);
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

            decimal biltyGrandTotal = s.GrandTotal > 0 ? s.GrandTotal : (s.PaidAmount > 0 ? s.PaidAmount : s.TotalFreight);
            decimal mrCharge = 10m; // 10 rupee fixed
            decimal totalWithMrCharge = biltyGrandTotal + mrCharge;
            bool isCollected = s.IsSettled || (s.PaidAmount >= biltyGrandTotal && biltyGrandTotal > 0);

            return new MoneyReceiptDto
            {
                Id = s.Id,
                ReceiptNo = $"MR-{s.ShipmentNo}",
                ReceiptDate = s.DeliveryDate ?? s.ShipmentDate,
                ShipmentId = s.Id,
                ShipmentNo = s.ShipmentNo,
                PayerName = s.ConsignorName ?? "Cash Customer",
                PayerGstNo = s.ConsignorGstNo,
                PayerMobile = s.ConsignorMobile,
                FromLocation = s.FromLocation ?? "Zero Mile, Pahari, Patna-7",
                ToLocation = s.ToLocation,
                TotalPackages = pkgs,
                TotalWeightKg = weight,
                BaseFreight = biltyGrandTotal,
                MoneyReceiptCharge = mrCharge,
                HamaliCharges = hamali,
                DoorDeliveryCharges = dd,
                StationeryCharges = 0,
                Surcharges = surcharge,
                OtherCharges = other,
                GstAmount = s.TotalTaxAmount,
                TotalAmount = totalWithMrCharge,
                PaymentMode = !string.IsNullOrWhiteSpace(s.SettledPaymentMode) ? s.SettledPaymentMode : "Cash",
                TransactionRef = s.SettlementReferenceNo,
                CollectedBy = s.BookingClerk ?? "Counter Cashier",
                Remarks = s.Remarks,
                CreatedAt = s.CreatedAt,
                IsCollected = isCollected,
                PaidAmount = s.PaidAmount,
                DueAmount = s.DueAmount
            };
        }
    }
}
