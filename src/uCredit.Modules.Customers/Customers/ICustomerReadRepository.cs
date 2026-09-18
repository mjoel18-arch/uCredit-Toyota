namespace UCredit.Modules.Customers.Customers;

public interface ICustomerReadRepository
{
    Task<PagedCustomers> SearchAsync(CustomerSearchCriteria criteria, CancellationToken cancellationToken = default);

    Task<Customer?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default);
}

public sealed record PagedCustomers(
    IReadOnlyList<Customer> Items,
    int Page,
    int PageSize,
    int Total);
