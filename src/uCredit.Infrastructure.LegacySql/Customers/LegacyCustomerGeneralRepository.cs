using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

public sealed partial class LegacyCustomerGeneralRepository(
    IOptions<LegacySqlOptions> options,
    IHostEnvironment hostEnvironment,
    IExecutionTenantContext tenantContext,
    ILogger<LegacyCustomerGeneralRepository> logger) : ICustomerGeneralRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;
    private readonly ILogger<LegacyCustomerGeneralRepository> _logger = logger;

    internal const string GeneralSelectSql = """
        SELECT P.PNA_FL_PERSONA AS PersonId, P.PNA_CL_PJURIDICA AS LegalPersonalityCode,
               P.PNA_CL_RFC AS Rfc, P.PNA_FG_STATUS AS StatusCode,
               P.PNA_FE_ULTMOD AS PersonModifiedAt,
               F.PFI_DS_NOMBRE AS FirstName, F.PFI_DS_APATERNO AS PaternalSurname,
               F.PFI_DS_AMATERNO AS MaternalSurname, F.PFI_FE_NACIMIENTO AS BirthDate,
               F.PFI_FE_ULTMOD AS PhysicalModifiedAt,
               M.PMO_DS_RAZON_SOCIAL AS LegalName, M.PMO_DS_NBCONTACTO AS ContactName,
               M.PMO_DS_PTOCONTACTO AS ContactPosition, M.PMO_FE_ULTMOD AS MoralModifiedAt
        FROM dbo.CPERSONA AS P
        LEFT JOIN dbo.CPFISICA AS F ON F.PNA_FL_PERSONA = P.PNA_FL_PERSONA
        LEFT JOIN dbo.CPMORAL AS M ON M.PNA_FL_PERSONA = P.PNA_FL_PERSONA
        WHERE P.PNA_FL_PERSONA = @PersonId;
        """;

    internal const string RolesSelectSql = """
        SELECT T.PTI_FG_VALOR AS Code, C.PAR_DS_DESCRIPCION AS Description,
               C.PAR_FG_STATUS AS CatalogStatus
        FROM dbo.CPTIPO AS T
        LEFT JOIN dbo.CPARAMETRO AS C ON C.PAR_FL_CVE = 5 AND C.PAR_CL_VALOR = T.PTI_FG_VALOR
        WHERE T.PNA_FL_PERSONA = @PersonId
        ORDER BY T.PTI_FG_VALOR;
        """;

    internal const string PersonUpdateSql = """
        UPDATE dbo.CPERSONA
        SET PNA_DS_NOMBRE = @FullName,
            PNA_FE_ULTMOD = @OperationDate,
            USR_CL_CVE = @LegacyUserCode
        WHERE PNA_FL_PERSONA = @PersonId AND PNA_FE_ULTMOD = @ExpectedPersonModifiedAt;
        """;

    internal const string PhysicalUpdateSql = """
        UPDATE dbo.CPFISICA
        SET PFI_DS_NOMBRE = @FirstName, PFI_DS_APATERNO = @PaternalSurname,
            PFI_DS_AMATERNO = @MaternalSurname, PFI_FE_NACIMIENTO = @BirthDate,
            PFI_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode
        WHERE PNA_FL_PERSONA = @PersonId AND PFI_FE_ULTMOD = @ExpectedSubtypeModifiedAt;
        """;

    internal const string MoralUpdateSql = """
        UPDATE dbo.CPMORAL
        SET PMO_DS_RAZON_SOCIAL = @LegalName, PMO_DS_NBCONTACTO = @ContactName,
            PMO_DS_PTOCONTACTO = @ContactPosition, PMO_FE_ULTMOD = @OperationDate,
            USR_CL_CVE = @LegacyUserCode
        WHERE PNA_FL_PERSONA = @PersonId AND PMO_FE_ULTMOD = @ExpectedSubtypeModifiedAt;
        """;

    private sealed class GeneralRow
    {
        public int PersonId { get; init; }
        public int LegalPersonalityCode { get; init; }
        public string? Rfc { get; init; }
        public int StatusCode { get; init; }
        public DateTime PersonModifiedAt { get; init; }
        public string? FirstName { get; init; }
        public string? PaternalSurname { get; init; }
        public string? MaternalSurname { get; init; }
        public DateTime? BirthDate { get; init; }
        public DateTime? PhysicalModifiedAt { get; init; }
        public string? LegalName { get; init; }
        public string? ContactName { get; init; }
        public string? ContactPosition { get; init; }
        public DateTime? MoralModifiedAt { get; init; }
    }

    private sealed class RoleRow
    {
        public int Code { get; init; }
        public string? Description { get; init; }
        public byte? CatalogStatus { get; init; }
    }

    public async Task<CustomerGeneralProfile?> GetAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (personId <= 0) return null;
        await _tenantContext.GetAsync(cancellationToken);
        EnsureReadConfigured();
        await using var connection = new SqlConnection(_options.ReadConnectionString);
        await connection.OpenAsync(cancellationToken);
        return await LoadAsync(connection, null, personId, cancellationToken);
    }

    public async Task<CustomerGeneralProfile> UpdateAsync(CustomerGeneralUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyUserCode) || legacyUserCode.Trim().Length > 8)
            throw new CustomerGeneralValidationException("Legacy user code is invalid.");
        var tenant = await _tenantContext.GetAsync(cancellationToken);
        if (tenant is null) throw new CustomerGeneralValidationException("No execution tenant is selected.");
        EnsureWriteConfigured();
        Validate(command);

        await using var connection = new SqlConnection(_options.WriteConnectionString);
        try { await connection.OpenAsync(cancellationToken); }
        catch (SqlException exception) { throw new LegacyWriteUnavailableException("connection", exception); }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var stage = "configuration";
        try
        {
            stage = "person";
            var current = await LoadAsync(connection, transaction, command.PersonId, cancellationToken)
                ?? throw new CustomerGeneralNotFoundException("Customer was not found.");
            if (current.PersonModifiedAt != command.ExpectedPersonModifiedAt || current.SubtypeModifiedAt != command.ExpectedSubtypeModifiedAt)
                throw new CustomerGeneralConflictException("customer_modified", "Customer data was modified by another operation.");

            var roleCodes = command.RoleCodes;
            if (roleCodes is not null)
            {
                stage = "role";
                await ValidateAndApplyRolesAsync(connection, transaction, command.PersonId, current.Roles, roleCodes, legacyUserCode, cancellationToken);
            }

            var operationDate = DateTime.UtcNow;
            var fullName = current.LegalPersonalityCode < 20
                ? string.Join(" ", new[] { command.PaternalSurname, command.MaternalSurname, command.FirstName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim()
                : (command.LegalName ?? string.Empty).Trim();
            var parameters = new DynamicParameters();
            parameters.Add("PersonId", command.PersonId, DbType.Int32);
            parameters.Add("FullName", fullName, DbType.String, size: 200);
            parameters.Add("OperationDate", operationDate, DbType.DateTime);
            parameters.Add("ExpectedPersonModifiedAt", command.ExpectedPersonModifiedAt, DbType.DateTime);
            parameters.Add("ExpectedSubtypeModifiedAt", command.ExpectedSubtypeModifiedAt, DbType.DateTime);
            parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
            parameters.Add("FirstName", command.FirstName, DbType.String, size: 100);
            parameters.Add("PaternalSurname", command.PaternalSurname, DbType.String, size: 100);
            parameters.Add("MaternalSurname", command.MaternalSurname, DbType.String, size: 100);
            parameters.Add("BirthDate", command.BirthDate, DbType.DateTime);
            parameters.Add("LegalName", command.LegalName, DbType.String, size: 200);
            parameters.Add("ContactName", command.ContactName, DbType.String, size: 200);
            parameters.Add("ContactPosition", command.ContactPosition, DbType.String, size: 200);
            if (await connection.ExecuteAsync(new CommandDefinition(PersonUpdateSql, parameters, transaction, cancellationToken: cancellationToken)) != 1)
                throw new CustomerGeneralConflictException("customer_modified", "Customer data was modified by another operation.");

            stage = "subtype";
            var subtypeSql = current.LegalPersonalityCode < 20 ? PhysicalUpdateSql : MoralUpdateSql;
            if (await connection.ExecuteAsync(new CommandDefinition(subtypeSql, parameters, transaction, cancellationToken: cancellationToken)) != 1)
                throw new CustomerGeneralConflictException("customer_modified", "Customer data was modified by another operation.");

            stage = "audit";
            var bitacoraId = await NextIdAsync(connection, transaction, "KBITACORA", cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO dbo.KBITACORA (BIT_FL_CVE, BIT_FE_FECHA, ATV_FL_CVE, BIT_FE_OPERACION, BIT_DS_REFERENCIA, USR_CL_CVE, USR_CL_FIRMA, BIT_TOP_CVE) VALUES (@BitacoraId, SYSUTCDATETIME(), 5, @OperationDate, @Reference, @LegacyUserCode, @LegacyUserCode, '');",
                new { BitacoraId = bitacoraId, OperationDate = operationDate, Reference = $"Se actualizo la persona con clave {command.PersonId}", LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));
            stage = "commit";
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            LogFailure(_logger, exception.GetType().Name, stage, exception is SqlException sql ? sql.Number : null, correlationId);
            throw;
        }

        return await GetAsync(command.PersonId, cancellationToken) ?? throw new CustomerGeneralNotFoundException("Customer was not found after update.");
    }

    private static void Validate(CustomerGeneralUpdateCommand command)
    {
        if (command.PersonId <= 0) throw new CustomerGeneralValidationException("personId is invalid.");
        if (command.RoleCodes is not null && (command.RoleCodes.Count == 0 || command.RoleCodes.Distinct().Count() != command.RoleCodes.Count))
            throw new CustomerGeneralValidationException("At least one distinct role is required.");
        CustomerGeneralRules.ValidateDate(command.BirthDate, "birthDate");
    }

    private static async Task ValidateAndApplyRolesAsync(SqlConnection connection, SqlTransaction transaction, int personId, IReadOnlyList<CustomerGeneralRole> existing, IReadOnlyList<int> requested, string legacyUserCode, CancellationToken cancellationToken)
    {
        var activeExisting = existing.Where(role => role.IsActive).Select(role => role.Code).ToHashSet();
        var required = existing.Where(role => role.Code == 10).Select(role => role.Code).ToHashSet();
        var requestedSet = requested.ToHashSet();
        if (requested.Any(code => code <= 0) || requestedSet.Count != requested.Count)
            throw new CustomerGeneralValidationException("Roles must be distinct positive codes.");
        var catalog = (await connection.QueryAsync<int>(new CommandDefinition("SELECT PAR_CL_VALOR FROM dbo.CPARAMETRO WHERE PAR_FL_CVE = @CatalogCode AND PAR_FG_STATUS = 1 AND PAR_CL_VALOR > 0 AND PAR_CL_VALOR IN @RoleCodes;", new { CatalogCode = 5, RoleCodes = requested }, transaction, cancellationToken: cancellationToken))).ToHashSet();
        if (catalog.Count != requestedSet.Count || requestedSet.Any(code => !catalog.Contains(code)))
            throw new CustomerGeneralValidationException("One or more roles are inactive or unknown.");
        if (required.Any(code => !requestedSet.Contains(code)))
            throw new CustomerGeneralConflictException("customer_role_required", "The insurer role cannot be removed.");
        if (requestedSet.Contains(10) && !activeExisting.Contains(10))
            throw new CustomerGeneralValidationException("The insurer role cannot be added from this screen.");
        if (requestedSet.Count == 0)
            throw new CustomerGeneralValidationException("At least one role is required.");

        foreach (var code in activeExisting.Except(requestedSet).Where(code => code != 10))
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM dbo.CPTIPO WHERE PNA_FL_PERSONA = @PersonId AND PTI_FG_VALOR = @RoleCode;", new { PersonId = personId, RoleCode = code }, transaction, cancellationToken: cancellationToken));
        foreach (var code in requestedSet.Except(existing.Select(role => role.Code)))
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.CPTIPO (PNA_FL_PERSONA, PTI_FG_VALOR, PTI_FE_ULTMOD, USR_CL_CVE) VALUES (@PersonId, @RoleCode, @OperationDate, @LegacyUserCode);", new { PersonId = personId, RoleCode = code, OperationDate = DateTime.UtcNow, LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task<CustomerGeneralProfile?> LoadAsync(SqlConnection connection, SqlTransaction? transaction, int personId, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<GeneralRow>(new CommandDefinition(GeneralSelectSql, new { PersonId = personId }, transaction, cancellationToken: cancellationToken));
        if (row is null) return null;
        var roles = (await connection.QueryAsync<RoleRow>(new CommandDefinition(RolesSelectSql, new { PersonId = personId }, transaction, cancellationToken: cancellationToken))).ToArray();
        var subtypeModifiedAt = row.LegalPersonalityCode < 20 ? row.PhysicalModifiedAt : row.MoralModifiedAt;
        if (subtypeModifiedAt is null) throw new CustomerGeneralValidationException("Customer subtype data is incomplete.");
        return new CustomerGeneralProfile(row.PersonId, row.LegalPersonalityCode, row.Rfc, row.FirstName, row.PaternalSurname, row.MaternalSurname, row.BirthDate, row.LegalName, row.ContactName, row.ContactPosition, row.StatusCode, row.PersonModifiedAt, subtypeModifiedAt.Value, roles.Select(role => new CustomerGeneralRole(role.Code, role.Description, role.CatalogStatus == 1, role.Code != 10 && role.CatalogStatus == 1)).ToArray());
    }

    private static async Task<int> NextIdAsync(SqlConnection connection, SqlTransaction transaction, string tableName, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = @TableName;", new { TableName = tableName }, transaction, cancellationToken: cancellationToken));
        return id ?? throw new InvalidOperationException($"No Legacy consecutive configured for {tableName}.");
    }

    private void EnsureReadConfigured() =>
        string.IsNullOrWhiteSpace(_options.ReadConnectionString).ThrowIfTrue("Legacy read connection is not configured.");

    private void EnsureWriteConfigured()
    {
        var reason = LegacyCustomerWriteRepository.GetConfigurationReason(_options, _hostEnvironment.IsDevelopment());
        if (reason is not null) throw new LegacyWriteNotConfiguredException("Legacy write configuration is not valid.", "configuration", reason.Value);
    }

    [LoggerMessage(EventId = 4210, Level = LogLevel.Error, Message = "Customer general update failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}")]
    private static partial void LogFailure(ILogger logger, string exceptionType, string stage, int? sqlNumber, string correlationId);
}

internal static class BooleanGuardExtensions
{
    public static void ThrowIfTrue(this bool value, string message)
    {
        if (value) throw new LegacyWriteNotConfiguredException(message, "configuration", LegacyWriteConfigurationReason.MissingWriteConnection);
    }
}
