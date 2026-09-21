using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UCredit.Application.Execution;
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
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Deployment:TenantCode"] = "ubimia-dev"
            }));
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.AddScoped<ITenantMembershipStore, EfTenantMembershipStore>();
            services.AddScoped<IExecutionTenantContext, IdentityExecutionTenantContext>();
            services.AddSingleton<IDeploymentTenantPolicy>(serviceProvider =>
                new DeploymentTenantPolicy(serviceProvider.GetRequiredService<IConfiguration>()));
            services.AddScoped<IdentityCookieEvents>();
            services.AddScoped<ITenantCookieIssuer, IdentityTenantCookieIssuer>();
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
        await EnsureAdminAsync(userManager);
    }

    public async Task SeedDeploymentTenantAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await EnsureAdminAsync(userManager);

        var deploymentTenant = await context.Tenants
            .SingleOrDefaultAsync(tenant => tenant.Code == "UBIMIA-DEV");
        if (deploymentTenant is null)
        {
            deploymentTenant = new Tenant
            {
                Id = TestIdentityData.DeploymentTenantId,
                Code = "UBIMIA-DEV",
                Name = "Ubimia development tenant",
                IsActive = true
            };
            context.Tenants.Add(deploymentTenant);
        }

        var foreignTenant = await context.Tenants
            .SingleOrDefaultAsync(tenant => tenant.Code == "OTHER-TENANT");
        if (foreignTenant is null)
        {
            foreignTenant = new Tenant
            {
                Id = TestIdentityData.OtherTenantId,
                Code = "OTHER-TENANT",
                Name = "Other tenant",
                IsActive = true
            };
            context.Tenants.Add(foreignTenant);
        }

        var permission = await context.Permissions
            .SingleOrDefaultAsync(candidate => candidate.Code == "contracts.read");
        if (permission is null)
        {
            permission = new Permission
            {
                Id = TestIdentityData.DeploymentPermissionId,
                Code = "contracts.read",
                Name = "Read contracts"
            };
            context.Permissions.Add(permission);
        }

        await context.SaveChangesAsync();
        await EnsureMembershipAsync(context, user, deploymentTenant, permission);
        await EnsureMembershipAsync(context, user, foreignTenant, permission);
        var companyScope = await context.TenantLegacyCompanyScopes
            .SingleOrDefaultAsync(candidate =>
                candidate.TenantId == deploymentTenant.Id && candidate.CompanyId == 1);
        if (companyScope is null)
        {
            context.TenantLegacyCompanyScopes.Add(new TenantLegacyCompanyScope
            {
                TenantId = deploymentTenant.Id,
                Tenant = deploymentTenant,
                CompanyId = 1,
                DisplayName = null,
                IsActive = true
            });
        }
        else
        {
            companyScope.IsActive = true;
        }

        await context.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var user = await userManager.FindByNameAsync(UserName);
        if (user is not null)
            return user;

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

        return user;
    }

    private static async Task EnsureMembershipAsync(
        IdentityDbContext context,
        ApplicationUser user,
        Tenant tenant,
        Permission permission)
    {
        var membership = await context.UserTenantMemberships
            .SingleOrDefaultAsync(candidate => candidate.UserId == user.Id && candidate.TenantId == tenant.Id);
        if (membership is null)
        {
            membership = new UserTenantMembership
            {
                UserId = user.Id,
                User = user,
                TenantId = tenant.Id,
                Tenant = tenant,
                IsActive = true
            };
            context.UserTenantMemberships.Add(membership);
        }
        else
        {
            membership.IsActive = true;
        }

        await context.SaveChangesAsync();
        var assignment = await context.MembershipPermissions
            .SingleOrDefaultAsync(candidate =>
                candidate.UserId == user.Id &&
                candidate.TenantId == tenant.Id &&
                candidate.PermissionId == permission.Id);
        if (assignment is null)
        {
            context.MembershipPermissions.Add(new MembershipPermission
            {
                UserId = user.Id,
                TenantId = tenant.Id,
                Membership = membership,
                PermissionId = permission.Id,
                Permission = permission
            });
            await context.SaveChangesAsync();
        }
    }
}
