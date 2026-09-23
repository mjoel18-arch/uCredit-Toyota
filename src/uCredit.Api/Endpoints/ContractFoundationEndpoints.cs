using Microsoft.AspNetCore.Mvc;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Api.Endpoints;

public static class ContractFoundationEndpoints
{
    public static IEndpointRouteBuilder MapContractFoundationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapGroup("/api/v1/contracts/catalogs")
            .WithTags("Contracts")
            .RequireAuthorization("contracts.read");

        read.MapGet("/operations", GetOperationsAsync);
        read.MapGet("/cnbv", GetCnbvAsync);
        read.MapGet("/cfdi-uses", GetCfdiUsesAsync);
        read.MapGet("/addresses", GetAddressesAsync);
        read.MapGet("/ordinary-rate", GetOrdinaryRateAsync);
        read.MapGet("/late-rate", GetLateRateAsync);

        endpoints.MapPost("/api/v1/contracts/preview", PreviewAsync)
            .WithTags("Contracts")
            .RequireAuthorization("contracts.read")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        return endpoints;
    }

    private static async Task<IResult> GetOperationsAsync(
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IContractFoundationRepository repository,
        CancellationToken cancellationToken)
    {
        var tenant = await GetTenantAsync(tenantContext, configuration, cancellationToken);
        return tenant is null
            ? Results.Forbid()
            : Results.Ok(await repository.GetOperationsAsync(tenant.AllowedCompanyIds, cancellationToken));
    }

    private static async Task<IResult> GetCnbvAsync(
        string? operationCode,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IContractFoundationRepository repository,
        CancellationToken cancellationToken)
    {
        var tenant = await GetTenantAsync(tenantContext, configuration, cancellationToken);
        if (tenant is null) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(operationCode) || operationCode.Trim().Length > 10)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["operationCode"] = ["Operation code is required."] });
        return Results.Ok(await repository.GetCnbvAsync(operationCode, tenant.AllowedCompanyIds, cancellationToken));
    }

    private static async Task<IResult> GetCfdiUsesAsync(
        int personId,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IContractFoundationRepository repository,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["Person id must be greater than zero."] });
        if (await GetTenantAsync(tenantContext, configuration, cancellationToken) is null) return Results.Forbid();
        return Results.Ok(await repository.GetCfdiUsesAsync(personId, cancellationToken));
    }

    private static async Task<IResult> GetAddressesAsync(
        int personId,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IContractFoundationRepository repository,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["Person id must be greater than zero."] });
        if (await GetTenantAsync(tenantContext, configuration, cancellationToken) is null) return Results.Forbid();
        var addresses = await repository.GetActiveAddressesAsync(personId, cancellationToken);
        return addresses is null ? Results.NotFound() : Results.Ok(addresses);
    }

    private static async Task<IResult> GetOrdinaryRateAsync(
        string? operationCode,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IContractFoundationRepository repository,
        CancellationToken cancellationToken)
    {
        if (await GetTenantAsync(tenantContext, configuration, cancellationToken) is null) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(operationCode)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["operationCode"] = ["Operation code is required."] });
        var rate = await repository.GetOrdinaryRateAsync(operationCode, 1, cancellationToken);
        return rate is null ? Results.UnprocessableEntity(new ProblemDetails { Title = "Ordinary rate configuration is required.", Status = 422, Extensions = { ["code"] = "contract_rate_configuration_required" } }) : Results.Ok(rate);
    }

    private static async Task<IResult> GetLateRateAsync(
        string? operationCode,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IContractFoundationRepository repository,
        CancellationToken cancellationToken)
    {
        if (await GetTenantAsync(tenantContext, configuration, cancellationToken) is null) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(operationCode)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["operationCode"] = ["Operation code is required."] });
        var rate = await repository.GetLateRateAsync(operationCode, 1, cancellationToken);
        return rate is null ? Results.UnprocessableEntity(new ProblemDetails { Title = "Late rate configuration is required.", Status = 422, Extensions = { ["code"] = "contract_late_rate_configuration_required" } }) : Results.Ok(rate);
    }

    private static async Task<IResult> PreviewAsync(
        [FromBody] ContractPreviewRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IContractPreviewService service,
        CancellationToken cancellationToken)
    {
        var tenant = await GetTenantAsync(tenantContext, configuration, cancellationToken);
        if (tenant is null) return Results.Forbid();
        if (request.PersonId <= 0 || request.AddressId <= 0 || string.IsNullOrWhiteSpace(request.OperationCode) || string.IsNullOrWhiteSpace(request.CfdiUseCode))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Customer, operation, CFDI use and address are required."] });

        var result = await service.PreviewAsync(request.ToCommand(), tenant.AllowedCompanyIds, cancellationToken);
        return Results.Ok(new ContractPreviewResponse(
            result.CanCreate,
            result.OperationCode,
            result.BusinessDate,
            new ContractPreviewAmounts(result.Capital, result.DownPayment, result.AmountToFinance, result.InitialBalance),
            new ContractPreviewResolvedCatalogs(result.RateId, result.CnbvId, result.CfdiUseCode, result.AddressId),
            result.PendingRules));
    }

    private static async Task<ExecutionTenant?> GetTenantAsync(IExecutionTenantContext context, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var tenant = await context.GetAsync(cancellationToken);
        var deployment = configuration["Deployment:TenantCode"];
        return tenant is not null && !string.IsNullOrWhiteSpace(deployment) &&
               string.Equals(tenant.TenantCode, deployment.Trim(), StringComparison.OrdinalIgnoreCase)
            ? tenant
            : null;
    }
}

public sealed record ContractPreviewRequest(
    int PersonId,
    string OperationCode,
    decimal Capital,
    decimal DownPayment,
    decimal Iva,
    DateOnly StartDate,
    DateOnly FirstPaymentDate,
    DateOnly DisbursementRequestDate,
    int Term,
    decimal NominalAnnualRate,
    int CnbvCode,
    string CfdiUseCode,
    int AddressId)
{
    public ContractPreviewCommand ToCommand() => new(
        PersonId,
        OperationCode.Trim(),
        Capital,
        DownPayment,
        Iva,
        StartDate,
        FirstPaymentDate,
        DisbursementRequestDate,
        Term,
        NominalAnnualRate,
        CnbvCode,
        CfdiUseCode.Trim(),
        AddressId);
}

public sealed record ContractPreviewResponse(
    bool CanCreate,
    string OperationCode,
    DateOnly? BusinessDate,
    ContractPreviewAmounts Amounts,
    ContractPreviewResolvedCatalogs ResolvedCatalogs,
    IReadOnlyList<ContractPreviewPendingRule> PendingRules);

public sealed record ContractPreviewAmounts(decimal Capital, decimal DownPayment, decimal AmountToFinance, decimal InitialBalance);

public sealed record ContractPreviewResolvedCatalogs(int? RateId, int? CnbvId, string? CfdiUseCode, int? AddressId);
