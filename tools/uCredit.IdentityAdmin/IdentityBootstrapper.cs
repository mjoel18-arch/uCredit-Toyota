using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.Identity.Models;

namespace UCredit.IdentityAdmin;

public sealed record BootstrapAction(
    string ObjectType,
    Guid PrimaryId,
    Guid? SecondaryId,
    bool Created)
{
    public int? NumericIdentifier { get; init; }
    public bool Reactivated { get; init; }
}

public sealed class BootstrapResult
{
    public BootstrapResult(IReadOnlyList<BootstrapAction> actions)
    {
        Actions = actions;
    }

    public IReadOnlyList<BootstrapAction> Actions { get; }
}

public sealed class IdentityBootstrapper(IIdentityMigrationReadiness migrationReadiness)
{
    private const string PermissionCode = "contracts.read";
    private const string PermissionName = "Read contracts";
    private const string DevelopmentAdminDisplayName = "Development Administrator";

    public async Task<BootstrapResult> ExecuteAsync(
        BootstrapOptions options,
        IdentityDbContext context,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        if (!options.Apply)
            throw new BootstrapException("The --apply argument is required.");

        await migrationReadiness.EnsureInitialIdentityAppliedAsync(context, cancellationToken);

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var actions = new List<BootstrapAction>();
            var tenant = await FindTenantAsync(context, options.TenantCode, cancellationToken);
            if (tenant is null)
            {
                tenant = new Tenant
                {
                    Id = Guid.NewGuid(),
                    Code = options.TenantCode,
                    Name = options.TenantName,
                    IsActive = true
                };
                context.Tenants.Add(tenant);
                actions.Add(new BootstrapAction("Tenant", tenant.Id, null, true));
            }
            else
            {
                if (!tenant.IsActive || !string.Equals(tenant.Name, options.TenantName, StringComparison.Ordinal))
                    throw new BootstrapException("The development tenant exists but is not valid for bootstrap.");

                actions.Add(new BootstrapAction("Tenant", tenant.Id, null, false));
            }

            var permission = await FindPermissionAsync(context, PermissionCode, cancellationToken);
            if (permission is null)
            {
                permission = new Permission
                {
                    Id = Guid.NewGuid(),
                    Code = PermissionCode,
                    Name = PermissionName
                };
                context.Permissions.Add(permission);
                actions.Add(new BootstrapAction("Permission", permission.Id, null, true));
            }
            else
            {
                actions.Add(new BootstrapAction("Permission", permission.Id, null, false));
            }

            await context.SaveChangesAsync(cancellationToken);

            var user = await userManager.FindByEmailAsync(options.AdminEmail);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = options.AdminEmail,
                    Email = options.AdminEmail,
                    EmailConfirmed = true,
                    DisplayName = DevelopmentAdminDisplayName,
                    IsActive = true,
                    LockoutEnabled = true
                };

                var creation = await userManager.CreateAsync(user, options.AdminPassword);
                if (!creation.Succeeded)
                {
                    var codes = string.Join(", ", creation.Errors.Select(error => error.Code));
                    throw new BootstrapException($"The development administrator could not be created ({codes}).");
                }

                actions.Add(new BootstrapAction("ApplicationUser", user.Id, null, true));
            }
            else
            {
                if (!user.IsActive)
                    throw new BootstrapException("The development administrator exists but is inactive.");

                actions.Add(new BootstrapAction("ApplicationUser", user.Id, null, false));
            }

            var membership = await context.UserTenantMemberships
                .SingleOrDefaultAsync(
                    candidate => candidate.UserId == user.Id && candidate.TenantId == tenant.Id,
                    cancellationToken);
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
                actions.Add(new BootstrapAction("UserTenantMembership", user.Id, tenant.Id, true));
            }
            else
            {
                if (!membership.IsActive)
                    throw new BootstrapException("The development administrator membership exists but is inactive.");

                actions.Add(new BootstrapAction("UserTenantMembership", user.Id, tenant.Id, false));
            }

            var assignment = await context.MembershipPermissions
                .SingleOrDefaultAsync(
                    candidate => candidate.UserId == user.Id &&
                        candidate.TenantId == tenant.Id &&
                        candidate.PermissionId == permission.Id,
                    cancellationToken);
            if (assignment is null)
            {
                context.MembershipPermissions.Add(new MembershipPermission
                {
                    UserId = user.Id,
                    TenantId = tenant.Id,
                    PermissionId = permission.Id,
                    Membership = membership,
                    Permission = permission
                });
                actions.Add(new BootstrapAction("MembershipPermission", user.Id, tenant.Id, true));
            }
            else
            {
                actions.Add(new BootstrapAction("MembershipPermission", user.Id, tenant.Id, false));
            }

            foreach (var companyId in options.CompanyIds)
            {
                var scope = await context.TenantLegacyCompanyScopes
                    .SingleOrDefaultAsync(
                        candidate => candidate.TenantId == tenant.Id && candidate.CompanyId == companyId,
                        cancellationToken);

                if (scope is null)
                {
                    context.TenantLegacyCompanyScopes.Add(new TenantLegacyCompanyScope
                    {
                        TenantId = tenant.Id,
                        Tenant = tenant,
                        CompanyId = companyId,
                        DisplayName = null,
                        IsActive = true
                    });
                    actions.Add(new BootstrapAction("TenantLegacyCompanyScope", tenant.Id, null, true)
                    {
                        NumericIdentifier = companyId
                    });
                }
                else if (!scope.IsActive)
                {
                    scope.IsActive = true;
                    actions.Add(new BootstrapAction("TenantLegacyCompanyScope", tenant.Id, null, false)
                    {
                        NumericIdentifier = companyId,
                        Reactivated = true
                    });
                }
                else
                {
                    actions.Add(new BootstrapAction("TenantLegacyCompanyScope", tenant.Id, null, false)
                    {
                        NumericIdentifier = companyId
                    });
                }
            }

            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return new BootstrapResult(actions);
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<Tenant?> FindTenantAsync(
        IdentityDbContext context,
        string code,
        CancellationToken cancellationToken)
    {
        var tenants = await context.Tenants.ToListAsync(cancellationToken);
        return tenants.SingleOrDefault(tenant =>
            string.Equals(tenant.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Permission?> FindPermissionAsync(
        IdentityDbContext context,
        string code,
        CancellationToken cancellationToken)
    {
        var permissions = await context.Permissions.ToListAsync(cancellationToken);
        return permissions.SingleOrDefault(permission =>
            string.Equals(permission.Code, code, StringComparison.OrdinalIgnoreCase));
    }
}

public static class BootstrapOutput
{
    public static string Format(BootstrapAction action)
    {
        var identifier = action.NumericIdentifier is not null
            ? $"{action.PrimaryId:D}/{action.NumericIdentifier.Value}"
            : action.SecondaryId is null
                ? action.PrimaryId.ToString("D")
                : $"{action.PrimaryId:D}/{action.SecondaryId.Value:D}";
        var state = action.Reactivated
            ? "reactivated"
            : action.Created
                ? "created"
                : "already existed";
        return $"{action.ObjectType} {identifier}: {state}";
    }
}
