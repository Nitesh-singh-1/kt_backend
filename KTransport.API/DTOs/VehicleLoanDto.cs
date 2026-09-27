using System;
using System.ComponentModel.DataAnnotations;

namespace KTransport.API.DTOs
{
    public class VehicleLoanDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string Lender { get; set; } = null!;
        public string? LoanAccountNo { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal EmiAmount { get; set; }
        public int TenureMonths { get; set; }
        public int EmisPaid { get; set; }
        public int RemainingEmis => TenureMonths > EmisPaid ? TenureMonths - EmisPaid : 0;
        public decimal OutstandingApprox => EmiAmount * RemainingEmis;
        public decimal ProgressPct => TenureMonths > 0 ? Math.Round((decimal)EmisPaid / TenureMonths * 100m, 1) : 0m;
        public decimal InterestRate { get; set; }
        public DateOnly? LoanStartDate { get; set; }
        public DateOnly? NextDueDate { get; set; }
        public bool IsClosed { get; set; }
        public string? Remarks { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateVehicleLoanRequest
    {
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }

        [Required(ErrorMessage = "Lender / financier name is required.")]
        public string Lender { get; set; } = null!;

        public string? LoanAccountNo { get; set; }
        public decimal PrincipalAmount { get; set; } = 0;
        public decimal EmiAmount { get; set; } = 0;
        public int TenureMonths { get; set; } = 0;
        public int EmisPaid { get; set; } = 0;
        public decimal InterestRate { get; set; } = 0;
        public DateOnly? LoanStartDate { get; set; }
        public DateOnly? NextDueDate { get; set; }
        public string? Remarks { get; set; }
    }

    public class UpdateVehicleLoanRequest
    {
        public long? VehicleId { get; set; }
        public string? VehicleNo { get; set; }
        public string? Lender { get; set; }
        public string? LoanAccountNo { get; set; }
        public decimal? PrincipalAmount { get; set; }
        public decimal? EmiAmount { get; set; }
        public int? TenureMonths { get; set; }
        public int? EmisPaid { get; set; }
        public decimal? InterestRate { get; set; }
        public DateOnly? LoanStartDate { get; set; }
        public DateOnly? NextDueDate { get; set; }
        public bool? IsClosed { get; set; }
        public string? Remarks { get; set; }
        public bool? IsActive { get; set; }
    }
}
