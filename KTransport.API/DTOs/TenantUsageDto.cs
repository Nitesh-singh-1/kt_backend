using System;
using System.Collections.Generic;

namespace KTransport.API.DTOs
{
    /// <summary>
    /// Tenant-facing snapshot of "how much of my plan am I using?" — driven by
    /// TASK-007 slice 1. Read-only, safe for any authenticated tenant user; the
    /// backend derives the numbers, so the frontend doesn't decide when to
    /// warn.
    /// </summary>
    public class TenantUsageDto
    {
        public string PlanTier { get; set; } = "Starter";
        public string PlanStatus { get; set; } = "Active";
        public DateTime? ExpiresAt { get; set; }

        public ResourceUsageDto Vehicles { get; set; } = new();
        public ResourceUsageDto Users { get; set; } = new();
        public ResourceUsageDto MonthlyShipments { get; set; } = new();

        /// <summary>
        /// Pre-computed human-readable strings describing any resource whose
        /// utilisation is at or above the critical threshold. Empty when the
        /// tenant is comfortably under all limits.
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    public class ResourceUsageDto
    {
        public int Current { get; set; }
        public int Max { get; set; }
        /// <summary>0-100 integer for easy UI rendering (bar width, colour ramp).</summary>
        public int Percent { get; set; }
        /// <summary>True when Percent >= critical threshold (80%).</summary>
        public bool IsCritical { get; set; }
    }
}
