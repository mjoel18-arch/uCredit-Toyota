using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using UCredit.Api.Security;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql.Customers;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Api.Endpoints;

public static class CustomerGeneralEndpoints
{
    public static IEndpointRouteBuilder MapCustomerGeneralEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/customers/{personId:int}/general", GetAsync)
            .WithTags("Customers").RequireAuthorization("customers.read");
        endpoints.MapPut("/api/v1/customers/{personId:int}/general", UpdateAsync)
            .WithTags("Customers").RequireAuthorization("customers.write")
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        int personId,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] ICustomerGeneralRepository repository,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        var profile = await repository.GetAsync(personId, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(CustomerGeneralResponse.FromModel(profile));
    }

    private static async Task<IResult> UpdateAsync(
        int personId,
        CustomerGeneralRequest request,
        [FromServices] IExecutionTenantContext tenantContext,
        [FromServices] IConfiguration configuration,
        [FromServices] IExecutionActorContext actorContext,
        [FromServices] ICustomerGeneralRepository repository,
        [FromServices] IOptions<CustomerCreationOptions> creationOptions,
        [FromServices] IEnumerable<IPersonPepChecker> pepCheckers,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (personId <= 0) return InvalidPersonId();
        if (!await IsDeploymentTenantAsync(tenantContext, configuration, cancellationToken)) return Results.Forbid();
        try
        {
            var current = await repository.GetAsync(personId, cancellationToken);
            if (current is null) return Results.NotFound();
            if (request.LegalPersonality is not null || request.Rfc is not null || request.StatusCode is not null || request.CountryCode is not null || request.GroupCode is not null || request.RiskCode is not null || request.TaxRegimeCode is not null || request.Code is not null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["Immutable customer fields cannot be modified."] });

            var changesName = current.LegalPersonalityCode < 20
                ? request.FirstName is not null || request.PaternalSurname is not null || request.MaternalSurname is not null
                : request.LegalName is not null;
            if (changesName && creationOptions.Value.RequirePepCheck)
            {
                var checker = pepCheckers.SingleOrDefault();
                if (checker is null) return Unavailable(httpContext.TraceIdentifier);
                var pep = await checker.CheckAsync(ToPepCommand(current, request), cancellationToken);
                if (pep.Status == PepCheckStatus.Unavailable) return Unavailable(httpContext.TraceIdentifier);
                if (pep.Status == PepCheckStatus.Match && !request.PepConfirmed)
                {
                    var problem = new ProblemDetails { Title = "PEP review confirmation is required before updating the customer.", Status = StatusCodes.Status422UnprocessableEntity };
                    problem.Extensions["code"] = "pep_confirmation_required";
                    return Results.UnprocessableEntity(problem);
                }
            }

            var actor = await actorContext.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(actor.LegacyUserCode))
                return Results.UnprocessableEntity(new ProblemDetails { Title = "Legacy user code is required.", Status = 422 });
            var command = request.ToCommand(personId, current);
            var result = await repository.UpdateAsync(command, actor.LegacyUserCode, httpContext.TraceIdentifier, cancellationToken);
            return Results.Ok(CustomerGeneralResponse.FromModel(result));
        }
        catch (CustomerGeneralValidationException exception) { return Results.Problem(exception.Message, statusCode: 400); }
        catch (CustomerGeneralConflictException exception) { return Conflict(exception.Code); }
        catch (CustomerGeneralNotFoundException) { return Results.NotFound(); }
        catch (LegacyWriteNotConfiguredException) { return Unavailable(httpContext.TraceIdentifier); }
        catch (LegacyWriteUnavailableException) { return Unavailable(httpContext.TraceIdentifier); }
    }

    private static CustomerCreateCommand ToPepCommand(CustomerGeneralProfile current, CustomerGeneralRequest request)
    {
        var personality = current.LegalPersonalityCode switch
        {
            1 => CustomerCreatePersonality.Individual,
            2 => CustomerCreatePersonality.IndividualBusiness,
            20 => CustomerCreatePersonality.Moral,
            _ => throw new CustomerGeneralValidationException("Legal personality is not supported.")
        };

        return new CustomerCreateCommand(
            personality,
            current.Rfc ?? string.Empty,
            request.FirstName ?? current.FirstName,
            request.PaternalSurname ?? current.PaternalSurname,
            request.MaternalSurname ?? current.MaternalSurname,
            request.LegalName ?? current.LegalName,
            null,
            DateOnly.FromDateTime((request.BirthDate ?? current.BirthDate ?? DateTime.MinValue).Date),
            1,
            1,
            1,
            1,
            "605",
            current.Roles.Where(role => role.IsActive).Select(role => role.Code).ToArray());
    }

    private static async Task<bool> IsDeploymentTenantAsync(IExecutionTenantContext tenantContext, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var tenant = await tenantContext.GetAsync(cancellationToken);
        var deploymentTenant = configuration["Deployment:TenantCode"];
        return tenant is not null && !string.IsNullOrWhiteSpace(deploymentTenant) && string.Equals(tenant.TenantCode, deploymentTenant, StringComparison.Ordinal);
    }

    private static IResult Conflict(string code)
    {
        var problem = new ProblemDetails { Title = "Customer update conflict.", Status = StatusCodes.Status409Conflict };
        problem.Extensions["code"] = code;
        return Results.Conflict(problem);
    }

    private static IResult Unavailable(string correlationId) => Results.Problem(new ProblemDetails { Title = "The Legacy write service is unavailable.", Status = 503, Extensions = { ["correlationId"] = correlationId } });
    private static IResult InvalidPersonId() => Results.ValidationProblem(new Dictionary<string, string[]> { ["personId"] = ["personId must be greater than zero."] });
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomerGeneralRequest(
    string? FirstName,
    string? PaternalSurname,
    string? MaternalSurname,
    DateTime? BirthDate,
    string? LegalName,
    string? ContactName,
    string? ContactPosition,
    IReadOnlyList<int>? RoleCodes,
    DateTime ExpectedPersonModifiedAt,
    DateTime ExpectedSubtypeModifiedAt,
    bool PepConfirmed,
    int? LegalPersonality = null,
    string? Rfc = null,
    int? StatusCode = null,
    int? CountryCode = null,
    int? GroupCode = null,
    int? RiskCode = null,
    string? TaxRegimeCode = null,
    int? Code = null)
{
    public CustomerGeneralUpdateCommand ToCommand(int personId, CustomerGeneralProfile current) => new(
        personId,
        FirstName ?? current.FirstName,
        PaternalSurname ?? current.PaternalSurname,
        MaternalSurname ?? current.MaternalSurname,
        BirthDate ?? current.BirthDate,
        LegalName ?? current.LegalName,
        ContactName ?? current.ContactName,
        ContactPosition ?? current.ContactPosition,
        RoleCodes,
        ExpectedPersonModifiedAt,
        ExpectedSubtypeModifiedAt);
}

public sealed record CustomerGeneralResponse(
    int PersonId,
    int LegalPersonalityCode,
    string? Rfc,
    string? FirstName,
    string? PaternalSurname,
    string? MaternalSurname,
    DateTime? BirthDate,
    string? LegalName,
    string? ContactName,
    string? ContactPosition,
    int StatusCode,
    DateTime PersonModifiedAt,
    DateTime SubtypeModifiedAt,
    IReadOnlyList<CustomerGeneralRoleResponse> Roles)
{
    public static CustomerGeneralResponse FromModel(CustomerGeneralProfile model) => new(model.PersonId, model.LegalPersonalityCode, model.Rfc, model.FirstName, model.PaternalSurname, model.MaternalSurname, model.BirthDate, model.LegalName, model.ContactName, model.ContactPosition, model.StatusCode, model.PersonModifiedAt, model.SubtypeModifiedAt, model.Roles.Select(role => new CustomerGeneralRoleResponse(role.Code, role.Description, role.IsActive, role.IsEditable)).ToArray());
}

public sealed record CustomerGeneralRoleResponse(int RoleCode, string? RoleName, bool IsActive, bool IsEditable);
