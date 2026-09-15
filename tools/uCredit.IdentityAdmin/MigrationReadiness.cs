using Microsoft.EntityFrameworkCore;
using UCredit.Infrastructure.Identity;

namespace UCredit.IdentityAdmin;

public sealed class BootstrapException(string message) : Exception(message);

public interface IIdentityMigrationReadiness
{
    Task EnsureInitialIdentityAppliedAsync(
        IdentityDbContext context,
        CancellationToken cancellationToken = default);
}

public sealed class EfIdentityMigrationReadiness : IIdentityMigrationReadiness
{
    public const string InitialIdentityMigrationName = "InitialIdentity";

    public async Task EnsureInitialIdentityAppliedAsync(
        IdentityDbContext context,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> appliedMigrations;
        try
        {
            appliedMigrations = (await context.Database
                .GetAppliedMigrationsAsync(cancellationToken))
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new BootstrapException("The Identity database is unavailable or not prepared.");
        }

        if (!appliedMigrations.Any(IsInitialIdentityMigration))
            throw new BootstrapException("InitialIdentity must already be applied to the Identity database.");
    }

    private static bool IsInitialIdentityMigration(string migrationId) =>
        migrationId.EndsWith($"_{InitialIdentityMigrationName}", StringComparison.Ordinal);
}
