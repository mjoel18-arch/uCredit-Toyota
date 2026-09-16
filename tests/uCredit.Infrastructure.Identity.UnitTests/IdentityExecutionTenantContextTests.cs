using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UCredit.Infrastructure.Identity.Models;
using UCredit.Infrastructure.Identity.Tenants;

namespace UCredit.Infrastructure.Identity.UnitTests;

public sealed class IdentityExecutionTenantContextTests
{
    [Fact]
    public async Task ReturnsOnlyActiveServerScopeForDeploymentTenant()
    {
        var membership = ActiveMembership("TENANT-A", [101, 202]);
        var store = new StubTenantMembershipStore(membership);
        var context = CreateContext(store, "TENANT-A", membership.TenantId);

        var result = await context.GetAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(membership.TenantId, result.TenantId);
        Assert.Equal("TENANT-A", result.TenantCode);
        Assert.Equal([101, 202], result.AllowedCompanyIds);
    }

    [Fact]
    public async Task RejectsASelectedTenantOutsideTheDeployment()
    {
        var membership = ActiveMembership("TENANT-B", [303]);
        var store = new StubTenantMembershipStore(membership);
        var context = CreateContext(store, "TENANT-B", membership.TenantId, deploymentCode: "TENANT-A");

        var result = await context.GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, store.FindCalls);
    }

    [Fact]
    public async Task MissingDeploymentCodeFailsClosed()
    {
        var membership = ActiveMembership("TENANT-A", [101]);
        var store = new StubTenantMembershipStore(membership);
        var context = CreateContext(store, "TENANT-A", membership.TenantId, deploymentCode: null);

        var result = await context.GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, store.FindCalls);
    }

    [Fact]
    public async Task MissingActiveCompanyScopeFailsClosed()
    {
        var membership = ActiveMembership("TENANT-A", []);
        var store = new StubTenantMembershipStore(membership);
        var context = CreateContext(store, "TENANT-A", membership.TenantId);

        var result = await context.GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    private static IdentityExecutionTenantContext CreateContext(
        StubTenantMembershipStore store,
        string tenantCode,
        Guid tenantId,
        string? deploymentCode = "TENANT-A")
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, store.UserId.ToString("D")),
                new Claim(TenantClaimTypes.Id, tenantId.ToString("D")),
                new Claim(TenantClaimTypes.Code, tenantCode)
            ],
            "cookie"));
        return new IdentityExecutionTenantContext(
            new HttpContextAccessor { HttpContext = httpContext },
            store,
            new DeploymentTenantPolicy(deploymentCode));
    }

    private static ActiveTenantMembership ActiveMembership(string tenantCode, IReadOnlyList<int> companyIds) =>
        new(
            Guid.Parse("70000000-0000-0000-0000-000000000001"),
            tenantCode,
            tenantCode,
            [],
            companyIds);

    private sealed class StubTenantMembershipStore(ActiveTenantMembership membership) : ITenantMembershipStore
    {
        public Guid UserId { get; } = Guid.Parse("80000000-0000-0000-0000-000000000001");
        public int FindCalls { get; private set; }

        public Task<ApplicationUser?> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<IReadOnlyList<ActiveTenantMembership>> GetActiveMembershipsAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ActiveTenantMembership>>([]);

        public Task<ActiveTenantMembership?> FindActiveMembershipAsync(
            Guid userId,
            string tenantCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ActiveTenantMembership?>(null);

        public Task<ActiveTenantMembership?> FindActiveMembershipAsync(
            Guid userId,
            Guid tenantId,
            string tenantCode,
            CancellationToken cancellationToken = default)
        {
            FindCalls++;
            return Task.FromResult<ActiveTenantMembership?>(
                userId == UserId && tenantId == membership.TenantId &&
                string.Equals(tenantCode, membership.TenantCode, StringComparison.Ordinal)
                    ? membership
                    : null);
        }
    }
}
