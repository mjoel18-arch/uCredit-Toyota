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
    public async Task ProvisioningIsIdempotent()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var bootstrapper = new IdentityBootstrapper(new AlwaysReadyMigration());
        var options = CreateOptions();

        var first = await bootstrapper.ExecuteAsync(options, context, userManager, TestContext.Current.CancellationToken);
        var second = await bootstrapper.ExecuteAsync(options, context, userManager, TestContext.Current.CancellationToken);

        Assert.Equal(5, first.Actions.Count);
        Assert.All(first.Actions, action => Assert.True(action.Created));
        Assert.Equal(5, second.Actions.Count);
        Assert.All(second.Actions, action => Assert.False(action.Created));
        Assert.Single(await context.Tenants.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.Permissions.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.Users.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.UserTenantMemberships.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await context.MembershipPermissions.ToListAsync(TestContext.Current.CancellationToken));
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
            CreateOptions("A-Different-Test-Password-123!"),
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
        var bootstrapper = new IdentityBootstrapper(new MigrationNotReady());

        await Assert.ThrowsAsync<BootstrapException>(() =>
            bootstrapper.ExecuteAsync(CreateOptions(), context, userManager, TestContext.Current.CancellationToken));
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

    private static BootstrapOptions CreateOptions(string password = "OnlyTest-Password-123!") => new()
    {
        Apply = true,
        IdentityConnectionString = "Server=(local);Database=uCreditIdentity_Dev;Trusted_Connection=True;",
        TenantCode = "DEV",
        TenantName = "Development tenant",
        AdminEmail = "admin@example.test",
        AdminPassword = password
    };

    private sealed class AlwaysReadyMigration : IIdentityMigrationReadiness
    {
        public Task EnsureInitialIdentityAppliedAsync(
            IdentityDbContext context,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class MigrationNotReady : IIdentityMigrationReadiness
    {
        public Task EnsureInitialIdentityAppliedAsync(
            IdentityDbContext context,
            CancellationToken cancellationToken = default) =>
            throw new BootstrapException("InitialIdentity must already be applied to the Identity database.");
    }
}


