using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using UCredit.Infrastructure.Identity.Models;
using UCredit.Infrastructure.Identity.Tenants;
using Xunit;

namespace UCredit.Infrastructure.Identity.UnitTests;

public sealed class IdentityCookieEventsTests
{
    [Fact]
    public async Task DisabledMembershipRejectsPrincipalAndSignsOut()
    {
        var store = new MutableTenantMembershipStore { MembershipActive = false };
        var (context, authentication) = CreateContext(SelectedPrincipal(store));

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.True(authentication.SignedOut);
    }

    [Fact]
    public async Task DisabledTenantRejectsPrincipalAndSignsOut()
    {
        var store = new MutableTenantMembershipStore { TenantActive = false };
        var (context, authentication) = CreateContext(SelectedPrincipal(store));

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.True(authentication.SignedOut);
    }

    [Fact]
    public async Task DisabledUserRejectsPrincipalAndSignsOut()
    {
        var store = new MutableTenantMembershipStore { UserActive = false };
        var (context, authentication) = CreateContext(SelectedPrincipal(store));

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.True(authentication.SignedOut);
    }

    [Fact]
    public async Task RevokedPermissionIsRemovedAndPrincipalIsRenewed()
    {
        var store = new MutableTenantMembershipStore { PermissionCodes = [] };
        var (context, _) = CreateContext(SelectedPrincipal(store, "contracts.read"));

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.DoesNotContain(CurrentPrincipal(context).Claims, claim => claim.Type == "permission");
    }

    [Fact]
    public async Task AddedPermissionIsAppliedWhenPrincipalIsRenewed()
    {
        var store = new MutableTenantMembershipStore { PermissionCodes = ["contracts.read"] };
        var (context, _) = CreateContext(SelectedPrincipal(store));

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.Contains(CurrentPrincipal(context).Claims, claim => claim.Type == "permission" && claim.Value == "contracts.read");
    }

    [Fact]
    public async Task PrincipalWithoutTenantHasNoPermissions()
    {
        var store = new MutableTenantMembershipStore();
        var (context, _) = CreateContext(UserPrincipal(store.UserId));

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.NotNull(context.Principal);
        Assert.DoesNotContain(CurrentPrincipal(context).Claims, claim => claim.Type == "permission");
    }

    [Fact]
    public async Task IncompleteTenantClaimsRejectPrincipal()
    {
        var store = new MutableTenantMembershipStore();
        var principal = UserPrincipal(store.UserId, new Claim(TenantClaimTypes.Code, store.TenantCode));
        var (context, authentication) = CreateContext(principal);

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.True(authentication.SignedOut);
    }

    [Fact]
    public async Task ManipulatedTenantIdRejectsPrincipal()
    {
        var store = new MutableTenantMembershipStore();
        var principal = UserPrincipal(
            store.UserId,
            new Claim(TenantClaimTypes.Id, Guid.NewGuid().ToString("D")),
            new Claim(TenantClaimTypes.Code, store.TenantCode));
        var (context, authentication) = CreateContext(principal);

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.Null(context.Principal);
        Assert.True(authentication.SignedOut);
    }

    [Fact]
    public async Task PermissionsFromAnotherTenantAreNotIncorporated()
    {
        var store = new MutableTenantMembershipStore { PermissionCodes = ["contracts.write"] };
        var principal = SelectedPrincipal(store, "contracts.read");
        var (context, _) = CreateContext(principal);

        await new IdentityCookieEvents(store, new DeploymentTenantPolicy("TENANT-A")).ValidatePrincipal(context);

        Assert.True(context.ShouldRenew);
        Assert.Contains(CurrentPrincipal(context).Claims, claim => claim.Type == "permission" && claim.Value == "contracts.write");
        Assert.DoesNotContain(CurrentPrincipal(context).Claims, claim => claim.Type == "permission" && claim.Value == "contracts.read");
    }

    private static (CookieValidatePrincipalContext Context, RecordingAuthenticationService Authentication) CreateContext(ClaimsPrincipal principal)
    {
        var authentication = new RecordingAuthenticationService();
        var services = new ServiceCollection()
            .AddSingleton<IAuthenticationService>(authentication)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var options = new CookieAuthenticationOptions { Cookie = { Name = "uCredit.Auth" } };
        var scheme = new AuthenticationScheme(
            IdentityConstants.ApplicationScheme,
            IdentityConstants.ApplicationScheme,
            typeof(CookieAuthenticationHandler));
        return (new CookieValidatePrincipalContext(httpContext, scheme, options, new AuthenticationTicket(principal, scheme.Name)), authentication);
    }

    private static ClaimsPrincipal CurrentPrincipal(CookieValidatePrincipalContext context) => context.Principal ?? throw new InvalidOperationException("Principal was rejected.");

private static ClaimsPrincipal SelectedPrincipal(MutableTenantMembershipStore store, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(TenantClaimTypes.Id, store.TenantId.ToString("D")),
            new(TenantClaimTypes.Code, store.TenantCode)
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        return UserPrincipal(store.UserId, claims.ToArray());
    }
    private static ClaimsPrincipal UserPrincipal(Guid userId, params Claim[] claims)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")), .. claims],
            IdentityConstants.ApplicationScheme);
        return new ClaimsPrincipal(identity);
    }

    private sealed class MutableTenantMembershipStore : ITenantMembershipStore
    {
        public Guid UserId { get; } = Guid.Parse("50000000-0000-0000-0000-000000000001");
        public Guid TenantId { get; } = Guid.Parse("60000000-0000-0000-0000-000000000001");
        public string TenantCode { get; } = "TENANT-A";
        public bool UserActive { get; init; } = true;
        public bool TenantActive { get; init; } = true;
        public bool MembershipActive { get; init; } = true;
        public IReadOnlyList<string> PermissionCodes { get; init; } = [];

        public Task<ApplicationUser?> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ApplicationUser?>(userId == UserId && UserActive
                ? new ApplicationUser { Id = UserId, UserName = "cookie-test-user", IsActive = true }
                : null);
        }

        public Task<IReadOnlyList<ActiveTenantMembership>> GetActiveMembershipsAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActiveTenantMembership>>([]);

        public Task<ActiveTenantMembership?> FindActiveMembershipAsync(Guid userId, string tenantCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<ActiveTenantMembership?>(null);

        public Task<ActiveTenantMembership?> FindActiveMembershipAsync(Guid userId, Guid tenantId, string tenantCode, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<ActiveTenantMembership?>(
                userId == UserId && tenantId == TenantId &&
                string.Equals(tenantCode, TenantCode, StringComparison.OrdinalIgnoreCase) &&
                TenantActive && MembershipActive
                    ? new ActiveTenantMembership(TenantId, TenantCode, "Tenant A", PermissionCodes)
                    : null);
        }
    }

    private sealed class RecordingAuthenticationService : IAuthenticationService
    {
        public bool SignedOut { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) =>
            Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) =>
            Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOut = true;
            return Task.CompletedTask;
        }
    }
}