using Microsoft.AspNetCore.Mvc;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerBankEndpoints
{
    public static IEndpointRouteBuilder MapCustomerBankEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/catalogs/banks", GetAsync)
            .WithTags("Customers")
            .RequireAuthorization("customers.read");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] ICustomerBankReadRepository repository,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetAsync(cancellationToken);
        var deployment = configuration["Deployment:TenantCode"];
        if (tenant is null || string.IsNullOrWhiteSpace(deployment) ||
            !string.Equals(tenant.TenantCode, deployment, StringComparison.Ordinal))
            return Results.Forbid();

        var banks = await repository.GetActiveRealAsync(cancellationToken);
        return Results.Ok(banks.Select(bank => new CustomerBankResponse(bank.BankId, bank.BankName)).ToArray());
    }
}

public sealed record CustomerBankResponse(short BankId, string BankName);
