using System;

namespace KTransport.API.Models
{
    /// <summary>
    /// Global permission catalog. One row per (module × page × action).
    /// Not tenant-scoped — permission keys are a product-wide namespace.
    ///
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate):
    /// <see cref="FeatureKey"/> is GONE. A permission now resolves structurally
    /// via <see cref="ModuleId"/> (NOT NULL FK → modules.id) and the optional
    /// <see cref="PageKey"/> (→ menu_items.key) that scopes it to a single page.
    /// </summary>
    public class Permission
    {
        public int Id { get; set; }

        /// <summary>Canonical dotted key, e.g. "billing.invoices.view" or "reports.view".</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Action enum string: View, Create, Edit, Delete, Print, Approve, Export.</summary>
        public string Action { get; set; } = "View";

        public string? Description { get; set; }

        public int DisplayOrder { get; set; } = 0;

        /// <summary>NOT NULL FK → <see cref="Module"/>.Id. The structural module gate.</summary>
        public int ModuleId { get; set; }

        public virtual Module? Module { get; set; }

        /// <summary>
        /// Optional FK → <see cref="MenuItem.Key"/>. When set, this permission is
        /// scoped to that page and is in the tenant ceiling only if the page is
        /// entitled (tenant_module_pages). NULL = module-level permission.
        /// </summary>
        public string? PageKey { get; set; }
    }
}
