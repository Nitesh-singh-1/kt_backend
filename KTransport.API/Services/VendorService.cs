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
    public class VendorService : IVendorService
    {
        private readonly KTransportDbContext _context;
        private readonly INumberingSequenceService _numberingService;
        private readonly ILogger<VendorService> _logger;

        public VendorService(
            KTransportDbContext context,
            INumberingSequenceService numberingService,
            ILogger<VendorService> logger)
        {
            _context = context;
            _numberingService = numberingService;
            _logger = logger;
        }

        // Vendors
        public async Task<List<VendorDto>> GetVendorsAsync(string? search = null)
        {
            var query = _context.Vendors.AsNoTracking().Where(v => v.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(v =>
                    v.Name.ToLower().Contains(s) ||
                    (v.Code != null && v.Code.ToLower().Contains(s)) ||
                    (v.Mobile != null && v.Mobile.Contains(s)) ||
                    (v.PanNo != null && v.PanNo.ToLower().Contains(s))
                );
            }

            var vendors = await query.OrderBy(v => v.Name).ToListAsync();
            return vendors.Select(v => new VendorDto
            {
                Id = v.Id,
                TenantId = v.TenantId,
                Name = v.Name,
                Code = v.Code,
                PanNo = v.PanNo,
                GstNo = v.GstNo,
                ContactPerson = v.ContactPerson,
                Mobile = v.Mobile,
                Phone = v.Phone,
                Email = v.Email,
                Address = v.Address,
                City = v.City,
                State = v.State,
                TdsPercentage = v.TdsPercentage,
                BankName = v.BankName,
                AccountNumber = v.AccountNumber,
                IfscCode = v.IfscCode,
                AccountHolderName = v.AccountHolderName,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            }).ToList();
        }

        public async Task<List<VendorLookupDto>> GetVendorLookupAsync(string? query = null)
        {
            var dbQuery = _context.Vendors.AsNoTracking().Where(v => v.IsActive);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim().ToLower();
                dbQuery = dbQuery.Where(v =>
                    v.Name.ToLower().Contains(s) ||
                    (v.Mobile != null && v.Mobile.Contains(s))
                );
            }

            return await dbQuery
                .OrderBy(v => v.Name)
                .Take(50)
                .Select(v => new VendorLookupDto
                {
                    Id = v.Id,
                    Name = v.Name,
                    Code = v.Code,
                    PanNo = v.PanNo,
                    Mobile = v.Mobile,
                    TdsPercentage = v.TdsPercentage
                })
                .ToListAsync();
        }

        public async Task<VendorDto?> GetVendorByIdAsync(long id)
        {
            var v = await _context.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (v == null) return null;

            return new VendorDto
            {
                Id = v.Id,
                TenantId = v.TenantId,
                Name = v.Name,
                Code = v.Code,
                PanNo = v.PanNo,
                GstNo = v.GstNo,
                ContactPerson = v.ContactPerson,
                Mobile = v.Mobile,
                Phone = v.Phone,
                Email = v.Email,
                Address = v.Address,
                City = v.City,
                State = v.State,
                TdsPercentage = v.TdsPercentage,
                BankName = v.BankName,
                AccountNumber = v.AccountNumber,
                IfscCode = v.IfscCode,
                AccountHolderName = v.AccountHolderName,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            };
        }

        public async Task<VendorDto> CreateVendorAsync(CreateVendorRequest request)
        {
            var vendor = new Vendor
            {
                Name = request.Name.Trim(),
                Code = request.Code?.Trim(),
                PanNo = request.PanNo?.Trim(),
                GstNo = request.GstNo?.Trim(),
                ContactPerson = request.ContactPerson?.Trim(),
                Mobile = request.Mobile?.Trim(),
                Phone = request.Phone?.Trim(),
                Email = request.Email?.Trim(),
                Address = request.Address?.Trim(),
                City = request.City?.Trim(),
                State = request.State?.Trim(),
                TdsPercentage = request.TdsPercentage,
                BankName = request.BankName?.Trim(),
                AccountNumber = request.AccountNumber?.Trim(),
                IfscCode = request.IfscCode?.Trim(),
                AccountHolderName = request.AccountHolderName?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created Vendor {Name} (ID: {Id})", vendor.Name, vendor.Id);
            return (await GetVendorByIdAsync(vendor.Id))!;
        }

        public async Task<VendorDto?> UpdateVendorAsync(long id, CreateVendorRequest request)
        {
            var v = await _context.Vendors.FirstOrDefaultAsync(x => x.Id == id);
            if (v == null) return null;

            v.Name = request.Name.Trim();
            v.Code = request.Code?.Trim();
            v.PanNo = request.PanNo?.Trim();
            v.GstNo = request.GstNo?.Trim();
            v.ContactPerson = request.ContactPerson?.Trim();
            v.Mobile = request.Mobile?.Trim();
            v.Phone = request.Phone?.Trim();
            v.Email = request.Email?.Trim();
            v.Address = request.Address?.Trim();
            v.City = request.City?.Trim();
            v.State = request.State?.Trim();
            v.TdsPercentage = request.TdsPercentage;
            v.BankName = request.BankName?.Trim();
            v.AccountNumber = request.AccountNumber?.Trim();
            v.IfscCode = request.IfscCode?.Trim();
            v.AccountHolderName = request.AccountHolderName?.Trim();
            v.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (await GetVendorByIdAsync(v.Id))!;
        }

        public async Task<bool> DeleteVendorAsync(long id)
        {
            var v = await _context.Vendors.FirstOrDefaultAsync(x => x.Id == id);
            if (v == null) return false;

            v.IsActive = false;
            v.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // Lorry Hire Contracts
        public async Task<List<LorryHireContractDto>> GetLorryHireContractsAsync(string? search = null, long? vendorId = null)
        {
            var query = _context.LorryHireContracts.AsNoTracking().Where(c => c.IsActive);

            if (vendorId.HasValue)
            {
                query = query.Where(c => c.VendorId == vendorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(c =>
                    c.ContractNo.ToLower().Contains(s) ||
                    (c.VendorName != null && c.VendorName.ToLower().Contains(s)) ||
                    (c.VehicleNo != null && c.VehicleNo.ToLower().Contains(s))
                );
            }

            var contracts = await query.OrderByDescending(c => c.ContractDate).ThenByDescending(c => c.Id).ToListAsync();
            return contracts.Select(c => new LorryHireContractDto
            {
                Id = c.Id,
                TenantId = c.TenantId,
                ContractNo = c.ContractNo,
                ContractDate = c.ContractDate,
                TripId = c.TripId,
                VendorId = c.VendorId,
                VendorName = c.VendorName,
                VehicleNo = c.VehicleNo,
                DriverName = c.DriverName,
                DriverMobile = c.DriverMobile,
                FromLocation = c.FromLocation,
                ToLocation = c.ToLocation,
                TotalHireAmount = c.TotalHireAmount,
                AdvanceCashPaid = c.AdvanceCashPaid,
                DieselAdvanceAmount = c.DieselAdvanceAmount,
                TdsAmount = c.TdsAmount,
                OtherDeductions = c.OtherDeductions,
                BalancePayable = c.BalancePayable,
                PaidBalanceAmount = c.PaidBalanceAmount,
                PaymentStatus = c.PaymentStatus,
                PaymentReference = c.PaymentReference,
                Remarks = c.Remarks,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            }).ToList();
        }

        public async Task<LorryHireContractDto?> GetLorryHireContractByIdAsync(long id)
        {
            var c = await _context.LorryHireContracts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return null;

            return new LorryHireContractDto
            {
                Id = c.Id,
                TenantId = c.TenantId,
                ContractNo = c.ContractNo,
                ContractDate = c.ContractDate,
                TripId = c.TripId,
                VendorId = c.VendorId,
                VendorName = c.VendorName,
                VehicleNo = c.VehicleNo,
                DriverName = c.DriverName,
                DriverMobile = c.DriverMobile,
                FromLocation = c.FromLocation,
                ToLocation = c.ToLocation,
                TotalHireAmount = c.TotalHireAmount,
                AdvanceCashPaid = c.AdvanceCashPaid,
                DieselAdvanceAmount = c.DieselAdvanceAmount,
                TdsAmount = c.TdsAmount,
                OtherDeductions = c.OtherDeductions,
                BalancePayable = c.BalancePayable,
                PaidBalanceAmount = c.PaidBalanceAmount,
                PaymentStatus = c.PaymentStatus,
                PaymentReference = c.PaymentReference,
                Remarks = c.Remarks,
                IsActive = c.IsActive,
                CreatedAt = c.CreatedAt
            };
        }

        public async Task<LorryHireContractDto> CreateLorryHireContractAsync(CreateLorryHireRequest request, int? userId = null)
        {
            string contractNo = request.ContractNo?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(contractNo))
            {
                contractNo = await _numberingService.GetNextNumberAsync("LORRY_HIRE", "LHS");
            }

            string? vendorName = request.VendorName;
            decimal tdsRate = 1.0m;

            if (request.VendorId.HasValue)
            {
                var v = await _context.Vendors.FindAsync(request.VendorId.Value);
                if (v != null)
                {
                    vendorName ??= v.Name;
                    tdsRate = v.TdsPercentage;
                }
            }

            decimal tdsAmount = request.TdsAmount > 0 ? request.TdsAmount : (request.TotalHireAmount * tdsRate / 100m);
            decimal balance = request.TotalHireAmount - (request.AdvanceCashPaid + request.DieselAdvanceAmount + tdsAmount + request.OtherDeductions);
            if (balance < 0) balance = 0;

            var contract = new LorryHireContract
            {
                ContractNo = contractNo,
                ContractDate = request.ContractDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                TripId = request.TripId,
                VendorId = request.VendorId,
                VendorName = vendorName,
                VehicleNo = request.VehicleNo,
                DriverName = request.DriverName,
                DriverMobile = request.DriverMobile,
                FromLocation = request.FromLocation,
                ToLocation = request.ToLocation,
                TotalHireAmount = request.TotalHireAmount,
                AdvanceCashPaid = request.AdvanceCashPaid,
                DieselAdvanceAmount = request.DieselAdvanceAmount,
                TdsAmount = tdsAmount,
                OtherDeductions = request.OtherDeductions,
                BalancePayable = balance,
                PaidBalanceAmount = 0,
                PaymentStatus = balance == 0 ? "Paid" : "Unpaid",
                Remarks = request.Remarks,
                CreatedBy = userId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.LorryHireContracts.Add(contract);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created Lorry Hire Memo {ContractNo}", contract.ContractNo);
            return (await GetLorryHireContractByIdAsync(contract.Id))!;
        }

        public async Task<LorryHireContractDto?> RecordLorryHirePaymentAsync(long contractId, RecordLorryHirePaymentRequest request, int? userId = null)
        {
            var c = await _context.LorryHireContracts.FirstOrDefaultAsync(x => x.Id == contractId);
            if (c == null) return null;

            c.PaidBalanceAmount += request.Amount;
            if (c.PaidBalanceAmount >= c.BalancePayable)
            {
                c.PaymentStatus = "Paid";
            }
            else if (c.PaidBalanceAmount > 0)
            {
                c.PaymentStatus = "PartiallyPaid";
            }

            c.PaymentReference = request.PaymentReference ?? c.PaymentReference;
            c.Remarks = $"{c.Remarks} | Payment: {request.Amount} ({request.Remarks})".Trim();
            c.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return (await GetLorryHireContractByIdAsync(c.Id))!;
        }

        public async Task<bool> DeleteLorryHireContractAsync(long id)
        {
            var c = await _context.LorryHireContracts.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return false;

            c.IsActive = false;
            c.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
