using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UCredit.Infrastructure.Identity;

/// <summary>
/// Supplies provider metadata for EF tooling without opening a database connection.
/// Runtime and controlled database operations use IdentitySql__ConnectionString.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=(design-time-placeholder);Database=uCreditIdentity_Dev;Trusted_Connection=True;TrustServerCertificate=True;";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        var configuredConnectionString =
            Environment.GetEnvironmentVariable("IdentitySql__ConnectionString");

        var connectionString = string.IsNullOrWhiteSpace(configuredConnectionString)
            ? DesignTimeConnectionString
            : configuredConnectionString;

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsAssembly(typeof(IdentityDbContext).Assembly.FullName))
            .Options;

        return new IdentityDbContext(options);
    }
}
