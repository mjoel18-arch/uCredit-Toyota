namespace UCredit.Application.Execution;

public sealed record ExecutionTenant(
    Guid TenantId,
    string TenantCode,
    IReadOnlyList<int> AllowedCompanyIds);

public interface IExecutionTenantContext
{
    Task<ExecutionTenant?> GetAsync(CancellationToken cancellationToken = default);
}