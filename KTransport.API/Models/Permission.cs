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
    }
}
