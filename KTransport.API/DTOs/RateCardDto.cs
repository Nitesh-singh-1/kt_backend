using System;
using KTransport.API.Models;

namespace KTransport.API.DTOs
{
    public class FreightRateCardDto
    {
        public long Id { get; set; }
        public Guid TenantId { get; set; }
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public string FromLocation { get; set; } = null!;
        public string ToLocation { get; set; } = null!;
        public string? CommodityType { get; set; }
        public RateType RateType { get; set; }
        public string RateTypeName => RateType.ToString();
        public decimal BaseRate { get; set; }
        public decimal MinFreightAmount { get; set; }
        public decimal HamaliRatePerKg { get; set; }
        public decimal DoorDeliveryCharge { get; set; }
        public decimal StationaryCharge { get; set; }
        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateRateCardRequest
    {
        public long? PartyId { get; set; }
        public string? PartyName { get; set; }
        public string FromLocation { get; set; } = null!;
        public string ToLocation { get; set; } = null!;
        public string? CommodityType { get; set; }
        public RateType RateType { get; set; } = RateType.PerKg;
        public decimal BaseRate { get; set; }
        public decimal MinFreightAmount { get; set; } = 0;
        public decimal HamaliRatePerKg { get; set; } = 0;
        public decimal DoorDeliveryCharge { get; set; } = 0;
        public decimal StationaryCharge { get; set; } = 0;
        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
    }

    public class CalculateFreightRequest
    {
        public long? PartyId { get; set; }
        public string FromLocation { get; set; } = null!;
        public string ToLocation { get; set; } = null!;
        public decimal WeightKg { get; set; } = 0;
        public int PackagesCount { get; set; } = 1;
        public bool IncludeDoorDelivery { get; set; } = false;
        public bool IncludeHamali { get; set; } = true;
    }

    public class CalculatedFreightResponse
    {
        public bool MatchedRateCard { get; set; }
        public long? RateCardId { get; set; }
        public decimal BaseRate { get; set; }
        public RateType RateType { get; set; }
        public decimal FreightAmount { get; set; }
        public decimal HamaliAmount { get; set; }
        public decimal DoorDeliveryAmount { get; set; }
        public decimal StationaryAmount { get; set; }
        public decimal TotalEstimatedFreight { get; set; }
        public string? CalculationBreakdown { get; set; }
    }
}
