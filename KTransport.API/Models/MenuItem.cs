namespace KTransport.API.Models
{
    /// <summary>
    /// Global menu catalog = the PAGES of a module. GLOBAL (no tenant_id) —
    /// access-controlled only via [RequirePlatformAdmin] on admin endpoints.
    ///
    /// RBAC clean-slate (ADR rbac-menu-consolidation-clean-slate):
    /// the string self-ref <c>parent_key</c> is replaced by an int self-FK
    /// <see cref="ParentId"/>. <see cref="Key"/> stays as the stable UI id and
    /// as the page-restriction key (permissions.page_key → this key).
    /// <see cref="PermissionKey"/> is kept purely as a UI hint for the frontend
    /// button-gating; it is NOT a visibility gate (module_id + page is).
    /// </summary>
    public class MenuItem
    {
        public int Id { get; set; }

        /// <summary>Semantic UI key, e.g. "billing.invoices". Unique.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>NULL = top-level item. Else the parent menu_items.id.</summary>
        public int? ParentId { get; set; }

        public virtual MenuItem? Parent { get; set; }

        public string Title { get; set; } = string.Empty;

        /// <summary>NULL = parent group with no route (expands children only).</summary>
        public string? Path { get; set; }

        /// <summary>Icon key the frontend sidebar renderer resolves (e.g. "package").</summary>
        public string? Icon { get; set; }

        /// <summary>UI-only button-gating hint (not a visibility gate).</summary>
        public string? PermissionKey { get; set; }

        /// <summary>Optional pill text like "Freight Bill".</summary>
        public string? Badge { get; set; }

        public int DisplayOrder { get; set; }

        /// <summary>
        /// Names a non-trivial visibility predicate. Known values:
        /// NULL (default), "report_entitlement".
        /// </summary>
        public string? VisibilityRule { get; set; }

        /// <summary>Soft-delete / global kill switch for the catalog row itself.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>NOT NULL FK → <see cref="Module"/>.Id. The structural menu join.</summary>
        public int ModuleId { get; set; }

        public virtual Module? Module { get; set; }
    }
}
