using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.Identity.Models;
using UCredit.Infrastructure.Identity.Tenants;

namespace UCredit.Api.IntegrationTests;

public sealed class LocalIdentityApiFactory : WebApplicationFactory<Program>
{
    public const string UserName = "integration-test-user";
    public const string Password = "OnlyTest-Password-123!";
    private readonly string databaseName = $"local-identity-http-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.AddSingleton<ITenantMembershipStore, FakeTenantMembershipStore>();
            services.AddScoped<IdentityCookieEvents>();
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
            services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, IdentityClaimsPrincipalFactory>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "uCredit.Test.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.None;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.LoginPath = "/api/v1/auth/login";
                options.AccessDeniedPath = "/api/v1/auth/forbidden";
                options.EventsType = typeof(IdentityCookieEvents);
            });
        });
    }

    public async Task SeedAdminAsync()
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync(UserName);
        if (user is not null)
            return;

        user = new ApplicationUser
        {
            Id = TestIdentityData.SingleMembershipUserId,
            UserName = UserName,
            Email = "integration-test-user@example.test",
            EmailConfirmed = true,
            DisplayName = "Integration test user",
            IsActive = true,
            LockoutEnabled = true
        };
        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}
