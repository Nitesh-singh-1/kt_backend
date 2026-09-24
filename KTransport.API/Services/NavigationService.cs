using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class NavigationService : INavigationService
    {
        private readonly KTransportDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public NavigationService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<List<DynamicMenuItemDto>> GetDynamicMenuAsync(Guid tenantId, string userRole, string? userIdOrName = null)
        {
            var entitlements = await GetTenantMenuEntitlementsAsync(tenantId);
            
            // 1. Organization-Level Subscribed Keys
            var subscribedKeys = new HashSet<string>(entitlements.EnabledMenuKeys, StringComparer.OrdinalIgnoreCase);

            bool isSuperUser = string.Equals(userRole, "SUPER_USER", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "admin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "tenantadmin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "superadmin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "TENANT_OWNER", StringComparison.OrdinalIgnoreCase);

            HashSet<string> effectiveKeys;

            if (isSuperUser)
            {
                // Super User gets all organization-subscribed features + settings & dashboard
                effectiveKeys = new HashSet<string>(subscribedKeys, StringComparer.OrdinalIgnoreCase);
                effectiveKeys.Add("dashboard");
                effectiveKeys.Add("system");
                effectiveKeys.Add("system.settings");
                if (tenantId == TenantConstants.DefaultTenantId)
                {
                    effectiveKeys.Add("clients");
                }
            }
            else
            {
                // Sub User: Resolve assigned permissions
                var userAssigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // 1. Check User-Specific Dedicated Overrides
                if (!string.IsNullOrWhiteSpace(userIdOrName) && !string.IsNullOrWhiteSpace(entitlements.UserOverridesJson))
                {
                    try
                    {
                        var userOverrides = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(entitlements.UserOverridesJson, JsonOptions);
                        if (userOverrides != null)
                        {
                            var matchingKey = userOverrides.Keys.FirstOrDefault(k => string.Equals(k, userIdOrName, StringComparison.OrdinalIgnoreCase));
                            if (matchingKey != null && userOverrides[matchingKey] != null)
                            {
                                foreach (var k in userOverrides[matchingKey])
                                {
                                    userAssigned.Add(k);
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2. Check Role-Based Matrix Overrides if no direct user override was found
                if (userAssigned.Count == 0 && !string.IsNullOrWhiteSpace(userRole) && !string.IsNullOrWhiteSpace(entitlements.RoleOverridesJson))
                {
                    try
                    {
                        var roleOverrides = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(entitlements.RoleOverridesJson, JsonOptions);
                        if (roleOverrides != null)
                        {
                            var matchingRole = roleOverrides.Keys.FirstOrDefault(k => string.Equals(k, userRole, StringComparison.OrdinalIgnoreCase));
                            if (matchingRole != null && roleOverrides[matchingRole] != null)
                            {
                                foreach (var k in roleOverrides[matchingRole])
                                {
                                    userAssigned.Add(k);
                                }
                            }
                        }
                    }
                    catch { }
                }

                // If no user overrides or role overrides exist, fallback to subscribed keys minus administrative ones
                if (userAssigned.Count == 0)
                {
                    userAssigned = new HashSet<string>(subscribedKeys, StringComparer.OrdinalIgnoreCase);
                }

                // Effective Access = Organization Subscription ∩ User Permissions
                effectiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var k in userAssigned)
                {
                    var canon = FeatureConstants.Normalize(k);
                    // Match either direct key or normalized canonical code
                    if (subscribedKeys.Contains(k) || subscribedKeys.Any(sk => FeatureConstants.Normalize(sk) == canon))
                    {
                        effectiveKeys.Add(k);
                    }
                }

                // Active sub-users always get Dashboard
                effectiveKeys.Add("dashboard");

                // Sub-users NEVER receive SaaS configuration or superadmin screens
                effectiveKeys.Remove("system.settings");
                effectiveKeys.Remove("system.onboard");
                effectiveKeys.Remove("clients");
                effectiveKeys.Remove("saas");
                effectiveKeys.Remove("SAAS_CONFIGURATION");
            }

            var enabledReportKeys = entitlements.Reports
                .Where(r => r.IsEnabled)
                .Select(r => r.ReportKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var menu = new List<DynamicMenuItemDto>();

            bool IsEnabled(string key)
            {
                return effectiveKeys.Contains(key) || effectiveKeys.Contains(FeatureConstants.Normalize(key));
            }

            // 1. Dashboard
            if (IsEnabled("dashboard"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "dashboard",
                    Title = "Dashboard",
                    Path = "/dashboard",
                    Icon = "home",
                    PermissionKey = "dashboard.view"
                });
            }

            // 2. Consignments (Bilty / GR Booking)
            if (IsEnabled("consignments") || IsEnabled("consignments.create") || IsEnabled("consignments.all") || IsEnabled("gr") || IsEnabled("GOOD_RECEIPT") || IsEnabled("SHIPMENT"))
            {
                var grChildren = new List<DynamicMenuItemDto>();

                if (IsEnabled("consignments.create") || IsEnabled("gr.entry") || IsEnabled("GOOD_RECEIPT"))
                {
                    grChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "consignments.create",
                        Title = "New Bilty (GR Booking)",
                        Path = "/shipments/create",
                        Icon = "package",
                        PermissionKey = "consignments.create"
                    });
                }

                if (IsEnabled("consignments.all") || IsEnabled("consignments") || IsEnabled("gr.list") || IsEnabled("gr") || IsEnabled("GOOD_RECEIPT") || IsEnabled("SHIPMENT"))
                {
                    grChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "consignments.all",
                        Title = "All Bilties (GR Registry)",
                        Path = "/shipments",
                        Icon = "fileText",
                        PermissionKey = "consignments.view"
                    });
                }

                if (grChildren.Count > 0)
                {
                    menu.Add(new DynamicMenuItemDto
                    {
                        Id = "consignments",
                        Title = "Bilty / GR Booking",
                        Icon = "package",
                        PermissionKey = "consignments.module",
                        Children = grChildren
                    });
                }
            }

            // 3. LR / Truck Challan (Manifest)
            if (IsEnabled("trips") || IsEnabled("challan") || IsEnabled("challan.list") || IsEnabled("challan.entry") || IsEnabled("MANIFEST"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "trips",
                    Title = "LR / Truck Challan",
                    Path = "/trips",
                    Icon = "truck",
                    PermissionKey = "trips.view"
                });
            }

            // 4. POD & Deliveries
            if (IsEnabled("pod") || IsEnabled("POD"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "pod",
                    Title = "POD & Deliveries",
                    Path = "/pod",
                    Icon = "fileText",
                    PermissionKey = "pod.view"
                });
            }

            // 5. Freight Invoicing & Billing
            if (IsEnabled("billing") || IsEnabled("billing.invoices") || IsEnabled("billing.receipts") || IsEnabled("receipts") || IsEnabled("BILLING") || IsEnabled("INVOICE"))
            {
                var billingChildren = new List<DynamicMenuItemDto>();

                if (IsEnabled("billing.invoices") || IsEnabled("billing") || IsEnabled("BILLING") || IsEnabled("INVOICE"))
                {
                    billingChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "billing.invoices",
                        Title = "Freight Invoices",
                        Path = "/billing",
                        Icon = "fileText",
                        PermissionKey = "billing.view"
                    });
                }

                if (IsEnabled("billing.receipts") || IsEnabled("receipts") || IsEnabled("billing") || IsEnabled("BILLING") || IsEnabled("MONEY_RECEIPT"))
                {
                    billingChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "billing.receipts",
                        Title = "Money Receipts (MR)",
                        Path = "/receipts",
                        Icon = "fileText",
                        PermissionKey = "billing.view",
                        Badge = "Paid MR"
                    });
                }

                if (billingChildren.Count > 0)
                {
                    menu.Add(new DynamicMenuItemDto
                    {
                        Id = "billing",
                        Title = "Freight Invoicing & Billing",
                        Icon = "fileText",
                        PermissionKey = "billing.view",
                        Children = billingChildren
                    });
                }
            }

            // 6. Master Data (Parties & Fleet)
            if (IsEnabled("master_data") || IsEnabled("master_data.parties") || IsEnabled("master_data.fleet") || IsEnabled("customers") || IsEnabled("fleet") || IsEnabled("PARTY") || IsEnabled("VEHICLE"))
            {
                var masterChildren = new List<DynamicMenuItemDto>();

                if (IsEnabled("master_data.parties") || IsEnabled("customers") || IsEnabled("master_data") || IsEnabled("PARTY"))
                {
                    masterChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "master_data.parties",
                        Title = "Party Directory",
                        Path = "/customers",
                        Icon = "fileText",
                        PermissionKey = "parties.view"
                    });
                }

                if (IsEnabled("master_data.fleet") || IsEnabled("fleet") || IsEnabled("master_data") || IsEnabled("VEHICLE"))
                {
                    masterChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "master_data.fleet",
                        Title = "Fleet & Stations",
                        Path = "/fleet",
                        Icon = "truck",
                        PermissionKey = "fleet.view"
                    });
                }

                if (masterChildren.Count > 0)
                {
                    menu.Add(new DynamicMenuItemDto
                    {
                        Id = "master_data",
                        Title = "Master Data",
                        Icon = "cog",
                        PermissionKey = "masterdata.module",
                        Children = masterChildren
                    });
                }
            }

            // 7. Market Vendors & Hire
            if (IsEnabled("vendors") || IsEnabled("VENDOR"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "vendors",
                    Title = "Market Vendors & Hire",
                    Path = "/vendors",
                    Icon = "truck",
                    PermissionKey = "vendors.view"
                });
            }

            // 8. Damage & Claims
            if (IsEnabled("claims") || IsEnabled("CLAIMS"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "claims",
                    Title = "Damage & Claims",
                    Path = "/claims",
                    Icon = "info",
                    PermissionKey = "claims.view"
                });
            }

            // 9. Reports & Analytics
            if (IsEnabled("reports") || IsEnabled("REPORTING"))
            {
                var reportChildren = new List<DynamicMenuItemDto>();

                foreach (var rep in entitlements.Reports.Where(r => r.IsEnabled || enabledReportKeys.Contains(r.ReportKey)))
                {
                    reportChildren.Add(new DynamicMenuItemDto
                    {
                        Id = $"reports.{rep.ReportKey}",
                        Title = rep.Title,
                        Path = $"/reports?tab={rep.ReportKey}",
                        Icon = "fileText",
                        PermissionKey = $"reports.{rep.ReportKey}"
                    });
                }

                menu.Add(new DynamicMenuItemDto
                {
                    Id = "reports",
                    Title = "Reports & Analytics",
                    Path = "/reports",
                    Icon = "barChart",
                    PermissionKey = "reports.view",
                    Badge = reportChildren.Count > 0 ? $"{reportChildren.Count}" : null,
                    Children = reportChildren.Count > 0 ? reportChildren : null
                });
            }

            // 10. Live GPS Tracker
            if (IsEnabled("tracking") || IsEnabled("TRACKING"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "tracking",
                    Title = "Live GPS Tracker",
                    Path = "/tracking",
                    Icon = "info",
                    PermissionKey = "tracking.view"
                });
            }

            // 11. SuperAdmin / Client Management (Super User only)
            if (isSuperUser && (IsEnabled("clients") || tenantId == TenantConstants.DefaultTenantId))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "clients",
                    Title = "Client Management",
                    Path = "/clients",
                    Icon = "lock",
                    PermissionKey = "saas.tenants.manage",
                    Badge = "SaaS"
                });
            }

            // 12. System / SaaS Configuration Group (Super User only)
            if (isSuperUser)
            {
                var systemChildren = new List<DynamicMenuItemDto>
                {
                    new DynamicMenuItemDto
                    {
                        Id = "system.settings",
                        Title = "SaaS Configuration",
                        Path = "/settings",
                        Icon = "settings",
                        PermissionKey = "settings.manage"
                    },
                    new DynamicMenuItemDto
                    {
                        Id = "system.onboard",
                        Title = "Tenant Onboarding",
                        Path = "/onboard",
                        Icon = "info",
                        PermissionKey = "tenant.onboard"
                    },
                    new DynamicMenuItemDto
                    {
                        Id = "system.forgot_password",
                        Title = "Forgot Password",
                        Path = "/forgot-password",
                        Icon = "lock",
                        PermissionKey = "auth.password_reset"
                    }
                };

                menu.Add(new DynamicMenuItemDto
                {
                    Id = "system",
                    Title = "System & Settings",
                    Icon = "settings",
                    Children = systemChildren
                });
            }

            return menu;
        }

        public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, string userRole, string? userIdOrName = null)
        {
            var menu = await GetDynamicMenuAsync(tenantId, userRole, userIdOrName);
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void CollectPermissions(DynamicMenuItemDto item)
            {
                if (!string.IsNullOrWhiteSpace(item.PermissionKey))
                {
                    permissions.Add(item.PermissionKey);
                }
                if (item.Children != null)
                {
                    foreach (var child in item.Children)
                    {
                        CollectPermissions(child);
                    }
                }
            }

            foreach (var item in menu)
            {
                CollectPermissions(item);
            }

            bool isSuperUser = string.Equals(userRole, "SUPER_USER", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "admin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "tenantadmin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "superadmin", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(userRole, "TENANT_OWNER", StringComparison.OrdinalIgnoreCase);

            if (isSuperUser)
            {
                permissions.Add("*");
                permissions.Add("admin");
                permissions.Add("settings.manage");
                permissions.Add("users.manage");
                permissions.Add("saas.tenants.manage");
            }

            return permissions.ToList();
        }

        public async Task<TenantMenuEntitlementsDto> GetTenantMenuEntitlementsAsync(Guid tenantId)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            if (setting != null && !string.IsNullOrWhiteSpace(setting.MenuEntitlementsJson) && setting.MenuEntitlementsJson != "{}")
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TenantMenuEntitlementsDto>(setting.MenuEntitlementsJson, JsonOptions);
                    if (parsed != null && parsed.EnabledMenuKeys != null)
                    {
                        parsed.TenantId = tenantId;
                        EnsureAllCatalogReportsPresent(parsed);
                        return parsed;
                    }
                }
                catch { }
            }

            return GetDefaultEntitlements(tenantId);
        }

        public async Task<TenantMenuEntitlementsDto> UpdateTenantMenuEntitlementsAsync(Guid tenantId, TenantMenuEntitlementsDto dto)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            dto.TenantId = tenantId;
            var json = JsonSerializer.Serialize(dto, JsonOptions);

            if (setting == null)
            {
                setting = new TenantSetting
                {
                    TenantId = tenantId,
                    MenuEntitlementsJson = json,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TenantSettings.Add(setting);
            }
            else
            {
                setting.MenuEntitlementsJson = json;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return dto;
        }

        private void EnsureAllCatalogReportsPresent(TenantMenuEntitlementsDto dto)
        {
            var standardReports = GetStandardReportCatalog();
            var existingKeys = dto.Reports.Select(r => r.ReportKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var std in standardReports)
            {
                if (!existingKeys.Contains(std.ReportKey))
                {
                    dto.Reports.Add(new ReportEntitlementItemDto
                    {
                        ReportKey = std.ReportKey,
                        Title = std.Title,
                        Description = std.Description,
                        Category = std.Category,
                        Path = std.Path,
                        IsEnabled = false
                    });
                }
            }
        }

        private List<string> GetDefaultMenuKeys()
        {
            return new List<string>
            {
                "dashboard",
                "consignments",
                "consignments.create",
                "consignments.all",
                "trips",
                "pod",
                "billing",
                "billing.invoices",
                "billing.receipts",
                "master_data",
                "master_data.parties",
                "master_data.fleet",
                "vendors",
                "claims",
                "reports",
                "reports.booking_register",
                "reports.tax_summary",
                "reports.party_outstanding",
                "reports.trip_profitability",
                "reports.vendor_payables",
                "tracking",
                "clients",
                "system",
                "system.settings",
                "system.onboard",
                "system.forgot_password"
            };
        }

        private TenantMenuEntitlementsDto GetDefaultEntitlements(Guid tenantId)
        {
            return new TenantMenuEntitlementsDto
            {
                TenantId = tenantId,
                PlanTier = "Enterprise",
                EnabledMenuKeys = GetDefaultMenuKeys(),
                Reports = GetStandardReportCatalog()
            };
        }

        private List<ReportEntitlementItemDto> GetStandardReportCatalog()
        {
            return new List<ReportEntitlementItemDto>
            {
                new ReportEntitlementItemDto
                {
                    ReportKey = "booking_register",
                    Title = "Consignment Booking Register",
                    Description = "Comprehensive log of all booked goods receipts with weight, freight, and consignor breakdown",
                    Category = "Operational",
                    Path = "/reports?tab=booking_register",
                    IsEnabled = true
                },
                new ReportEntitlementItemDto
                {
                    ReportKey = "tax_summary",
                    Title = "GST & Tax Summary Report",
                    Description = "Taxable amounts, CGST, SGST, IGST, and RCM breakdowns for monthly GST returns",
                    Category = "Financial",
                    Path = "/reports?tab=tax_summary",
                    IsEnabled = true
                },
                new ReportEntitlementItemDto
                {
                    ReportKey = "party_outstanding",
                    Title = "Customer Outstanding Ledger",
                    Description = "Real-time receivables, billed vs settled balance, and customer aging summary",
                    Category = "Financial",
                    Path = "/reports?tab=party_outstanding",
                    IsEnabled = true
                },
                new ReportEntitlementItemDto
                {
                    ReportKey = "trip_profitability",
                    Title = "Trip Profitability & P&L",
                    Description = "Trip revenue vs diesel, toll, driver advance, and vehicle expense margin analysis",
                    Category = "Operational",
                    Path = "/reports?tab=trip_profitability",
                    IsEnabled = true
                },
                new ReportEntitlementItemDto
                {
                    ReportKey = "vendor_payables",
                    Title = "Vendor & Lorry Hire Payables",
                    Description = "Transporter / vehicle broker hiring contracts, paid advances, and pending dues",
                    Category = "Financial",
                    Path = "/reports?tab=vendor_payables",
                    IsEnabled = true
                }
            };
        }
    }
}
