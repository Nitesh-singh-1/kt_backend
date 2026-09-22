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
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public NavigationService(KTransportDbContext context)
        {
            _context = context;
        }

        public async Task<List<DynamicMenuItemDto>> GetDynamicMenuAsync(Guid tenantId, string userRole)
        {
            var entitlements = await GetTenantMenuEntitlementsAsync(tenantId);
            var enabledKeys = new HashSet<string>(entitlements.EnabledMenuKeys, StringComparer.OrdinalIgnoreCase);
            var enabledReportKeys = entitlements.Reports
                .Where(r => r.IsEnabled)
                .Select(r => r.ReportKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool isAdmin = string.Equals(userRole, "admin", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(userRole, "tenantadmin", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(userRole, "superadmin", StringComparison.OrdinalIgnoreCase);

            var menu = new List<DynamicMenuItemDto>();

            // Helper to check if item is enabled (admins get default access if key set is permissive)
            bool IsEnabled(string key) => isAdmin || enabledKeys.Count == 0 || enabledKeys.Contains(key);

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

            // 2. Consignments (GR)
            if (IsEnabled("consignments") || IsEnabled("gr"))
            {
                var grChildren = new List<DynamicMenuItemDto>();

                if (IsEnabled("consignments.all") || IsEnabled("gr.list"))
                {
                    grChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "consignments.all",
                        Title = "All Shipments",
                        Path = "/shipments",
                        Icon = "fileText",
                        PermissionKey = "consignments.view"
                    });
                }

                if ((IsEnabled("consignments.create") || IsEnabled("gr.entry")) && !string.Equals(userRole, "viewer", StringComparison.OrdinalIgnoreCase))
                {
                    grChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "consignments.create",
                        Title = "New Consignment",
                        Path = "/shipments/create",
                        Icon = "package",
                        PermissionKey = "consignments.create"
                    });
                }

                if (grChildren.Count > 0)
                {
                    menu.Add(new DynamicMenuItemDto
                    {
                        Id = "consignments",
                        Title = "Consignments (GR)",
                        Icon = "package",
                        PermissionKey = "consignments.module",
                        Children = grChildren
                    });
                }
            }

            // 3. Trip Manifests
            if (IsEnabled("trips") || IsEnabled("challan"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "trips",
                    Title = "Trip Manifests",
                    Path = "/trips",
                    Icon = "truck",
                    PermissionKey = "trips.view"
                });
            }

            // 4. POD & Deliveries
            if (IsEnabled("pod"))
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

            // 5. Master Data (Parties & Fleet)
            if (IsEnabled("master_data") || IsEnabled("customers") || IsEnabled("fleet"))
            {
                var masterChildren = new List<DynamicMenuItemDto>();

                if (IsEnabled("master_data.parties") || IsEnabled("customers"))
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

                if (IsEnabled("master_data.fleet") || IsEnabled("fleet"))
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

            // 6. Market Vendors & Hire
            if (IsEnabled("vendors"))
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

            // 7. Billing & Invoices
            if (IsEnabled("billing"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "billing",
                    Title = "Billing & Invoices",
                    Path = "/billing",
                    Icon = "fileText",
                    PermissionKey = "billing.view"
                });
            }

            // 8. Damage & Claims
            if (IsEnabled("claims"))
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
            if (IsEnabled("reports"))
            {
                var reportChildren = new List<DynamicMenuItemDto>();

                foreach (var rep in entitlements.Reports.Where(r => r.IsEnabled || isAdmin))
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

            // 10. Live Tracker
            if (IsEnabled("tracking"))
            {
                menu.Add(new DynamicMenuItemDto
                {
                    Id = "tracking",
                    Title = "Live Tracker",
                    Path = "/tracking",
                    Icon = "info",
                    PermissionKey = "tracking.view"
                });
            }

            // 11. SuperAdmin / TenantAdmin Client Management
            if (IsEnabled("clients") || (tenantId == TenantConstants.DefaultTenantId && isAdmin))
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

            // 12. System / Settings Group
            if (IsEnabled("system"))
            {
                var systemChildren = new List<DynamicMenuItemDto>();

                if (IsEnabled("system.settings") && (isAdmin || string.Equals(userRole, "manager", StringComparison.OrdinalIgnoreCase)))
                {
                    systemChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "system.settings",
                        Title = "SaaS Configuration",
                        Path = "/settings",
                        Icon = "settings",
                        PermissionKey = "settings.manage"
                    });
                }

                if (IsEnabled("system.onboard") || isAdmin)
                {
                    systemChildren.Add(new DynamicMenuItemDto
                    {
                        Id = "system.onboard",
                        Title = "Tenant Onboarding",
                        Path = "/onboard",
                        Icon = "info",
                        PermissionKey = "tenant.onboard"
                    });
                }

                systemChildren.Add(new DynamicMenuItemDto
                {
                    Id = "system.forgot_password",
                    Title = "Forgot Password",
                    Path = "/forgot-password",
                    Icon = "lock",
                    PermissionKey = "auth.password_reset"
                });

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

        public async Task<List<string>> GetUserPermissionsAsync(Guid tenantId, string userRole)
        {
            var menu = await GetDynamicMenuAsync(tenantId, userRole);
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

            // Add standard root wildcard / view permissions for admins
            if (string.Equals(userRole, "admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userRole, "tenantadmin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(userRole, "superadmin", StringComparison.OrdinalIgnoreCase))
            {
                permissions.Add("*");
                permissions.Add("admin");
                permissions.Add("reports.view");
                permissions.Add("settings.manage");
                permissions.Add("consignments.view");
                permissions.Add("consignments.create");
                permissions.Add("trips.view");
                permissions.Add("pod.view");
                permissions.Add("parties.view");
                permissions.Add("fleet.view");
                permissions.Add("vendors.view");
                permissions.Add("billing.view");
                permissions.Add("claims.view");
                permissions.Add("tracking.view");
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
                    if (parsed != null && parsed.EnabledMenuKeys != null && parsed.EnabledMenuKeys.Count > 0)
                    {
                        parsed.TenantId = tenantId;
                        EnsureAllCatalogReportsPresent(parsed);
                        EnsureCoreMenuKeysPresent(parsed);
                        return parsed;
                    }
                }
                catch
                {
                    // Fallback to default
                }
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
                    dto.Reports.Add(std);
                }
            }
        }

        private void EnsureCoreMenuKeysPresent(TenantMenuEntitlementsDto dto)
        {
            var defaultKeys = GetDefaultMenuKeys();
            var existingKeys = new HashSet<string>(dto.EnabledMenuKeys, StringComparer.OrdinalIgnoreCase);

            foreach (var key in defaultKeys)
            {
                if (!existingKeys.Contains(key))
                {
                    dto.EnabledMenuKeys.Add(key);
                }
            }
        }

        private List<string> GetDefaultMenuKeys()
        {
            return new List<string>
            {
                "dashboard",
                "consignments",
                "consignments.all",
                "consignments.create",
                "gr",
                "gr.list",
                "gr.entry",
                "trips",
                "pod",
                "master_data",
                "master_data.parties",
                "master_data.fleet",
                "customers",
                "fleet",
                "vendors",
                "billing",
                "claims",
                "reports",
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
