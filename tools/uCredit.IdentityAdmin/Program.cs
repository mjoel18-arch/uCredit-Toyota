using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.Identity.Models;

namespace UCredit.IdentityAdmin;

public sealed class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var input = BootstrapConfiguration.Load(args);
            var validation = BootstrapValidator.Validate(input);
            if (!validation.IsValid)
            {
                foreach (var error in validation.Errors)
                    Console.Error.WriteLine($"Bootstrap rejected: {error}");
                return 2;
            }

            await using var provider = BuildServices(validation.Options!.IdentityConnectionString);
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var bootstrapper = new IdentityBootstrapper(new EfIdentityMigrationReadiness());
            var result = await bootstrapper.ExecuteAsync(validation.Options, context, userManager);

            Console.WriteLine("Identity bootstrap completed.");
            foreach (var action in result.Actions)
                Console.WriteLine(BootstrapOutput.Format(action));
            return 0;
        }
        catch (BootstrapException exception)
        {
            Console.Error.WriteLine($"Bootstrap rejected: {exception.Message}");
            return 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Bootstrap failed without exposing connection or credential details.");
            return 1;
        }
    }

    private static ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
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
        .AddEntityFrameworkStores<IdentityDbContext>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}

