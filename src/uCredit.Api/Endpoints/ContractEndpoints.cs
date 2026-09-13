using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Api.Endpoints;

public static class ContractEndpoints
{
    public static IEndpointRouteBuilder MapContractEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/contracts")
            .WithTags("Contracts")
            .RequireAuthorization("contracts.read");

        group.MapGet("/", SearchContractsAsync)
            .WithName("SearchContracts");

        group.MapGet("/{contractNumber}", GetContractAsync)
            .WithName("GetContract");

        return endpoints;
    }

    private static async Task<IResult> SearchContractsAsync(
        [AsParameters] ContractSearchRequest request,
        IContractReadRepository repository,
        CancellationToken cancellationToken)
    {
        var criteria = request.ToCriteria();
        var errors = ContractSearchValidator.Validate(criteria);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await repository.SearchAsync(criteria, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetContractAsync(
        string contractNumber,
        IContractReadRepository repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contractNumber) || contractNumber.Length > 15)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["contractNumber"] = ["Contract number is required and must not exceed 15 characters."]
            });
        }

        var contract = await repository.GetByNumberAsync(contractNumber, cancellationToken);
        return contract is null ? Results.NotFound() : Results.Ok(contract);
    }
}

public sealed record ContractSearchRequest(
    string? ContractNumber,
    int? PersonId,
    string? Rfc,
    string? PersonName,
    string? Vin,
    string? ApplicationNumber,
    string? OperationType,
    int? Status,
    int Page = 1,
    int PageSize = 20,
    ContractSort Sort = ContractSort.ContractNumberAsc)
{
    public ContractSearchCriteria ToCriteria() => new(
        ContractNumber,
        PersonId,
        Rfc,
        PersonName,
        Vin,
        ApplicationNumber,
        OperationType,
        Status,
        Page,
        PageSize,
        Sort);
}
