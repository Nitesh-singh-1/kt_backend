using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class GoodsDetailService : IGoodsDetailService
    {
        private readonly ILogger<GoodsDetailService> _logger;
        private readonly KTransportDbContext _context;

        public GoodsDetailService(ILogger<GoodsDetailService> logger, KTransportDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<GoodsDetailResponse> CreateGoodsDetailAsync(CreateGoodsDetailRequest request)
        {
            try
            {
                _logger.LogInformation("Creating goods detail for Bill ID: {BillId}", request.BillId);

                // Check if bill exists
                var billExists = await _context.GstBills.AnyAsync(b => b.Id == request.BillId);
                if (!billExists)
                {
                    return new GoodsDetailResponse
                    {
                        Success = false,
                        Message = "Bill not found"
                    };
                }

                var goodsDetail = new GoodsDetail
                {
                    BillId = request.BillId,
                    Article = request.Article,
                    Description = request.Description,
                    Weight = request.Weight,
                    Rate = request.Rate,
                    CreatedAt = DateTime.Now
                };

                _context.GoodsDetails.Add(goodsDetail);
                await _context.SaveChangesAsync();

                return new GoodsDetailResponse
                {
                    Success = true,
                    Message = "Goods detail created successfully",
                    Data = MapToDto(goodsDetail)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating goods detail");
                return new GoodsDetailResponse
                {
                    Success = false,
                    Message = "An error occurred while creating goods detail"
                };
            }
        }

        public async Task<GoodsDetailResponse> UpdateGoodsDetailAsync(int id, UpdateGoodsDetailRequest request)
        {
            try
            {
                var goodsDetail = await _context.GoodsDetails.FindAsync(id);

                if (goodsDetail == null)
                {
                    return new GoodsDetailResponse
                    {
                        Success = false,
                        Message = "Goods detail not found"
                    };
                }

                goodsDetail.Article = request.Article ?? goodsDetail.Article;
                goodsDetail.Description = request.Description ?? goodsDetail.Description;
                goodsDetail.Weight = request.Weight ?? goodsDetail.Weight;
                goodsDetail.Rate = request.Rate ?? goodsDetail.Rate;

                await _context.SaveChangesAsync();

                return new GoodsDetailResponse
                {
                    Success = true,
                    Message = "Goods detail updated successfully",
                    Data = MapToDto(goodsDetail)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating goods detail");
                return new GoodsDetailResponse
                {
                    Success = false,
                    Message = "An error occurred while updating goods detail"
                };
            }
        }

        public async Task<GoodsDetailResponse> GetGoodsDetailByIdAsync(int id)
        {
            try
            {
                var goodsDetail = await _context.GoodsDetails.FindAsync(id);

                if (goodsDetail == null)
                {
                    return new GoodsDetailResponse
                    {
                        Success = false,
                        Message = "Goods detail not found"
                    };
                }

                return new GoodsDetailResponse
                {
                    Success = true,
                    Message = "Goods detail retrieved successfully",
                    Data = MapToDto(goodsDetail)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving goods detail");
                return new GoodsDetailResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving goods detail"
                };
            }
        }

        public async Task<GoodsDetailListResponse> GetGoodsDetailsByBillIdAsync(int billId)
        {
            try
            {
                var goodsDetails = await _context.GoodsDetails
                    .Where(g => g.BillId == billId)
                    .ToListAsync();

                return new GoodsDetailListResponse
                {
                    Success = true,
                    Message = "Goods details retrieved successfully",
                    Data = goodsDetails.Select(MapToDto).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving goods details");
                return new GoodsDetailListResponse
                {
                    Success = false,
                    Message = "An error occurred while retrieving goods details"
                };
            }
        }

        public async Task<GoodsDetailResponse> DeleteGoodsDetailAsync(int id)
        {
            try
            {
                var goodsDetail = await _context.GoodsDetails.FindAsync(id);

                if (goodsDetail == null)
                {
                    return new GoodsDetailResponse
                    {
                        Success = false,
                        Message = "Goods detail not found"
                    };
                }

                _context.GoodsDetails.Remove(goodsDetail);
                await _context.SaveChangesAsync();

                return new GoodsDetailResponse
                {
                    Success = true,
                    Message = "Goods detail deleted successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting goods detail");
                return new GoodsDetailResponse
                {
                    Success = false,
                    Message = "An error occurred while deleting goods detail"
                };
            }
        }

        private GoodsDetailDto MapToDto(GoodsDetail goodsDetail)
        {
            return new GoodsDetailDto
            {
                Id = goodsDetail.Id,
                BillId = goodsDetail.BillId,
                Article = goodsDetail.Article,
                Description = goodsDetail.Description,
                Weight = goodsDetail.Weight,
                Rate = goodsDetail.Rate,
                CreatedAt = goodsDetail.CreatedAt
            };
        }
    }
}