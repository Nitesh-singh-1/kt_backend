using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KTransport.API.Authorization;
using KTransport.API.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace KTransport.API.Tests.Security;

/// <summary>
/// Phase 3 of the RBAC hardening plan: a standing guard that fails the build if
/// any state-changing controller action (POST/PUT/DELETE/PATCH) ships without
/// an authorization decision. Before this, ~10 controllers mutated data behind
/// only a class-level [Authorize], so any authenticated tenant user — even a
/// viewer — could create/edit/delete. This test makes that class of gap
/// impossible to reintroduce silently: a new mutating endpoint MUST carry one
/// of the recognized gates or this test names it and fails.
/// </summary>
public class ControllerAuthorizationCoverageTests
{
    private static readonly Assembly ApiAssembly = typeof(ShipmentController).Assembly;

    /// <summary>
    /// Self-service endpoints that act only on the CALLER's own identity. These
    /// are correctly gated by [Authorize] alone — no RBAC permission applies
    /// (every authenticated user may manage their own password/profile/token).
    /// Listed explicitly so the exemption is visible and auditable rather than
    /// hidden behind a loose attribute rule. Format: "Controller.Method".
    /// </summary>
    private static readonly HashSet<string> SelfServiceAllowlist = new(StringComparer.Ordinal)
    {
        "AuthController.ChangePassword",
        "AuthController.UpdateMyProfile",
        "AuthController.ValidateToken",
    };

    private static bool IsMutating(MethodInfo method)
        => method.GetCustomAttributes().Any(a =>
            a is HttpPostAttribute or HttpPutAttribute or HttpDeleteAttribute or HttpPatchAttribute);

    // An action is considered authorized if it — or its declaring controller —
    // carries any of these. [RequireFeature] alone does NOT count: it gates the
    // tenant's MODULE subscription, not the caller's role/permission, so a
    // mutation behind only [RequireFeature] is still open to every tenant user.
    private static bool HasAuthorizationDecision(MethodInfo method)
    {
        bool Has<T>(MemberInfo m) where T : Attribute => m.GetCustomAttributes(typeof(T), inherit: true).Any();

        var type = method.DeclaringType!;
        return Has<RequirePermissionAttribute>(method) || Has<RequirePermissionAttribute>(type)
            || Has<RequireSuperUserAttribute>(method) || Has<RequireSuperUserAttribute>(type)
            || Has<RequirePlatformAdminAttribute>(method) || Has<RequirePlatformAdminAttribute>(type)
            || Has<AllowAnonymousAttribute>(method) || Has<AllowAnonymousAttribute>(type);
    }

    private static IEnumerable<MethodInfo> AllMutatingActions()
    {
        var controllers = ApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var c in controllers)
        {
            var methods = c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var m in methods)
            {
                if (IsMutating(m)) yield return m;
            }
        }
    }

    [Fact]
    public void Every_mutating_controller_action_has_an_authorization_decision()
    {
        var offenders = AllMutatingActions()
            .Where(m => !HasAuthorizationDecision(m))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Where(name => !SelfServiceAllowlist.Contains(name))
            .OrderBy(x => x)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "These state-changing actions have no authorization gate " +
            "([RequirePermission] / [RequireSuperUser] / [RequirePlatformAdmin] / [AllowAnonymous]):\n  " +
            string.Join("\n  ", offenders));
    }

    [Fact]
    public void Guard_actually_sees_controller_actions()
    {
        // Sanity: the reflection scan must find a meaningful number of mutating
        // actions, otherwise a change to the assembly/type filter could make the
        // coverage test vacuously pass.
        Assert.True(AllMutatingActions().Count() > 50,
            $"Expected the scan to find many mutating actions; found {AllMutatingActions().Count()}.");
    }
}
