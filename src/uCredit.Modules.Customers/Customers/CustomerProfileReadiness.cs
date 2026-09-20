namespace UCredit.Modules.Customers.Customers;

public enum CustomerProfileRequirement
{
    GeneralData,
    Address,
    Phone,
    Account,
}

public sealed record CustomerProfileReadiness(
    int PersonId,
    bool HasGeneralData,
    bool HasAddress,
    bool HasPhone,
    bool HasAccount)
{
    public bool CanCreateContract => HasGeneralData && HasAddress && HasPhone && HasAccount;

    public IReadOnlyList<CustomerProfileRequirement> MissingRequirements =>
        new[]
        {
            (CustomerProfileRequirement.GeneralData, HasGeneralData),
            (CustomerProfileRequirement.Address, HasAddress),
            (CustomerProfileRequirement.Phone, HasPhone),
            (CustomerProfileRequirement.Account, HasAccount),
        }
        .Where(item => !item.Item2)
        .Select(item => item.Item1)
        .ToArray();
}

public interface ICustomerProfileReadinessRepository
{
    Task<CustomerProfileReadiness?> GetAsync(int personId, CancellationToken cancellationToken = default);
}

public interface ICustomerProfileReadinessService
{
    Task<CustomerProfileReadiness?> GetAsync(int personId, CancellationToken cancellationToken = default);
}

public sealed class CustomerProfileReadinessService(ICustomerProfileReadinessRepository repository)
    : ICustomerProfileReadinessService
{
    public Task<CustomerProfileReadiness?> GetAsync(int personId, CancellationToken cancellationToken = default) =>
        repository.GetAsync(personId, cancellationToken);
}
