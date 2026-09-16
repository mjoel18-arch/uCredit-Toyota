using Xunit;
using Microsoft.EntityFrameworkCore;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.Identity.Models;
using UCredit.Infrastructure.Identity.Tenants;

namespace UCredit.Infrastructure.Identity.UnitTests;

public sealed class EfTenantMembershipStoreTests
{
    [Fact]
    public async Task ActiveMembershipsReturnOnlyPermissionsAndActiveCompanyScopesFromEachMembership()
    {
        await using var context = await CreateContextAsync(TestContext.Current.CancellationToken);
        var store = new EfTenantMembershipStore(context);

        var memberships = await store.GetActiveMembershipsAsync(TestData.UserId, TestContext.Current.CancellationToken);

        Assert.Equal(2, memberships.Count);
        var tenantA = memberships.Single(membership => membership.TenantCode == "TENANT-A");
        var tenantB = memberships.Single(membership => membership.TenantCode == "TENANT-B");
        Assert.Equal(["contracts.read"], tenantA.PermissionCodes);
        Assert.Equal([101, 102], tenantA.AllowedCompanyIds);
        Assert.Equal(["contracts.write"], tenantB.PermissionCodes);
        Assert.Equal([201], tenantB.AllowedCompanyIds);
    }

    [Fact]
    public async Task InactiveTenantAndMembershipAreNotSelectable()
    {
        await using var context = await CreateContextAsync(TestContext.Current.CancellationToken);
        var store = new EfTenantMembershipStore(context);

        var inactiveTenant = await store.FindActiveMembershipAsync(TestData.UserId, "TENANT-INACTIVE", TestContext.Current.CancellationToken);
        var inactiveMembership = await store.FindActiveMembershipAsync(TestData.UserId, "TENANT-INACTIVE-MEMBERSHIP", TestContext.Current.CancellationToken);

        Assert.Null(inactiveTenant);
        Assert.Null(inactiveMembership);
    }

    [Fact]
    public async Task InactiveCompanyScopeIsExcluded()
    {
        await using var context = await CreateContextAsync(TestContext.Current.CancellationToken);
        var store = new EfTenantMembershipStore(context);

        var membership = await store.FindActiveMembershipAsync(TestData.UserId, "TENANT-A", TestContext.Current.CancellationToken);

        Assert.NotNull(membership);
        Assert.Equal([101, 102], membership.AllowedCompanyIds);
    }

    [Fact]
    public async Task InactiveUserHasNoActiveIdentityForMeOrSelection()
    {
        await using var context = await CreateContextAsync(TestContext.Current.CancellationToken);
        var store = new EfTenantMembershipStore(context);

        var user = await store.GetActiveUserAsync(TestData.InactiveUserId, TestContext.Current.CancellationToken);
        var memberships = await store.GetActiveMembershipsAsync(TestData.InactiveUserId, TestContext.Current.CancellationToken);

        Assert.Null(user);
        Assert.Empty(memberships);
    }

    [Fact]
    public async Task TenantCodeSelectionIsCaseInsensitiveButMembershipBound()
    {
        await using var context = await CreateContextAsync(TestContext.Current.CancellationToken);
        var store = new EfTenantMembershipStore(context);

        var membership = await store.FindActiveMembershipAsync(TestData.UserId, "tenant-a", TestContext.Current.CancellationToken);
        var foreign = await store.FindActiveMembershipAsync(TestData.OtherUserId, "TENANT-A", TestContext.Current.CancellationToken);

        Assert.NotNull(membership);
        Assert.Equal("TENANT-A", membership.TenantCode);
        Assert.Null(foreign);
    }

    [Fact]
    public void SelectionClaimsContainOnlyTheSelectedMembershipPermissions()
    {
        var membership = new ActiveTenantMembership(
            Guid.NewGuid(),
            "TENANT-B",
            "Tenant B",
            ["contracts.write"],
            [201]);

        var claims = TenantSelectionClaims.Build(membership);

        Assert.Contains(claims, claim => claim.Type == TenantClaimTypes.Code && claim.Value == "TENANT-B");
        Assert.Contains(claims, claim => claim.Type == "permission" && claim.Value == "contracts.write");
        Assert.DoesNotContain(claims, claim => claim.Type == "permission" && claim.Value == "contracts.read");
    }

    [Fact]
    public void ModelDefinesUniqueCodesCompositeKeysForeignKeysAndCompanyScope()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using var context = new IdentityDbContext(options);

        var tenant = context.Model.FindEntityType(typeof(Tenant))!;
        var permission = context.Model.FindEntityType(typeof(Permission))!;
        var membership = context.Model.FindEntityType(typeof(UserTenantMembership))!;
        var membershipPermission = context.Model.FindEntityType(typeof(MembershipPermission))!;
        var companyScope = context.Model.FindEntityType(typeof(TenantLegacyCompanyScope))!;

