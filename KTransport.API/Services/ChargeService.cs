using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class ChargeService : IChargeService
    {
        private readonly ILogger<ChargeService> _logger;
        private readonly KTransportDbContext _context;

        public ChargeService(ILogger<ChargeService> logger, KTransportDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<ChargeResponse> CreateChargeAsync(CreateChargeRequest request)
        {
            try
            {
                _logger.LogInformation("Creating charge for Bill ID: {BillId}", request.BillId);

                // Check if bill exists
                var billExists = await _context.GstBills.AnyAsync(b => b.Id == request.BillId);
                if (!billExists)
                {
                    return new ChargeResponse
                    {
                        Success = false,
                        Message = "Bill not found"
                    };
                }

                // Check if charge already exists for this bill
                var existingCharge = await _context.Charges
                    .FirstOrDefaultAsync(c => c.BillId == request.BillId);

                if (existingCharge != null)
                {
                    return new ChargeResponse
                    {
                        Success = false,
                        Message = "Charge already exists for this bill"
                    };
                }

                var charge = new Charge
                {
                    BillId = request.BillId,
                    Freight = request.Freight ?? 0,
                    ServiceCharge = request.ServiceCharge ?? 0,
                    DdCharge = request.DdCharge ?? 0,
                    Hamali = request.Hamali ?? 0,
                    OtherCharge = request.OtherCharge ?? 0,
                    StCharge = request.StCharge ?? 0,
                    GrandTotal = request.GrandTotal,
                    CreatedAt = DateTime.Now
                };

                _context.Charges.Add(charge);
                await _context.SaveChangesAsync();

                return new ChargeResponse
                {
                    Success = true,
                    Message = "Charge created successfully",
                    Data = MapToDto(charge)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating charge");
                return new ChargeResponse
                {
                    Success = false,
                    Message = "An error occurred while creating charge"
                };
            }
        }

        public async Task<ChargeResponse> UpdateChargeAsync(int id, UpdateChargeRequest request)
        {
            try
            {
                var charge = await _context.Charges.FindAsync(id);

                if (charge == null)
                {
                    return new ChargeResponse
                    {
                        Success = false,
                        Message = "Charge not found"
                    };
                }

                charge.Freight = request.Freight ?? charge.Freight;
                charge.ServiceCharge = request.ServiceCharge ?? charge.ServiceCharge;
                charge.DdCharge = request.DdCharge ?? charge.DdCharge;
                charge.Hamali = request.Hamali ?? charge.Hamali;
                charge.OtherCharge = request.OtherCharge ?? charge.OtherCharge;
                charge.StCharge = request.StCharge ?? charge.StCharge;
                charge.GrandTotal = request.GrandTotal ?? charge.GrandTotal;

                await _context.SaveChangesAsync();

                return new ChargeResponse
                {
                    Success = true,
                    Message = "Charge updated successfully",
                    Data = MapToDto(charge)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating charge");
                return new ChargeResponse
                {
                    Success = false,
                    Message = "An error occurred while updating charge"
                };
            }
        }

        public async Task<ChargeResponse> GetChargeByIdAsync(int id)
        {
            try
            {
                var charge = await _context.Charges.FindAsync(id);

                if (charge == null)
                {
                    return new ChargeResponse
                    {
                        Success = false,
                        Message = "Charge not found"
                    };
                }

                return new ChargeResponse
                {
                    Success = true,
                    Message = "Charge retrieved successfully",
                    Data = MapToDto(charge)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charge");
                return new ChargeResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving charge"
                };
            }
        }

        public async Task<ChargeResponse> GetChargeByBillIdAsync(int billId)
        {
            try
            {
                var charge = await _context.Charges
                    .FirstOrDefaultAsync(c => c.BillId == billId);

                if (charge == null)
                {
                    return new ChargeResponse
                    {
                        Success = false,
                        Message = "Charge not found"
                    };
                }

                return new ChargeResponse
                {
                    Success = true,
                    Message = "Charge retrieved successfully",
                    Data = MapToDto(charge)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving charge");
                return new ChargeResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving charge"
                };
            }
        }

        public async Task<ChargeResponse> DeleteChargeAsync(int id)
        {
            try
            {
                var charge = await _context.Charges.FindAsync(id);

                if (charge == null)
                {
                    return new ChargeResponse
                    {
                        Success = false,
                        Message = "Charge not found"
                    };
                }

                _context.Charges.Remove(charge);
                await _context.SaveChangesAsync();

                return new ChargeResponse
                {
                    Success = true,
                    Message = "Charge deleted successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting charge");
                return new ChargeResponse
                {
                    Success = false,
                    Message = "An error occurred while deleting charge"
                };
            }
        }

        private ChargeDto MapToDto(Charge charge)
        {
            return new ChargeDto
            {
                Id = charge.Id,
                BillId = charge.BillId,
                Freight = charge.Freight,
                ServiceCharge = charge.ServiceCharge,
                DdCharge = charge.DdCharge,
                Hamali = charge.Hamali,
                OtherCharge = charge.OtherCharge,
                StCharge = charge.StCharge,
                GrandTotal = charge.GrandTotal,
                CreatedAt = charge.CreatedAt
            };
        }
    }
}