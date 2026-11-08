using System;

namespace KTransport.API.Models
{
    /// <summary>
    /// RBAC clean-slate: the default module grant for a subscription plan.
    /// Global (not tenant-scoped). Assigning a plan seeds tenant_modules from
    /// these rows.
    /// </summary>
    public class PlanModule
    {
        public long Id { get; set; }
        public Guid PlanId { get; set; }
        public virtual SubscriptionPlan? Plan { get; set; }
        public int ModuleId { get; set; }
        public virtual Module? Module { get; set; }
    }

    /// <summary>
    /// RBAC clean-slate: optional per-plan page narrowing. When present for a
    /// (plan, module), assigning the plan seeds tenant_module_pages to exactly
    /// these page_keys.
    /// </summary>
    public class PlanModulePage
    {
        public long Id { get; set; }
        public Guid PlanId { get; set; }
        public virtual SubscriptionPlan? Plan { get; set; }
        public int ModuleId { get; set; }
        public virtual Module? Module { get; set; }
        public string PageKey { get; set; } = string.Empty;
    }
}
