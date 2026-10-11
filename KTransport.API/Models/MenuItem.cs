namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-041 Phase 1: global menu catalog that shadows the hard-coded
    /// if/else tree in <c>NavigationService.GetDynamicMenuAsync</c>.
    /// 10 columns; GLOBAL (no tenant_id) — access-controlled only via
    /// [RequirePlatformAdmin] on admin endpoints.
    /// </summary>
    public class MenuItem
    {
        public int Id { get; set; }

        /// <summary>Semantic key, e.g. "billing.bill_book". Unique.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>NULL = top-level item. Else references another row's Key.</summary>
        public string? ParentKey { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>NULL = parent group with no route (expands children only).</summary>
        public string? Path { get; set; }

        /// <summary>Icon key the frontend sidebar renderer resolves (e.g. "package").</summary>
        public string? Icon { get; set; }

        /// <summary>
        /// NULL = no permission gate (always visible). Else the permission key
        /// that must be in the user's effective set for this item to appear.
        /// Not a FK — permissions catalog is intentionally decoupled.
        /// </summary>
        public string? PermissionKey { get; set; }

        /// <summary>Optional pill text like "Freight Bill".</summary>
        public string? Badge { get; set; }

        public int DisplayOrder { get; set; }

        /// <summary>
        /// Names a non-trivial visibility predicate. Phase 1 known values:
        /// NULL (default), "report_entitlement", "subscription_feature".
        /// </summary>
        public string? VisibilityRule { get; set; }

        /// <summary>Soft-delete. Phase 2+ admin UI uses this instead of DELETE.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// TASK-049 Option B: FK to <see cref="Module"/>.Id. Backfilled by the
        /// <c>MenuRbacNormalization</c> migration from <see cref="Key"/> /
        /// <see cref="ParentKey"/>. The structural menu join uses this column
        /// (instead of fuzzy string lookups on <see cref="Key"/> or
        /// <see cref="PermissionKey"/>). Legacy string columns stay through
        /// Phase A for the one-release transition and are dropped in Phase B.
        /// </summary>
        public int ModuleId { get; set; }

        public virtual Module? Module { get; set; }
    }
}
