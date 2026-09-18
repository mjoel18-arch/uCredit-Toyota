using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UCredit.IdentityAdmin;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.Identity.Models;
using Xunit;

namespace UCredit.IdentityAdmin.UnitTests;

public sealed class IdentityBootstrapperTests
{
    [Fact]
    public async Task ProvisioningCreatesBothPermissionsAndIsIdempotent()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new AlwaysReadyMigration());
        var options = CreateOptions();

        var first = await bootstrapper.ExecuteAsync(options, context, userManager, TestContext.Current.CancellationToken);
        var second = await bootstrapper.ExecuteAsync(options, context, userManager, TestContext.Current.CancellationToken);

        Assert.Equal(8, first.Actions.Count);
        Assert.All(first.Actions, action => Assert.True(action.Created));
        Assert.Equal(8, second.Actions.Count);
        Assert.All(second.Actions, action => Assert.False(action.Created));
        Assert.Single(await context.Tenants.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(["contracts.read", "customers.read"], await context.Permissions
            .OrderBy(permission => permission.Code)
            .Select(permission => permission.Code)
            .ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.UserTenantMemberships.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.MembershipPermissions.CountAsync(TestContext.Current.CancellationToken));
        var companyScope = Assert.Single(await context.TenantLegacyCompanyScopes.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, companyScope.CompanyId);
        Assert.True(companyScope.IsActive);
    }

    [Fact]
    public async Task ProvisioningPreservesExistingPermissionsAndDoesNotAssignAcrossTenants()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new AlwaysReadyMigration());

        await bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken);
        var developmentTenant = await context.Tenants.SingleAsync(TestContext.Current.CancellationToken);
        var developmentUser = await userManager.FindByEmailAsync("admin@example.test");
        var contractsPermission = await context.Permissions.SingleAsync(item => item.Code == "contracts.read", TestContext.Current.CancellationToken);
        var existingPermission = new Permission { Id = Guid.NewGuid(), Code = "reports.read", Name = "Read reports" };
        context.Permissions.Add(existingPermission);
        context.MembershipPermissions.Add(new MembershipPermission
        {
            UserId = developmentUser!.Id,
            TenantId = developmentTenant.Id,
            PermissionId = existingPermission.Id
        });

        var otherTenant = new Tenant { Id = Guid.NewGuid(), Code = "OTHER", Name = "Other tenant", IsActive = true };
        var otherUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "other@example.test",
            Email = "other@example.test",
            EmailConfirmed = true,
            DisplayName = "Other administrator",
            IsActive = true
        };
        context.Tenants.Add(otherTenant);
        var creation = await userManager.CreateAsync(otherUser, "OnlyTest-Password-123!");
        Assert.True(creation.Succeeded);
        context.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = otherUser.Id,
            User = otherUser,
            TenantId = otherTenant.Id,
            Tenant = otherTenant,
            IsActive = true
        });
        context.MembershipPermissions.Add(new MembershipPermission
        {
            UserId = otherUser.Id,
            TenantId = otherTenant.Id,
            PermissionId = contractsPermission.Id
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken);

        Assert.Contains(await context.MembershipPermissions.ToListAsync(TestContext.Current.CancellationToken), assignment =>
            assignment.UserId == developmentUser.Id && assignment.TenantId == developmentTenant.Id && assignment.PermissionId == existingPermission.Id);
        Assert.Single(await context.MembershipPermissions.Where(assignment => assignment.TenantId == otherTenant.Id)
            .ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProvisioningCreatesSeveralCompanyScopesWithoutRemovingUnlistedScopes()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new AlwaysReadyMigration());

        await bootstrapper.ExecuteAsync(CreateOptions(companyIds: [1]), context, userManager, TestContext.Current.CancellationToken);
        var tenant = await context.Tenants.SingleAsync(TestContext.Current.CancellationToken);
        context.TenantLegacyCompanyScopes.Add(new TenantLegacyCompanyScope
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            CompanyId = 9,
            IsActive = true
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await bootstrapper.ExecuteAsync(
            CreateOptions(companyIds: [1, 2, 3]),
            context,
            userManager,
            TestContext.Current.CancellationToken);

        Assert.Equal(4, await context.TenantLegacyCompanyScopes.CountAsync(TestContext.Current.CancellationToken));
        Assert.Contains(result.Actions, action => action.ObjectType == "TenantLegacyCompanyScope" && action.NumericIdentifier == 2 && action.Created);
        Assert.Contains(result.Actions, action => action.ObjectType == "TenantLegacyCompanyScope" && action.NumericIdentifier == 3 && action.Created);
        Assert.Contains(await context.TenantLegacyCompanyScopes.Select(item => item.CompanyId).ToListAsync(TestContext.Current.CancellationToken), companyId => companyId == 9);
    }

    [Fact]
    public async Task ProvisioningReactivatesAnExistingInactiveScope()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new AlwaysReadyMigration());

        await bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken);
        var companyScope = Assert.Single(await context.TenantLegacyCompanyScopes.ToListAsync(TestContext.Current.CancellationToken));
        companyScope.IsActive = false;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken);

        var action = Assert.Single(result.Actions, item => item.ObjectType == "TenantLegacyCompanyScope");
        Assert.False(action.Created);
        Assert.True(action.Reactivated);
        Assert.True((await context.TenantLegacyCompanyScopes.SingleAsync(TestContext.Current.CancellationToken)).IsActive);
        Assert.Contains("reactivated", BootstrapOutput.Format(action), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingUserPasswordIsNeverChanged()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new AlwaysReadyMigration());

        await bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken);
        var existingUser = await userManager.FindByEmailAsync("admin@example.test");
        var originalHash = existingUser!.PasswordHash;

        await bootstrapper.ExecuteAsync(
            CreateOptions(password: "A-Different-Test-Password-123!"),
            context,
            userManager,
            TestContext.Current.CancellationToken);

        var unchangedUser = await userManager.FindByEmailAsync("admin@example.test");
        Assert.Equal(originalHash, unchangedUser!.PasswordHash);
    }

    [Fact]
    public async Task RefusesToProvisionWhenInitialMigrationIsNotApplied()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new MigrationNotReady("InitialIdentity"));

        await Assert.ThrowsAsync<BootstrapException>(() =>
            bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken));
        Assert.Empty(await context.Tenants.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RefusesToProvisionWhenCompanyScopeMigrationIsNotApplied()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new MigrationNotReady("AddTenantLegacyCompanyScope"));

        var exception = await Assert.ThrowsAsync<BootstrapException>(() =>
            bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken));

        Assert.Contains("AddTenantLegacyCompanyScope", exception.Message, StringComparison.Ordinal);
        Assert.Empty(await context.Tenants.ToListAsync(TestContext.Current.CancellationToken));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<IdentityDbContext>();
        return services.BuildServiceProvider();
    }

    private static BootstrapOptions CreateOptions(
        string password = "OnlyTest-Password-123!",
        IReadOnlyList<int>? companyIds = null) => new()
    {
        Apply = true,
        IdentityConnectionString = "Server=(local);Database=uCreditIdentity_Dev;Trusted_Connection=True;",
        TenantCode = "DEV",
        TenantName = "Development tenant",
        AdminEmail = "admin@example.test",
        AdminPassword = password,
        CompanyIds = companyIds ?? [1]
    };

    private sealed class AlwaysReadyMigration : IIdentityMigrationReadiness
    {
        public Task EnsureInitialIdentityAppliedAsync(
            IdentityDbContext context,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MigrationNotReady(string migrationName) : IIdentityMigrationReadiness
    {
        public Task EnsureInitialIdentityAppliedAsync(
            IdentityDbContext context,
            CancellationToken cancellationToken = default) =>
            throw new BootstrapException($"{migrationName} must already be applied to the Identity database.");
    }
}
