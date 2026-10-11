using System;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-040 Phase 1: Global permission catalog. One row per (feature × action).
    /// Not tenant-scoped — permission keys are a product-wide namespace.
    /// </summary>
    public class Permission
    {
        public int Id { get; set; }

        /// <summary>Fully qualified key, e.g. "billing.bill_book.view".</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Feature id this maps to (e.g. "billing.bill_book").</summary>
        public string FeatureKey { get; set; } = string.Empty;

        /// <summary>Action enum string: View, Create, Edit, Delete, Print, Approve, Export.</summary>
        public string Action { get; set; } = "View";

        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 0;

        /// <summary>
        /// TASK-049 Option B: FK to <see cref="Module"/>.Id. Added NOT NULL after
        /// the <c>MenuRbacNormalization</c> migration backfills from
        /// <see cref="FeatureKey"/> prefix. Lets admin role seeding and menu
        /// rendering become pure structural joins instead of fuzzy string
        /// matchers.
        /// </summary>
        public int ModuleId { get; set; }

        public virtual Module? Module { get; set; }
    }
}
