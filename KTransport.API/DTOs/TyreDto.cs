using System;
using System.ComponentModel.DataAnnotations;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class TyreDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public string SerialNo { get; set; } = null!;
        public string? Brand { get; set; }
        public string? Size { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? Position { get; set; }
        public DateOnly? PurchaseDate { get; set; }
        public decimal PurchaseCost { get; set; }
        public decimal PurchaseOdometer { get; set; }
        public decimal CurrentOdometer { get; set; }
        public decimal KmRun => CurrentOdometer > PurchaseOdometer ? CurrentOdometer - PurchaseOdometer : 0;
        public int RetreadCount { get; set; }
        public TyreStatus Status { get; set; }
        public string StatusName => Status.ToString();
        public DateOnly? DisposalDate { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateTyreRequest
    {
        [Required(ErrorMessage = "Tyre serial number is required.")]
        public string SerialNo { get; set; } = null!;
        public string? Brand { get; set; }
        public string? Size { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? Position { get; set; }
        public DateOnly? PurchaseDate { get; set; }
        public decimal PurchaseCost { get; set; } = 0;
        public decimal PurchaseOdometer { get; set; } = 0;
        public decimal CurrentOdometer { get; set; } = 0;
        public int RetreadCount { get; set; } = 0;
        public TyreStatus Status { get; set; } = TyreStatus.InStock;
        public string? Remarks { get; set; }
    }

    public class UpdateTyreRequest
    {
        public string? SerialNo { get; set; }
        public string? Brand { get; set; }
        public string? Size { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? Position { get; set; }
        public DateOnly? PurchaseDate { get; set; }
        public decimal? PurchaseCost { get; set; }
        public decimal? PurchaseOdometer { get; set; }
        public decimal? CurrentOdometer { get; set; }
        public int? RetreadCount { get; set; }
        public TyreStatus? Status { get; set; }
        public DateOnly? DisposalDate { get; set; }
        public string? Remarks { get; set; }
        public bool? IsActive { get; set; }
    }
}
