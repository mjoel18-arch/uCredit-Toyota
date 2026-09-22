namespace UCredit.Modules.Customers.Customers;

public sealed record CustomerRoleOption(int RoleCode, string RoleName);

public sealed class CustomerRoleCatalogConflictException(string message) : Exception(message);

public interface ICustomerRoleCatalogRepository
{
    Task<IReadOnlyList<CustomerRoleOption>> GetActiveAsync(CancellationToken cancellationToken = default);
}
