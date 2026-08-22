using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class ChallanService : IChallanService
    {
        private readonly ILogger<ChallanService> _logger;
        private readonly KTransportDbContext _context;

        public ChallanService(ILogger<ChallanService> logger, KTransportDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<ChallanResponse> CreateChallanAsync(CreateChallanRequest request, int userId)
        {
            try
            {
                _logger.LogInformation("Creating Challan: {ChallanNo}", request.ChallanNo);

                var challan = new Challan
                {
                    ChallanNo = request.ChallanNo,
                    ChallanDate = request.ChallanDate,
                    LorryNo = request.LorryNo,
                    DriverName = request.DriverName,
                    VoiceDriverName = request.VoiceDriverName,
                    FromLocation = request.FromLocation,
                    ToLocation = request.ToLocation,
                    Remarks = request.Remarks,
                    IsDeleted = false,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = userId
                };

                _context.Challans.Add(challan);
                await _context.SaveChangesAsync();

                // Add challan details
                if (request.ChallanDetails != null && request.ChallanDetails.Any())
                {
                    var challanDetails = request.ChallanDetails.Select(d => new ChallanDetail
                    {
                        ChallanId = challan.Id,
                        BillNo = d.BillNo,
                        Quantity = d.Quantity,
                        Destination = d.Destination,
                        FreightAmount = d.FreightAmount,
                        BillTypeId = d.BillTypeId,
                        ConsigneeName = d.ConsigneeName,
                        Remarks = d.Remarks,
                        IsDeleted = false,
                        CreatedDate = DateTime.UtcNow
                    }).ToList();

                    _context.ChallanDetails.AddRange(challanDetails);
                    await _context.SaveChangesAsync();
                }

                // Reload with navigation properties
                var savedChallan = await _context.Challans
                    .Include(c => c.CreatedByNavigation)
                    .Include(c => c.ChallanDetails)
                        .ThenInclude(cd => cd.BillType)
                    .FirstOrDefaultAsync(c => c.Id == challan.Id);

                return new ChallanResponse
                {
                    Success = true,
                    Message = "Challan created successfully",
                    Data = MapToDto(savedChallan!)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Challan");
                return new ChallanResponse
                {
                    Success = false,
                    Message = "An error occurred while creating Challan"
                };
            }
        }

        public async Task<ChallanResponse> UpdateChallanAsync(long id, UpdateChallanRequest request, int userId)
        {
            try
            {
                _logger.LogInformation("Updating Challan with ID: {Id}", id);

                var challan = await _context.Challans
                    .Include(c => c.ChallanDetails)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (challan == null || challan.IsDeleted)
                {
                    return new ChallanResponse
                    {
                        Success = false,
                        Message = "Challan not found"
                    };
                }

                // Update challan fields
                challan.ChallanNo = request.ChallanNo ?? challan.ChallanNo;
                challan.ChallanDate = request.ChallanDate ?? challan.ChallanDate;
                challan.LorryNo = request.LorryNo ?? challan.LorryNo;
                challan.DriverName = request.DriverName ?? challan.DriverName;
                challan.VoiceDriverName = request.VoiceDriverName ?? challan.VoiceDriverName;
                challan.FromLocation = request.FromLocation ?? challan.FromLocation;
                challan.ToLocation = request.ToLocation ?? challan.ToLocation;
                challan.Remarks = request.Remarks ?? challan.Remarks;
                challan.ModifiedDate = DateTime.UtcNow;
                challan.ModifiedBy = userId;

                // Update challan details if provided
                if (request.ChallanDetails != null)
                {
                    // Remove existing details
                    var existingDetails = challan.ChallanDetails.ToList();
                    _context.ChallanDetails.RemoveRange(existingDetails);

                    // Add new details
                    var newDetails = request.ChallanDetails.Select(d => new ChallanDetail
                    {
                        ChallanId = challan.Id,
                        BillNo = d.BillNo,
                        Quantity = d.Quantity,
                        Destination = d.Destination,
                        FreightAmount = d.FreightAmount,
                        BillTypeId = d.BillTypeId,
                        ConsigneeName = d.ConsigneeName,
                        Remarks = d.Remarks,
                        IsDeleted = false,
                        CreatedDate = DateTime.UtcNow
                    }).ToList();

                    _context.ChallanDetails.AddRange(newDetails);
                }

                await _context.SaveChangesAsync();

                // Reload with navigation properties
                var updatedChallan = await _context.Challans
                    .Include(c => c.CreatedByNavigation)
                    .Include(c => c.ModifiedByNavigation)
                    .Include(c => c.ChallanDetails)
                        .ThenInclude(cd => cd.BillType)
                    .FirstOrDefaultAsync(c => c.Id == id);

                return new ChallanResponse
                {
                    Success = true,
                    Message = "Challan updated successfully",
                    Data = MapToDto(updatedChallan!)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Challan");
                return new ChallanResponse
                {
                    Success = false,
                    Message = "An error occurred while updating Challan"
                };
            }
        }

        public async Task<ChallanResponse> GetChallanByIdAsync(long id)
        {
            try
            {
                var challan = await _context.Challans
                    .Include(c => c.CreatedByNavigation)
                    .Include(c => c.ModifiedByNavigation)
                    .Include(c => c.ChallanDetails)
                        .ThenInclude(cd => cd.BillType)
                    .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

                if (challan == null)
                {
                    return new ChallanResponse
                    {
                        Success = false,
                        Message = "Challan not found"
                    };
                }

                return new ChallanResponse
                {
                    Success = true,
                    Message = "Challan retrieved successfully",
                    Data = MapToDto(challan)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Challan");
                return new ChallanResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving Challan"
                };
            }
        }

        public async Task<ChallanListResponse> GetAllChallansAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var totalCount = await _context.Challans.CountAsync(c => !c.IsDeleted);

                var challans = await _context.Challans
                    .Include(c => c.CreatedByNavigation)
                    .Include(c => c.ModifiedByNavigation)
                    .Include(c => c.ChallanDetails)
                        .ThenInclude(cd => cd.BillType)
                    .Where(c => !c.IsDeleted)
                    .OrderByDescending(c => c.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new ChallanListResponse
                {
                    Success = true,
                    Message = "Challans retrieved successfully",
                    Data = challans.Select(MapToDto).ToList(),
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Challans");
                return new ChallanListResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving Challans"
                };
            }
        }

        public async Task<ChallanResponse> DeleteChallanAsync(long id, int userId)
        {
            try
            {
                var challan = await _context.Challans.FindAsync(id);

                if (challan == null || challan.IsDeleted)
                {
                    return new ChallanResponse
                    {
                        Success = false,
                        Message = "Challan not found"
                    };
                }

                // Soft delete
                challan.IsDeleted = true;
                challan.ModifiedDate = DateTime.UtcNow;
                challan.ModifiedBy = userId;

                await _context.SaveChangesAsync();

                return new ChallanResponse
                {
                    Success = true,
                    Message = "Challan deleted successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Challan");
                return new ChallanResponse
                {
                    Success = false,
                    Message = "An error occurred while deleting Challan"
                };
            }
        }

        public async Task<ChallanListResponse> SearchChallansAsync(string? searchTerm, DateOnly? startDate, DateOnly? endDate, int page = 1, int pageSize = 10)
        {
            try
            {
                var query = _context.Challans
                    .Include(c => c.CreatedByNavigation)
                    .Include(c => c.ModifiedByNavigation)
                    .Include(c => c.ChallanDetails)
                        .ThenInclude(cd => cd.BillType)
                    .Where(c => !c.IsDeleted);

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(c =>
                        (c.ChallanNo != null && c.ChallanNo.Contains(searchTerm)) ||
                        (c.LorryNo != null && c.LorryNo.Contains(searchTerm)) ||
                        (c.DriverName != null && c.DriverName.Contains(searchTerm)));
                }

                if (startDate.HasValue)
                {
                    query = query.Where(c => c.ChallanDate >= startDate.Value);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(c => c.ChallanDate <= endDate.Value);
                }

                var totalCount = await query.CountAsync();

                var challans = await query
                    .OrderByDescending(c => c.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new ChallanListResponse
                {
                    Success = true,
                    Message = "Challans retrieved successfully",
                    Data = challans.Select(MapToDto).ToList(),
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Challans");
                return new ChallanListResponse
                {
                    Success = false,
                    Message = "An error occurred while searching Challans"
                };
            }
        }

        private ChallanDto MapToDto(Challan challan)
        {
            return new ChallanDto
            {
                Id = challan.Id,
                ChallanNo = challan.ChallanNo,
                ChallanDate = challan.ChallanDate,
                LorryNo = challan.LorryNo,
                DriverName = challan.DriverName,
                VoiceDriverName = challan.VoiceDriverName,
                FromLocation = challan.FromLocation,
                ToLocation = challan.ToLocation,
                Remarks = challan.Remarks,
                IsDeleted = challan.IsDeleted,
                CreatedDate = challan.CreatedDate,
                CreatedBy = challan.CreatedBy,
                CreatedByName = challan.CreatedByNavigation?.FullName,
                ModifiedDate = challan.ModifiedDate,
                ModifiedBy = challan.ModifiedBy,
                ModifiedByName = challan.ModifiedByNavigation?.FullName,
                ChallanDetails = challan.ChallanDetails?.Where(d => !d.IsDeleted).Select(d => new ChallanDetailDto
                {
                    Id = d.Id,
                    ChallanId = d.ChallanId,
                    BillNo = d.BillNo,
                    Quantity = d.Quantity,
                    Destination = d.Destination,
                    FreightAmount = d.FreightAmount,
                    BillTypeId = d.BillTypeId,
                    BillTypeName = d.BillType?.Name,
                    ConsigneeName = d.ConsigneeName,
                    Remarks = d.Remarks,
                    IsDeleted = d.IsDeleted,
                    CreatedDate = d.CreatedDate
                }).ToList() ?? new List<ChallanDetailDto>()
            };
        }
    }
}