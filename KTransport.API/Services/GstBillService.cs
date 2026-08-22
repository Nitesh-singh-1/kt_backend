using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class GstBillService : IGstBillService
    {
        private readonly ILogger<GstBillService> _logger;
        private readonly KTransportDbContext _context;

        public GstBillService(ILogger<GstBillService> logger, KTransportDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<GstBillResponse> CreateGstBillAsync(CreateGstBillRequest request, int userId)
        {
            try
            {
                _logger.LogInformation("Creating GST Bill with GR No: {GrNo}", request.GrNo);

                // Check if GR No already exists
                var existingBill = await _context.GstBills
                    .FirstOrDefaultAsync(b => b.GrNo == request.GrNo);

                if (existingBill != null)
                {
                    return new GstBillResponse
                    {
                        Success = false,
                        Message = "GR Number already exists"
                    };
                }

                var gstBill = new GstBill
                {
                    GrNo = request.GrNo,
                    InvoiceNo = request.InvoiceNo,
                    FromLocation = request.FromLocation,
                    ToLocation = request.ToLocation,
                    GrDate = request.GrDate,
                    InvoiceDate = request.InvoiceDate,
                    GoodsValue = request.GoodsValue,
                    GstPaidBy = request.GstPaidBy,
                    ConsignerName = request.ConsignerName,
                    ConsignerGstNo = request.ConsignerGstNo,
                    ConsignerMobile = request.ConsignerMobile,
                    ConsigneeName = request.ConsigneeName,
                    ConsigneeGstNo = request.ConsigneeGstNo,
                    ConsigneeMobile = request.ConsigneeMobile,
                    ConsigneeAddress = request.ConsigneeAddress,
                    TruckNo = request.TruckNo,
                    DeliveryStatus = request.DeliveryStatus,
                    Remarks = request.Remarks,
                    Paid = request.Paid,
                    Tbb = request.Tbb,
                    ToPay = request.ToPay,
                    TotalAmount = request.TotalAmount,
                    BookingClerk = request.BookingClerk,
                    CreatedBy = userId,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.GstBills.Add(gstBill);
                await _context.SaveChangesAsync();

                // Reload with navigation properties
                var savedBill = await _context.GstBills
                    .Include(b => b.CreatedByNavigation)
                    .Include(b => b.Charge)
                    .Include(b => b.GoodsDetails)
                    .FirstOrDefaultAsync(b => b.Id == gstBill.Id);

                return new GstBillResponse
                {
                    Success = true,
                    Message = "GST Bill created successfully",
                    Data = MapToDto(savedBill!)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating GST Bill");
                return new GstBillResponse
                {
                    Success = false,
                    Message = "An error occurred while creating GST Bill"
                };
            }
        }

        public async Task<GstBillResponse> UpdateGstBillAsync(int id, UpdateGstBillRequest request, int userId)
        {
            try
            {
                _logger.LogInformation("Updating GST Bill with ID: {Id}", id);

                var gstBill = await _context.GstBills.FindAsync(id);

                if (gstBill == null)
                {
                    return new GstBillResponse
                    {
                        Success = false,
                        Message = "GST Bill not found"
                    };
                }

                // Update fields
                gstBill.InvoiceNo = request.InvoiceNo ?? gstBill.InvoiceNo;
                gstBill.FromLocation = request.FromLocation ?? gstBill.FromLocation;
                gstBill.ToLocation = request.ToLocation ?? gstBill.ToLocation;
                gstBill.GrDate = request.GrDate ?? gstBill.GrDate;
                gstBill.InvoiceDate = request.InvoiceDate ?? gstBill.InvoiceDate;
                gstBill.GoodsValue = request.GoodsValue ?? gstBill.GoodsValue;
                gstBill.GstPaidBy = request.GstPaidBy ?? gstBill.GstPaidBy;
                gstBill.ConsignerName = request.ConsignerName ?? gstBill.ConsignerName;
                gstBill.ConsignerGstNo = request.ConsignerGstNo ?? gstBill.ConsignerGstNo;
                gstBill.ConsignerMobile = request.ConsignerMobile ?? gstBill.ConsignerMobile;
                gstBill.ConsigneeName = request.ConsigneeName ?? gstBill.ConsigneeName;
                gstBill.ConsigneeGstNo = request.ConsigneeGstNo ?? gstBill.ConsigneeGstNo;
                gstBill.ConsigneeMobile = request.ConsigneeMobile ?? gstBill.ConsigneeMobile;
                gstBill.ConsigneeAddress = request.ConsigneeAddress ?? gstBill.ConsigneeAddress;
                gstBill.TruckNo = request.TruckNo ?? gstBill.TruckNo;
                gstBill.DeliveryStatus = request.DeliveryStatus ?? gstBill.DeliveryStatus;
                gstBill.Remarks = request.Remarks ?? gstBill.Remarks;
                gstBill.Paid = request.Paid ?? gstBill.Paid;
                gstBill.Tbb = request.Tbb ?? gstBill.Tbb;
                gstBill.ToPay = request.ToPay ?? gstBill.ToPay;
                gstBill.TotalAmount = request.TotalAmount ?? gstBill.TotalAmount;
                gstBill.BookingClerk = request.BookingClerk ?? gstBill.BookingClerk;
                gstBill.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                // Reload with navigation properties
                var updatedBill = await _context.GstBills
                    .Include(b => b.CreatedByNavigation)
                    .Include(b => b.UpdatedByNavigation)
                    .Include(b => b.Charge)
                    .Include(b => b.GoodsDetails)
                    .FirstOrDefaultAsync(b => b.Id == id);

                return new GstBillResponse
                {
                    Success = true,
                    Message = "GST Bill updated successfully",
                    Data = MapToDto(updatedBill!)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating GST Bill");
                return new GstBillResponse
                {
                    Success = false,
                    Message = "An error occurred while updating GST Bill"
                };
            }
        }

        public async Task<GstBillResponse> GetGstBillByIdAsync(int id)
        {
            try
            {
                var gstBill = await _context.GstBills
                    .Include(b => b.CreatedByNavigation)
                    .Include(b => b.UpdatedByNavigation)
                    .Include(b => b.Charge)
                    .Include(b => b.GoodsDetails)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (gstBill == null)
                {
                    return new GstBillResponse
                    {
                        Success = false,
                        Message = "GST Bill not found"
                    };
                }

                return new GstBillResponse
                {
                    Success = true,
                    Message = "GST Bill retrieved successfully",
                    Data = MapToDto(gstBill)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving GST Bill");
                return new GstBillResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving GST Bill"
                };
            }
        }

        public async Task<GstBillResponse> GetGstBillByGrNoAsync(string grNo)
        {
            try
            {
                var gstBill = await _context.GstBills
                    .Include(b => b.CreatedByNavigation)
                    .Include(b => b.UpdatedByNavigation)
                    .Include(b => b.Charge)
                    .Include(b => b.GoodsDetails)
                    .FirstOrDefaultAsync(b => b.GrNo == grNo);

                if (gstBill == null)
                {
                    return new GstBillResponse
                    {
                        Success = false,
                        Message = "GST Bill not found"
                    };
                }

                return new GstBillResponse
                {
                    Success = true,
                    Message = "GST Bill retrieved successfully",
                    Data = MapToDto(gstBill)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving GST Bill");
                return new GstBillResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving GST Bill"
                };
            }
        }

        public async Task<GstBillListResponse> GetAllGstBillsAsync(int page = 1, int pageSize = 10)
        {
            try
            {
                var totalCount = await _context.GstBills.CountAsync(b => b.IsActive == true);

                var gstBills = await _context.GstBills
                    .Include(b => b.CreatedByNavigation)
                    .Include(b => b.UpdatedByNavigation)
                    .Include(b => b.Charge)
                    .Include(b => b.GoodsDetails)
                    .Where(b => b.IsActive == true)
                    .OrderByDescending(b => b.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                return new GstBillListResponse
                {
                    Success = true,
                    Message = "GST Bills retrieved successfully",
                    Data = gstBills.Select(MapToDto).ToList(),
                    TotalCount = totalCount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving GST Bills");
                return new GstBillListResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving GST Bills"
                };
            }
        }

        public async Task<GstBillResponse> DeleteGstBillAsync(int id, int userId)
        {
            try
            {
                var gstBill = await _context.GstBills.FindAsync(id);

                if (gstBill == null)
                {
                    return new GstBillResponse
                    {
                        Success = false,
                        Message = "GST Bill not found"
                    };
                }

                // Soft delete
                gstBill.IsActive = false;
                gstBill.UpdatedBy = userId;

                await _context.SaveChangesAsync();

                return new GstBillResponse
                {
                    Success = true,
                    Message = "GST Bill deleted successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting GST Bill");
                return new GstBillResponse
                {
                    Success = false,
                    Message = "An error occurred while deleting GST Bill"
                };
            }
        }

        private GstBillDto MapToDto(GstBill bill)
        {
            return new GstBillDto
            {
                Id = bill.Id,
                GrNo = bill.GrNo,
                InvoiceNo = bill.InvoiceNo,
                FromLocation = bill.FromLocation,
                ToLocation = bill.ToLocation,
                GrDate = bill.GrDate,
                InvoiceDate = bill.InvoiceDate,
                GoodsValue = bill.GoodsValue,
                GstPaidBy = bill.GstPaidBy,
                ConsignerName = bill.ConsignerName,
                ConsignerGstNo = bill.ConsignerGstNo,
                ConsignerMobile = bill.ConsignerMobile,
                ConsigneeName = bill.ConsigneeName,
                ConsigneeGstNo = bill.ConsigneeGstNo,
                ConsigneeMobile = bill.ConsigneeMobile,
                ConsigneeAddress = bill.ConsigneeAddress,
                TruckNo = bill.TruckNo,
                DeliveryStatus = bill.DeliveryStatus,
                Remarks = bill.Remarks,
                Paid = bill.Paid,
                Tbb = bill.Tbb,
                ToPay = bill.ToPay,
                TotalAmount = bill.TotalAmount,
                BookingClerk = bill.BookingClerk,
                CreatedBy = bill.CreatedBy,
                CreatedByName = bill.CreatedByNavigation?.FullName,
                UpdatedBy = bill.UpdatedBy,
                UpdatedByName = bill.UpdatedByNavigation?.FullName,
                IsActive = bill.IsActive,
                CreatedAt = bill.CreatedAt,
                
                // Map related data
                Charge = bill.Charge != null ? new ChargeDto
                {
                    Id = bill.Charge.Id,
                    BillId = bill.Charge.BillId,
                    Freight = bill.Charge.Freight,
                    ServiceCharge = bill.Charge.ServiceCharge,
                    DdCharge = bill.Charge.DdCharge,
                    Hamali = bill.Charge.Hamali,
                    OtherCharge = bill.Charge.OtherCharge,
                    StCharge = bill.Charge.StCharge,
                    GrandTotal = bill.Charge.GrandTotal,
                    CreatedAt = bill.Charge.CreatedAt
                } : null,
                
                GoodsDetails = bill.GoodsDetails?.Select(g => new GoodsDetailDto
                {
                    Id = g.Id,
                    BillId = g.BillId,
                    Article = g.Article,
                    Description = g.Description,
                    Weight = g.Weight,
                    Rate = g.Rate,
                    CreatedAt = g.CreatedAt
                }).ToList() ?? new List<GoodsDetailDto>()
            };
        }
    }
}