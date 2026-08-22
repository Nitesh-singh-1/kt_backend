using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class CreateChallanRequest
    {
        public string? ChallanNo { get; set; }
        
        [Required]
        public DateOnly ChallanDate { get; set; }
        
        public string? LorryNo { get; set; }
        public string? DriverName { get; set; }
        public string? VoiceDriverName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? Remarks { get; set; }
        
        public List<CreateChallanDetailRequest> ChallanDetails { get; set; } = new();
    }

    public class CreateChallanDetailRequest
    {
        public string? BillNo { get; set; }
        public int? Quantity { get; set; }
        public string? Destination { get; set; }
        public decimal? FreightAmount { get; set; }
        public int? BillTypeId { get; set; }
        public string? ConsigneeName { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateChallanRequest
    {
        public string? ChallanNo { get; set; }
        public DateOnly? ChallanDate { get; set; }
        public string? LorryNo { get; set; }
        public string? DriverName { get; set; }
        public string? VoiceDriverName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? Remarks { get; set; }
        
        public List<UpdateChallanDetailRequest>? ChallanDetails { get; set; }
    }

    public class UpdateChallanDetailRequest
    {
        public long? Id { get; set; }
        public string? BillNo { get; set; }
        public int? Quantity { get; set; }
        public string? Destination { get; set; }
        public decimal? FreightAmount { get; set; }
        public int? BillTypeId { get; set; }
        public string? ConsigneeName { get; set; }
        public string? Remarks { get; set; }
    }

    public class ChallanResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ChallanDto? Data { get; set; }
    }

    public class ChallanListResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<ChallanDto> Data { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class ChallanDto
    {
        public long Id { get; set; }
        public string? ChallanNo { get; set; }
        public DateOnly ChallanDate { get; set; }
        public string? LorryNo { get; set; }
        public string? DriverName { get; set; }
        public string? VoiceDriverName { get; set; }
        public string? FromLocation { get; set; }
        public string? ToLocation { get; set; }
        public string? Remarks { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public string? ModifiedByName { get; set; }
        
        public List<ChallanDetailDto> ChallanDetails { get; set; } = new();
    }

    public class ChallanDetailDto
    {
        public long Id { get; set; }
        public long ChallanId { get; set; }
        public string? BillNo { get; set; }
        public int? Quantity { get; set; }
        public string? Destination { get; set; }
        public decimal? FreightAmount { get; set; }
        public int? BillTypeId { get; set; }
        public string? BillTypeName { get; set; }
        public string? ConsigneeName { get; set; }
        public string? Remarks { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}