using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Common;
using KTransport.API.Controllers;
using KTransport.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KTransport.API.Tests.Controllers;

/// <summary>
/// TASK-039 — exercises the per-page <see cref="RequirePermissionAttribute"/> as it
/// is wired on <see cref="InvoiceController"/>'s Bill Book flow endpoints.
///
/// There is no WebApplicationFactory wired in the current test harness (the test
/// project is pure unit tests on EF InMemory — see csproj and
/// <c>TripSettlementServiceTests</c>'s comment on scope). So instead of hitting the
/// endpoint through a Kestrel pipeline, we instantiate the attribute, hand it a
/// handcrafted <see cref="AuthorizationFilterContext"/> with the DI shape the
/// attribute reads (<see cref="ITenantContext"/> + <see cref="INavigationService"/>
/// stub), and assert the filter's <c>Result</c>:
///   - 403 when the stub reports NO `billing.bill_book` key.
///   - no Result (pass-through) when the stub reports it.
/// Both scenarios also assert that the attribute is actually attached to the
/// <c>POST /api/invoice</c> action (defence against someone silently removing it).
/// </summary>
public class InvoiceControllerAccessControlTests
{
    private static readonly Guid TestTenantId = TenantConstants.DefaultTenantId;

    private sealed class StubTenantContext : ITenantContext
    {
        public Guid CurrentTenantId { get; private set; } = TestTenantId;
        public bool HasTenant => true;
        public void SetTenantId(Guid tenantId) => CurrentTenantId = tenantId;
    }

    /// <summary>
    /// Minimal stub of <see cref="INavigationService"/>. Only
    /// <see cref="GetUserPermissionsAsync"/> is exercised by the attribute.
    /// </summary>
    private sealed class StubNavigationService : INavigationService
    {
        private readonly List<string> _permissions;
        public StubNavigationService(IEnumerable<string> permissions)
        {
            _permissions = permissions.ToList();
        }

        public Task<List<KTransport.API.DTOs.DynamicMenuItemDto>> GetDynamicMenuAsync(Guid tenantId, string userRole, string? userIdOrName = null)
            => Task.FromResult(new List<KTransport.API.DTOs.DynamicMenuItemDto>());

        public Task<List<string>> GetUserPermissionsAsync(Guid tenantId, string userRole, string? userIdOrName = null)
            => Task.FromResult(_permissions);

        public Task<KTransport.API.DTOs.TenantMenuEntitlementsDto> GetTenantMenuEntitlementsAsync(Guid tenantId)
            => Task.FromResult(new KTransport.API.DTOs.TenantMenuEntitlementsDto { TenantId = tenantId });

        public Task<KTransport.API.DTOs.TenantMenuEntitlementsDto> UpdateTenantMenuEntitlementsAsync(Guid tenantId, KTransport.API.DTOs.TenantMenuEntitlementsDto dto)
            => Task.FromResult(dto);
    }

    private static AuthorizationFilterContext BuildFilterContext(IEnumerable<string> permissionsReturnedByNav, bool authenticated = true, string role = "SUB_USER", string username = "nitesh")
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext>(new StubTenantContext());
        services.AddSingleton<INavigationService>(new StubNavigationService(permissionsReturnedByNav));
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        if (authenticated)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("tenant_id", TestTenantId.ToString())
            }, authenticationType: "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);
        }
        else
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    // ---------- 3. POST /api/invoice returns 403 when the page key is missing ----------
    [Fact]
    public async Task InvoiceController_PostInvoice_Returns403_WhenPageKeyMissing()
    {
        // User has the BILLING module (via class-level [RequireFeature]) but is missing
        // the granular `billing.bill_book` page key. The attribute must deny.
        var filter = new RequirePermissionAttribute("billing.bill_book");
        var ctx = BuildFilterContext(permissionsReturnedByNav: new[] { "billing.view", "dashboard.view" });

        await filter.OnAuthorizationAsync(ctx);

        Assert.NotNull(ctx.Result);
        var objectResult = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(403, objectResult.StatusCode);
    }

    // ---------- 4. POST /api/invoice succeeds (filter passes) when key is present ----------
    [Fact]
    public async Task InvoiceController_PostInvoice_Succeeds_WhenPageKeyPresent()
    {
        var filter = new RequirePermissionAttribute("billing.bill_book");
        var ctx = BuildFilterContext(permissionsReturnedByNav: new[] { "billing.view", "billing.bill_book", "dashboard.view" });

        await filter.OnAuthorizationAsync(ctx);

        // A null Result from an authorization filter means "pass through to the action".
        Assert.Null(ctx.Result);
    }

    // ---------- 4b. Super users with the "*" wildcard pass straight through ----------
    [Fact]
    public async Task InvoiceController_PostInvoice_Succeeds_ForSuperUserWildcard()
    {
        var filter = new RequirePermissionAttribute("billing.bill_book");
        var ctx = BuildFilterContext(permissionsReturnedByNav: new[] { "*", "admin" }, role: "SUPER_USER");

        await filter.OnAuthorizationAsync(ctx);

        Assert.Null(ctx.Result);
    }

    // ---------- 4c. Unauthenticated callers get 401, not 403 ----------
    [Fact]
    public async Task InvoiceController_PostInvoice_Returns401_WhenUnauthenticated()
    {
        var filter = new RequirePermissionAttribute("billing.bill_book");
        var ctx = BuildFilterContext(permissionsReturnedByNav: Array.Empty<string>(), authenticated: false);

        await filter.OnAuthorizationAsync(ctx);

        Assert.NotNull(ctx.Result);
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(ctx.Result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    // ---------- 5. The attribute is actually wired on the Bill Book flow actions ----------
    // Defence-in-depth: if a future refactor silently drops the attribute, the three
    // tests above would keep passing because they test the attribute in isolation.
    // These reflection checks fail loudly in that case.
    [Theory]
    [InlineData(nameof(InvoiceController.CreateInvoice))]
    [InlineData(nameof(InvoiceController.CreateBillBookInvoice))]
    [InlineData(nameof(InvoiceController.BulkBill))]
    [InlineData(nameof(InvoiceController.GetUnbilled))]
    [InlineData(nameof(InvoiceController.GetUnbilledParties))]
    [InlineData(nameof(InvoiceController.GetUnbilledByParty))]
    public void InvoiceController_BillBookFlowMethods_CarryRequirePermissionAttribute(string methodName)
    {
        var mi = typeof(InvoiceController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(mi);
        var attr = mi!.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
                      .Cast<RequirePermissionAttribute>()
                      .FirstOrDefault();
        Assert.NotNull(attr);
    }

    [Fact]
    public void InvoiceController_ReadEndpoints_DoNotCarryRequirePermissionAttribute()
    {
        // GetInvoices, GetInvoiceById, UpdateInvoice, VoidInvoice, RecordPayment stay
        // at the module gate only. Users with Billing module (no Bill Book) can still
        // view / edit existing invoices, per the contract's acceptance list.
        foreach (var name in new[] { nameof(InvoiceController.GetInvoices), nameof(InvoiceController.GetInvoiceById), nameof(InvoiceController.GetInvoiceLookup), nameof(InvoiceController.UpdateInvoice), nameof(InvoiceController.RecordPayment), nameof(InvoiceController.VoidInvoice) })
        {
            var mi = typeof(InvoiceController).GetMethod(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(mi);
            var hasAttr = mi!.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true).Any();
            Assert.False(hasAttr, $"{name} must NOT carry [RequirePermission] — it stays at the module gate only.");
        }
    }
}
