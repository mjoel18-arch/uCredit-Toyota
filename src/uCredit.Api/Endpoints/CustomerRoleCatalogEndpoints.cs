using Microsoft.AspNetCore.Mvc;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerRoleCatalogEndpoints
{
    public static IEndpointRouteBuilder MapCustomerRoleCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/catalogs/person-roles", GetAsync)
            .WithTags("Customers")
            .RequireAuthorization("customers.read");
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] ICustomerRoleCatalogRepository repository,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetAsync(cancellationToken);
        var deployment = configuration["Deployment:TenantCode"];
        if (tenant is null || string.IsNullOrWhiteSpace(deployment) ||
            !string.Equals(tenant.TenantCode, deployment, StringComparison.Ordinal))
            return Results.Forbid();

        try
        {
            var roles = await repository.GetActiveAsync(cancellationToken);
            return Results.Ok(roles.Select(role => new CustomerRoleResponse(role.RoleCode, role.RoleName)).ToArray());
        }
        catch (CustomerRoleCatalogConflictException conflict)
        {
            return Results.Conflict(new ProblemDetails { Title = "The person role catalog is inconsistent.", Detail = conflict.Message, Status = StatusCodes.Status409Conflict });
        }
    }
}

public sealed record CustomerRoleResponse(int RoleCode, string RoleName);
