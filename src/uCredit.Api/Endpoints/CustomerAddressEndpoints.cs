using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UCredit.Api.Contracts;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerAddressEndpoints
{
    public static IEndpointRouteBuilder MapCustomerAddressEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/customers/{personId:int}/addresses", GetAsync)
            .WithTags("Customers")
            .RequireAuthorization("customers.read");

        var group = endpoints.MapGroup("/api/v1/customers/{personId:int}/addresses")
            .WithTags("Customers")
            .RequireAuthorization("customers.write")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();

        group.MapPost("", CreateAsync);
        group.MapPut("{addressId:int}", UpdateAsync);
        group.MapPost("{addressId:int}/activate", ActivateAsync);
        group.MapPost("{addressId:int}/deactivate", DeactivateAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        int personId,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] ICustomerAddressReadRepository repository,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        var addresses = await repository.GetByPersonIdAsync(personId, cancellationToken);
        return addresses is null ? Results.NotFound() : Results.Ok(addresses.Select(ManagedCustomerAddressResponse.FromModel).ToArray());
    }

    private static async Task<IResult> CreateAsync(
        int personId,
        [FromBody] CustomerAddressRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerAddressWriteRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            var legacyUserCode = actor.LegacyUserCode;
            if (string.IsNullOrWhiteSpace(legacyUserCode)) return MissingLegacyUserCode();
            var result = await repository.CreateAsync(request.ToCreateCommand(personId), legacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Created($"/api/v1/customers/{personId}/addresses/{result.AddressId}", ManagedCustomerAddressResponse.FromModel(result));
        }
        catch (Exception exception)
        {
            return MapFailure(exception, httpContext.TraceIdentifier);
        }
    }

    private static async Task<IResult> UpdateAsync(
        int personId,
        int addressId,
        [FromBody] CustomerAddressRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerAddressWriteRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (personId <= 0 || addressId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            var legacyUserCode = actor.LegacyUserCode;
            if (string.IsNullOrWhiteSpace(legacyUserCode)) return MissingLegacyUserCode();
            var result = await repository.UpdateAsync(request.ToUpdateCommand(personId, addressId), legacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(ManagedCustomerAddressResponse.FromModel(result));
        }
        catch (Exception exception)
        {
            return MapFailure(exception, httpContext.TraceIdentifier);
        }
    }

    private static Task<IResult> ActivateAsync(
        int personId,
        int addressId,
        [FromBody] AddressStateChangeRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerAddressWriteRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) => ChangeStateAsync(personId, addressId, request, true, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static Task<IResult> DeactivateAsync(
        int personId,
        int addressId,
        [FromBody] AddressStateChangeRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerAddressWriteRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken) => ChangeStateAsync(personId, addressId, request, false, tenantContext, configuration, actorContext, repository, httpContext, cancellationToken);

    private static async Task<IResult> ChangeStateAsync(
        int personId,
        int addressId,
        AddressStateChangeRequest request,
        bool activate,
        IExecutionTenantContext tenantContext,
        IConfiguration configuration,
        IExecutionActorContext actorContext,
        ICustomerAddressWriteRepository repository,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (personId <= 0 || addressId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var actor = await actorContext.GetAsync(cancellationToken);
            var legacyUserCode = actor.LegacyUserCode;
            if (string.IsNullOrWhiteSpace(legacyUserCode)) return MissingLegacyUserCode();
            var command = new CustomerAddressStateChangeCommand(personId, addressId, request.ExpectedModifiedAtValue, request.ReplacementAddressId);
            var result = activate
                ? await repository.ActivateAsync(command, legacyUserCode, httpContext.TraceIdentifier, cancellationToken)
                : await repository.DeactivateAsync(command, legacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(ManagedCustomerAddressResponse.FromModel(result));
        }
        catch (Exception exception)
        {
            return MapFailure(exception, httpContext.TraceIdentifier);
        }
    }

    private static async Task<bool> IsDeploymentTenantAsync(IExecutionTenantContext tenantContext, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetAsync(cancellationToken);
        var deploymentTenant = configuration["Deployment:TenantCode"];
        return tenant is not null && !string.IsNullOrWhiteSpace(deploymentTenant) &&
            string.Equals(tenant.TenantCode, deploymentTenant, StringComparison.Ordinal);
    }

    private static IResult MapFailure(Exception exception, string correlationId) => exception switch
    {
        CustomerAddressNotFoundException => Results.NotFound(),
        CustomerAddressValidationException validation => Results.Problem(validation.Message, statusCode: StatusCodes.Status400BadRequest),
        CustomerAddressConflictException conflict => Conflict(conflict.Code),
        LegacyWriteNotConfiguredException => Unavailable(correlationId),
        LegacyWriteUnavailableException => Unavailable(correlationId),
        _ => throw exception,
    };

    private static IResult Conflict(string code)
    {
        var problem = new ProblemDetails { Title = "Address update conflict.", Status = StatusCodes.Status409Conflict };
        problem.Extensions["code"] = code;
        return Results.Conflict(problem);
    }

    private static IResult Unavailable(string correlationId)
    {
        var problem = new ProblemDetails { Title = "The Legacy write service is unavailable.", Status = StatusCodes.Status503ServiceUnavailable };
        problem.Extensions["correlationId"] = correlationId;
        return Results.Problem(problem);
    }

    private static IResult InvalidPersonId() => Results.ValidationProblem(new Dictionary<string, string[]>
    {
        ["personId"] = ["personId must be greater than zero."],
    });

    private static IResult MissingLegacyUserCode() => Results.UnprocessableEntity(new ProblemDetails
    {
        Title = "Legacy user code is required.",
        Status = StatusCodes.Status422UnprocessableEntity,
    });
}

public sealed record CustomerAddressRequest(
    string PostalCode,
    string State,
    string Municipality,
    string City,
    string Neighborhood,
    string StreetAndNumber,
    string ExteriorNumber,
    string? InteriorNumber,
    string? Reference,
    string? Schedule,
    int AddressTypeCode,
    IReadOnlyList<string>? Uses,
    bool IsDefault,
    DateTime? ExpectedModifiedAt,
    int CountryCode)
{
    public CustomerAddressCreateCommand ToCreateCommand(int personId) => new(personId, PostalCode, State, Municipality, City, Neighborhood, StreetAndNumber, ExteriorNumber, InteriorNumber, Reference, Schedule, AddressTypeCode, Uses, IsDefault, CountryCode);

    public CustomerAddressUpdateCommand ToUpdateCommand(int personId, int addressId) => new(personId, addressId, PostalCode, State, Municipality, City, Neighborhood, StreetAndNumber, ExteriorNumber, InteriorNumber, Reference, Schedule, AddressTypeCode, Uses, IsDefault, ExpectedModifiedAt ?? throw new CustomerAddressValidationException("expectedModifiedAt is required."), CountryCode);
}

public sealed record AddressStateChangeRequest(DateTime? ExpectedModifiedAt, int? ReplacementAddressId)
{
    public DateTime ExpectedModifiedAtValue => ExpectedModifiedAt ?? throw new CustomerAddressValidationException("expectedModifiedAt is required.");
}
