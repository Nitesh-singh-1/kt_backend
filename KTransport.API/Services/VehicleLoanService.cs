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
    public class VehicleLoanService : IVehicleLoanService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<VehicleLoanService> _logger;

        public VehicleLoanService(KTransportDbContext context, ILogger<VehicleLoanService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<VehicleLoanDto>> GetAllAsync(string? search = null, bool activeOnly = false)
        {
            var query = _context.VehicleLoans.AsNoTracking().Where(l => l.IsActive);

            if (activeOnly)
                query = query.Where(l => !l.IsClosed);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(l =>
                    l.Lender.ToLower().Contains(s) ||
                    (l.VehicleNo != null && l.VehicleNo.ToLower().Contains(s)) ||
                    (l.LoanAccountNo != null && l.LoanAccountNo.ToLower().Contains(s)));
            }

            var list = await query.OrderBy(l => l.IsClosed).ThenBy(l => l.NextDueDate).ToListAsync();
            return list.Select(Map).ToList();
        }

        public async Task<VehicleLoanDto?> GetByIdAsync(long id)
        {
            var l = await _context.VehicleLoans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return l == null ? null : Map(l);
        }

        public async Task<VehicleLoanDto> CreateAsync(CreateVehicleLoanRequest request)
        {
            var loan = new VehicleLoan
            {
                VehicleId = request.VehicleId,
                VehicleNo = request.VehicleNo?.Trim().ToUpperInvariant(),
                Lender = request.Lender.Trim(),
                LoanAccountNo = request.LoanAccountNo?.Trim(),
                PrincipalAmount = request.PrincipalAmount,
                EmiAmount = request.EmiAmount,
                TenureMonths = request.TenureMonths,
                EmisPaid = request.EmisPaid,
                InterestRate = request.InterestRate,
                LoanStartDate = request.LoanStartDate,
                NextDueDate = request.NextDueDate,
                IsClosed = request.TenureMonths > 0 && request.EmisPaid >= request.TenureMonths,
                Remarks = request.Remarks?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.VehicleLoans.Add(loan);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created vehicle loan for {Vehicle} / {Lender} (ID: {Id})", loan.VehicleNo, loan.Lender, loan.Id);
            return Map(loan);
        }

        public async Task<VehicleLoanDto?> UpdateAsync(long id, UpdateVehicleLoanRequest request)
        {
            var l = await _context.VehicleLoans.FirstOrDefaultAsync(x => x.Id == id);
            if (l == null) return null;

            if (request.VehicleId.HasValue) l.VehicleId = request.VehicleId;
            if (request.VehicleNo != null) l.VehicleNo = request.VehicleNo.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(request.Lender)) l.Lender = request.Lender.Trim();
            if (request.LoanAccountNo != null) l.LoanAccountNo = request.LoanAccountNo.Trim();
            if (request.PrincipalAmount.HasValue) l.PrincipalAmount = request.PrincipalAmount.Value;
            if (request.EmiAmount.HasValue) l.EmiAmount = request.EmiAmount.Value;
            if (request.TenureMonths.HasValue) l.TenureMonths = request.TenureMonths.Value;
            if (request.EmisPaid.HasValue) l.EmisPaid = request.EmisPaid.Value;
            if (request.InterestRate.HasValue) l.InterestRate = request.InterestRate.Value;
            if (request.LoanStartDate.HasValue) l.LoanStartDate = request.LoanStartDate;
            if (request.NextDueDate.HasValue) l.NextDueDate = request.NextDueDate;
            if (request.IsClosed.HasValue) l.IsClosed = request.IsClosed.Value;
            if (request.Remarks != null) l.Remarks = request.Remarks.Trim();
            if (request.IsActive.HasValue) l.IsActive = request.IsActive.Value;

            l.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Map(l);
        }

        public async Task<VehicleLoanDto?> RecordEmiPaidAsync(long id)
        {
            var l = await _context.VehicleLoans.FirstOrDefaultAsync(x => x.Id == id);
            if (l == null) return null;

            if (l.TenureMonths > 0 && l.EmisPaid < l.TenureMonths)
                l.EmisPaid += 1;

            // Advance next due date by one month.
            if (l.NextDueDate.HasValue)
                l.NextDueDate = l.NextDueDate.Value.AddMonths(1);

            if (l.TenureMonths > 0 && l.EmisPaid >= l.TenureMonths)
                l.IsClosed = true;

            l.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Recorded EMI for loan {Id}: {Paid}/{Tenure}", l.Id, l.EmisPaid, l.TenureMonths);
            return Map(l);
        }

        public async Task<bool> DeleteAsync(long id)
        {
            var l = await _context.VehicleLoans.FirstOrDefaultAsync(x => x.Id == id);
            if (l == null) return false;

            l.IsActive = false;
            l.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        private static VehicleLoanDto Map(VehicleLoan l) => new()
        {
            Id = l.Id,
            TenantId = l.TenantId,
            VehicleId = l.VehicleId,
            VehicleNo = l.VehicleNo,
            Lender = l.Lender,
            LoanAccountNo = l.LoanAccountNo,
            PrincipalAmount = l.PrincipalAmount,
            EmiAmount = l.EmiAmount,
            TenureMonths = l.TenureMonths,
            EmisPaid = l.EmisPaid,
            InterestRate = l.InterestRate,
            LoanStartDate = l.LoanStartDate,
            NextDueDate = l.NextDueDate,
            IsClosed = l.IsClosed,
            Remarks = l.Remarks,
            IsActive = l.IsActive,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        };
    }
}