        var tenantCodeIndex = tenant.GetIndexes().Single(index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Tenant.Code)]));
        var permissionCodeIndex = permission.GetIndexes().Single(index => index.Properties.Select(property => property.Name).SequenceEqual([nameof(Permission.Code)]));
        Assert.True(tenantCodeIndex.IsUnique);
        Assert.True(permissionCodeIndex.IsUnique);
        Assert.Equal(["UserId", "TenantId"], membership.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(["UserId", "TenantId", "PermissionId"], membershipPermission.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(["TenantId", "CompanyId"], companyScope.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Single(companyScope.GetForeignKeys());
        Assert.Equal(DeleteBehavior.Restrict, companyScope.GetForeignKeys().Single().DeleteBehavior);
        Assert.Equal(2, membership.GetForeignKeys().Count());
        Assert.Equal(2, membershipPermission.GetForeignKeys().Count());
        Assert.All(membership.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.All(membershipPermission.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    private static async Task<IdentityDbContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var context = new IdentityDbContext(options);

        var read = new Permission { Id = TestData.ReadPermissionId, Code = "contracts.read", Name = "Read contracts" };
        var write = new Permission { Id = TestData.WritePermissionId, Code = "contracts.write", Name = "Write contracts" };
        var tenantA = new Tenant { Id = TestData.TenantAId, Code = "TENANT-A", Name = "Tenant A", IsActive = true };
        var tenantB = new Tenant { Id = TestData.TenantBId, Code = "TENANT-B", Name = "Tenant B", IsActive = true };
        var inactiveTenant = new Tenant { Id = TestData.InactiveTenantId, Code = "TENANT-INACTIVE", Name = "Inactive tenant", IsActive = false };
        var inactiveMembershipTenant = new Tenant { Id = TestData.InactiveMembershipTenantId, Code = "TENANT-INACTIVE-MEMBERSHIP", Name = "Inactive membership tenant", IsActive = true };
        var user = new ApplicationUser { Id = TestData.UserId, UserName = "active-user", IsActive = true };
        var otherUser = new ApplicationUser { Id = TestData.OtherUserId, UserName = "other-user", IsActive = true };
        var inactiveUser = new ApplicationUser { Id = TestData.InactiveUserId, UserName = "inactive-user", IsActive = false };

        context.AddRange(user, otherUser, inactiveUser, tenantA, tenantB, inactiveTenant, inactiveMembershipTenant, read, write);
        context.AddRange(
            new TenantLegacyCompanyScope { TenantId = tenantA.Id, Tenant = tenantA, CompanyId = 101, DisplayName = "Company 101", IsActive = true },
            new TenantLegacyCompanyScope { TenantId = tenantA.Id, Tenant = tenantA, CompanyId = 102, DisplayName = "Company 102", IsActive = true },
            new TenantLegacyCompanyScope { TenantId = tenantA.Id, Tenant = tenantA, CompanyId = 103, DisplayName = "Inactive company", IsActive = false },
            new TenantLegacyCompanyScope { TenantId = tenantB.Id, Tenant = tenantB, CompanyId = 201, DisplayName = "Company 201", IsActive = true });
        context.AddRange(
            new UserTenantMembership { UserId = user.Id, User = user, TenantId = tenantA.Id, Tenant = tenantA, IsActive = true },
            new UserTenantMembership { UserId = user.Id, User = user, TenantId = tenantB.Id, Tenant = tenantB, IsActive = true },
            new UserTenantMembership { UserId = user.Id, User = user, TenantId = inactiveTenant.Id, Tenant = inactiveTenant, IsActive = true },
            new UserTenantMembership { UserId = user.Id, User = user, TenantId = inactiveMembershipTenant.Id, Tenant = inactiveMembershipTenant, IsActive = false },
            new UserTenantMembership { UserId = otherUser.Id, User = otherUser, TenantId = tenantB.Id, Tenant = tenantB, IsActive = true });
        context.AddRange(
            new MembershipPermission { UserId = user.Id, TenantId = tenantA.Id, PermissionId = read.Id, Permission = read },
            new MembershipPermission { UserId = user.Id, TenantId = tenantB.Id, PermissionId = write.Id, Permission = write });
        await context.SaveChangesAsync(cancellationToken);

        return context;
    }

    private static class TestData
    {
        public static readonly Guid UserId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid OtherUserId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid InactiveUserId = Guid.Parse("20000000-0000-0000-0000-000000000003");
        public static readonly Guid TenantAId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        public static readonly Guid TenantBId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        public static readonly Guid InactiveTenantId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        public static readonly Guid InactiveMembershipTenantId = Guid.Parse("30000000-0000-0000-0000-000000000004");
        public static readonly Guid ReadPermissionId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public static readonly Guid WritePermissionId = Guid.Parse("40000000-0000-0000-0000-000000000002");
    }
}
