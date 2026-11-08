using System;
using System.Collections.Generic;

namespace KTransport.API.Common
{
    /// <summary>
    /// RBAC clean-slate: the canonical module code set and the default plan→module
    /// grants. Single source of truth for the C# seed/onboarding path (the DB
    /// catalog reseed migration mirrors these codes). No aliases.
    /// </summary>
    public static class RbacCatalog
    {
        /// <summary>The 17 canonical module codes.</summary>
        public static readonly string[] AllModuleCodes =
        {
            "dashboard", "bilty", "pod", "trips", "billing", "master_data", "reports",
            "vendors", "claims", "quotations", "tracking", "analytics", "system",
            "trip_settlement", "delivery_settlement", "accounting", "clients",
        };

        /// <summary>The 7 seeded system role codes (admin first).</summary>
        public static readonly string[] SystemRoleCodes =
        {
            "admin", "operations_manager", "supervisor", "operator",
            "billing_executive", "accountant", "viewer",
        };

        /// <summary>
        /// Default module grants per plan tier (platform admin negotiates deltas
        /// per tenant afterwards). "clients" and "accounting" are never auto-granted
        /// (clients is platform-only; accounting is a paid add-on).
        /// </summary>
        public static IReadOnlyList<string> ModulesForPlan(string? tier)
        {
            var t = (tier ?? "professional").Trim().ToLowerInvariant();
            var starter = new List<string>
            {
                "dashboard", "bilty", "pod", "trips", "delivery_settlement",
                "tracking", "master_data", "system",
            };
            if (t == "starter") return starter;

            var professional = new List<string>(starter)
            {
                "billing", "trip_settlement", "reports", "vendors", "quotations", "claims", "analytics",
            };
            if (t == "professional") return professional;

            // Enterprise = everything except the platform-only clients module.
            var enterprise = new List<string>();
            foreach (var c in AllModuleCodes)
                if (c != "clients") enterprise.Add(c);
            return enterprise;
        }
    }
}
