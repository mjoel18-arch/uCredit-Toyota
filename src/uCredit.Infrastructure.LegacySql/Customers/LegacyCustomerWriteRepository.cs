using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

public sealed class CustomerCreateConflictException(string message) : Exception(message);
public enum LegacyWriteConfigurationReason
{
    NotDevelopment,
    MissingWriteConnection,
    WriteTestsNotAllowed,
    MissingExpectedDatabase,
    InvalidExpectedDatabase
}

public sealed class LegacyWriteNotConfiguredException(string message, string stage, LegacyWriteConfigurationReason reason) : Exception(message)
{
    public string Stage { get; } = stage;
    public LegacyWriteConfigurationReason Reason { get; } = reason;
}

public sealed class LegacyWriteUnavailableException(string stage, Exception innerException) : Exception("Legacy write service unavailable.", innerException)
{
    public string Stage { get; } = stage;
}

internal sealed class LegacyPersonPepChecker : IPersonPepChecker
{
    public Task<PepCheckResult> CheckAsync(CustomerCreateCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PepCheckResult(PepCheckStatus.Unavailable));
}

public sealed partial class LegacyCustomerWriteRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext,
    IHostEnvironment hostEnvironment,
    ILogger<LegacyCustomerWriteRepository> logger) : ICustomerWriteRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly ILogger<LegacyCustomerWriteRepository> _logger = logger;

    public static LegacyWriteConfigurationReason? GetConfigurationReason(LegacySqlOptions options, bool isDevelopment)
    {
        if (!isDevelopment) return LegacyWriteConfigurationReason.NotDevelopment;
        if (string.IsNullOrWhiteSpace(options.WriteConnectionString)) return LegacyWriteConfigurationReason.MissingWriteConnection;
        if (!options.AllowLegacyWriteTests) return LegacyWriteConfigurationReason.WriteTestsNotAllowed;
        if (string.IsNullOrWhiteSpace(options.LegacyWriteTestDatabase)) return LegacyWriteConfigurationReason.MissingExpectedDatabase;
        if (!string.Equals(options.LegacyWriteTestDatabase, "pr_t", StringComparison.Ordinal)) return LegacyWriteConfigurationReason.InvalidExpectedDatabase;
        return null;
    }

    public async Task<CustomerCreateResult> CreateAsync(CustomerCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(legacyUserCode) || legacyUserCode.Trim().Length > 8)
            throw new ArgumentException("A valid LegacyUserCode is required.", nameof(legacyUserCode));
        if (CustomerCreateValidator.Validate(command).Count > 0)
            throw new ArgumentException("The customer create command is invalid.", nameof(command));

        var tenant = await _tenantContext.GetAsync(cancellationToken) ?? throw new InvalidOperationException("No execution tenant is selected.");
        if (tenant.AllowedCompanyIds.Count == 0)
            throw new InvalidOperationException("The execution tenant has no Legacy company scope.");
        EnsureWriteConfigured();

        await using var connection = new SqlConnection(_options.WriteConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (SqlException exception)
        {
            throw new LegacyWriteUnavailableException("connection", exception);
        }
        try
        {
            await EnsureTestDatabaseAsync(connection, cancellationToken);
        }
        catch (SqlException exception)
        {
            throw new LegacyWriteUnavailableException("database_guard", exception);
        }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var stage = "rfc";
        try
        {
            await EnsureRfcAvailableAsync(connection, transaction, command.Rfc.Trim(), cancellationToken);
            stage = "consecutive";
            var personId = await NextIdAsync(connection, transaction, "CPERSONA", cancellationToken);
            var addressId = await NextIdAsync(connection, transaction, "CDOMICILIO", cancellationToken);
            var phoneId = await NextIdAsync(connection, transaction, "CTELEFONO", cancellationToken);
            var emailId = await NextIdAsync(connection, transaction, "CPERSONA_EMAIL", cancellationToken);
            var bitacoraId = await NextIdAsync(connection, transaction, "KBITACORA", cancellationToken);
            var fullName = command.LegalPersonality == CustomerCreatePersonality.Moral
                ? command.LegalName!.Trim()
                : string.Join(" ", new[] { command.PaternalSurname, command.MaternalSurname, command.FirstName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();
            var operationDate = DateTime.UtcNow;
            var parameters = new DynamicParameters();
            parameters.Add("PersonId", personId, DbType.Int32); parameters.Add("Personality", (int)command.LegalPersonality, DbType.Int32);
            parameters.Add("Rfc", command.Rfc.Trim(), DbType.String, size: 13); parameters.Add("Name", fullName, DbType.String, size: 200);
            parameters.Add("FirstName", command.FirstName, DbType.String, size: 100); parameters.Add("PaternalSurname", command.PaternalSurname, DbType.String, size: 100); parameters.Add("MaternalSurname", command.MaternalSurname, DbType.String, size: 100);
            parameters.Add("LegalName", command.LegalName, DbType.String, size: 200); parameters.Add("CapitalRegime", command.CapitalRegime, DbType.String, size: 200); parameters.Add("Email", command.Email.Trim(), DbType.String, size: 250);
            parameters.Add("ContactForm", command.ContactFormCode, DbType.Int32); parameters.Add("GroupCode", command.GroupCode, DbType.Int32); parameters.Add("RiskCode", command.RiskCode, DbType.Int32); parameters.Add("TaxRegime", command.TaxRegimeCode, DbType.Int32); parameters.Add("CountryCode", command.CountryCode, DbType.Int32);
            parameters.Add("OperationDate", operationDate, DbType.DateTime); parameters.Add("ConstitutionOrBirthDate", command.ConstitutionOrBirthDate.ToDateTime(TimeOnly.MinValue), DbType.Date); parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
            parameters.Add("AddressId", addressId, DbType.Int32); parameters.Add("AddressType", command.AddressTypeCode, DbType.Int32); parameters.Add("PostalCode", command.PostalCode.Trim(), DbType.String, size: 10); parameters.Add("State", command.State.Trim(), DbType.String, size: 100); parameters.Add("City", command.City.Trim(), DbType.String, size: 100); parameters.Add("Municipality", command.Municipality.Trim(), DbType.String, size: 100); parameters.Add("Neighborhood", command.Neighborhood.Trim(), DbType.String, size: 100); parameters.Add("StreetAndNumber", command.StreetAndNumber.Trim(), DbType.String, size: 200); parameters.Add("ExteriorNumber", command.ExteriorNumber.Trim(), DbType.String, size: 20); parameters.Add("InteriorNumber", command.InteriorNumber, DbType.String, size: 20); parameters.Add("AddressReference", command.AddressReference, DbType.String, size: 200); parameters.Add("AddressSchedule", command.AddressSchedule, DbType.String, size: 100); parameters.Add("AddressStatus", command.AddressStatusCode, DbType.Int32);
            parameters.Add("PhoneId", phoneId, DbType.Int32); parameters.Add("PhoneType", command.PhoneTypeCode, DbType.Int32); parameters.Add("AreaCode", command.AreaCode.Trim(), DbType.String, size: 10); parameters.Add("PhoneNumber", command.PhoneNumber.Trim(), DbType.String, size: 30); parameters.Add("PhoneExtension", command.PhoneExtension, DbType.String, size: 10); parameters.Add("PhoneContact", command.PhoneContact, DbType.String, size: 200);
            parameters.Add("EmailId", emailId, DbType.Int32); parameters.Add("EmailContact", command.EmailContact.Trim(), DbType.String, size: 250); parameters.Add("BitacoraId", bitacoraId, DbType.Int32);

            stage = "person";
            await connection.ExecuteAsync(new CommandDefinition(CreateInsertSql(command), parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "email_usage";
            foreach (var usageCode in command.EmailUsageCodes)
                await connection.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.KEMAIL_USO (PAR_CL_VALOR, MAI_FL_CVE, USR_CL_CVE, USO_FE_MODIFICACION) VALUES (@UsageCode, @EmailId, @LegacyUserCode, @OperationDate);", new { UsageCode = usageCode, EmailId = emailId, LegacyUserCode = legacyUserCode.Trim(), OperationDate = operationDate }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "audit";
            await connection.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.KBITACORA (BIT_FL_CVE, BIT_FE_FECHA, ATV_FL_CVE, BIT_FE_OPERACION, BIT_DS_REFERENCIA, USR_CL_CVE, USR_CL_FIRMA, BIT_TOP_CVE) VALUES (@BitacoraId, SYSUTCDATETIME(), 4, @OperationDate, @Reference, @LegacyUserCode, @LegacyUserCode, '');", new { BitacoraId = bitacoraId, OperationDate = operationDate, Reference = $"Se agrego la persona con clave {personId}", LegacyUserCode = legacyUserCode.Trim() }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            stage = "commit";
            await transaction.CommitAsync(cancellationToken);
            return new CustomerCreateResult(personId);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new CustomerCreateConflictException("The customer already exists or conflicts with Legacy data.");
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            LogFailure(_logger, exception.GetType().Name, stage, correlationId);
            throw;
        }
    }

    [LoggerMessage(EventId = 4201, Level = LogLevel.Error, Message = "Customer creation failed. ExceptionType={ExceptionType} Stage={Stage} CorrelationId={CorrelationId}")]
    private static partial void LogFailure(ILogger logger, string exceptionType, string stage, string correlationId);

    private static string CreateInsertSql(CustomerCreateCommand command) => $"""
        INSERT INTO dbo.CPERSONA (PNA_FL_PERSONA, PNA_CL_PJURIDICA, PNA_CL_RFC, PNA_DS_NOMBRE, PNA_DS_EMAIL, PNA_FG_FCONTACTO, GPR_FL_CVE, PNA_FE_ALTA, PNA_FE_ULTMOD, PNA_FG_STATUS, USR_CL_CVE, PNA_CL_TCARTERA, PNA_CL_REFPAGO, GRI_FL_CVE, PAI_FL_CVE, PNA_NO_CODE, PNA_FG_FRONTERIZO, RFI_CL_CLAVE)
        VALUES (@PersonId, @Personality, @Rfc, @Name, @Email, @ContactForm, @GroupCode, @OperationDate, @OperationDate, 1, @LegacyUserCode, 1, 2, @RiskCode, @CountryCode, 0, 0, @TaxRegime);
        INSERT INTO {(command.LegalPersonality == CustomerCreatePersonality.Moral ? "dbo.CPMORAL (PNA_FL_PERSONA, PMO_DS_RAZON_SOCIAL, PMO_DS_REGIMEN_CAPITAL, PMO_FE_CONSTITUCION, PMO_FE_ULTMOD, USR_CL_CVE) VALUES (@PersonId, @LegalName, @CapitalRegime, @ConstitutionOrBirthDate, @OperationDate, @LegacyUserCode);" : "dbo.CPFISICA (PNA_FL_PERSONA, PFI_DS_APATERNO, PFI_DS_AMATERNO, PFI_DS_NOMBRE, PFI_FE_NACIMIENTO, PFI_FE_ULTMOD, USR_CL_CVE) VALUES (@PersonId, @PaternalSurname, @MaternalSurname, @FirstName, @ConstitutionOrBirthDate, @OperationDate, @LegacyUserCode);")}
        INSERT INTO dbo.CPTIPO (PNA_FL_PERSONA, PTI_FG_VALOR, PTI_FE_ULTMOD, USR_CL_CVE) VALUES (@PersonId, 1, @OperationDate, @LegacyUserCode);
        INSERT INTO dbo.CDOMICILIO (DMO_FL_CVE, PNA_FL_PERSONA, DMO_CL_CPOSTAL, DMO_DS_EFEDERATIVA, DMO_DS_MUNICIPIO, DMO_DS_CIUDAD, DMO_DS_COLONIA, DMO_DS_CALLE_NUM, DMO_DS_NUMEXT, DMO_DS_NUMINT, DMO_DS_REFERENCIA, DMO_DS_HORARIO, DMO_FG_TDIRECCION, DMO_FG_STATUS, DMO_FG_REGDEFAULT, DMO_FG_FACTURA, DMO_FG_EDOCTA, DMO_FG_OTROS, DMO_FE_ULTMOD, USR_CL_CVE) VALUES (@AddressId, @PersonId, @PostalCode, @State, @Municipality, @City, @Neighborhood, @StreetAndNumber, @ExteriorNumber, @InteriorNumber, @AddressReference, @AddressSchedule, @AddressType, @AddressStatus, 1, CASE WHEN @AddressType IN (1,2) THEN 1 ELSE 0 END, CASE WHEN @AddressType = 1 THEN 1 ELSE 0 END, CASE WHEN @AddressType = 1 THEN 1 ELSE 0 END, @OperationDate, @LegacyUserCode);
        INSERT INTO dbo.CTELEFONO (TFN_FL_CVE, PNA_FL_PERSONA, TTL_FL_CVE, DMO_FL_CVE, TFN_CL_LARGA_DISTANCIA, TFN_CL_TELEFONO, TFN_CL_EXTENSION, TFN_FG_STATUS, TFN_FG_REGDEFAULT, TFN_FE_ULTMOD, USR_CL_CVE, TFN_CL_LADA, TFN_DS_CONTACTO) VALUES (@PhoneId, @PersonId, @PhoneType, @AddressId, '', @PhoneNumber, @PhoneExtension, 1, 1, @OperationDate, @LegacyUserCode, @AreaCode, @PhoneContact);
        INSERT INTO dbo.CPERSONA_EMAIL (MAI_FL_CVE, PNA_FL_PERSONA, MAI_DS_CONTACTO, MAI_DS_EMAIL, MAI_FG_STATUS, USR_CL_CVE, MAI_FE_ULTMOD, MAI_FG_OMITIR_ENVIO) VALUES (@EmailId, @PersonId, @EmailContact, @Email, 1, @LegacyUserCode, @OperationDate, 0);
        """;

    private static async Task EnsureRfcAvailableAsync(SqlConnection connection, SqlTransaction transaction, string rfc, CancellationToken cancellationToken)
    {
        var existing = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("SELECT TOP (1) PNA_FL_PERSONA FROM dbo.CPERSONA WITH (UPDLOCK, HOLDLOCK) WHERE PNA_CL_RFC = @Rfc;", new { Rfc = rfc }, transaction, cancellationToken: cancellationToken));
        if (existing is not null) throw new CustomerCreateConflictException("The customer RFC already exists.");
    }

    private async Task<int> NextIdAsync(SqlConnection connection, SqlTransaction transaction, string tableName, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition("UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = @TableName;", new { TableName = tableName }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
        return id ?? throw new InvalidOperationException($"No Legacy consecutive configured for {tableName}.");
    }

    private void EnsureWriteConfigured()
    {
        var reason = GetConfigurationReason(_options, _hostEnvironment.IsDevelopment());
        if (reason is not null)
            throw new LegacyWriteNotConfiguredException("Legacy write configuration is not valid.", "configuration", reason.Value);
    }

    private static async Task EnsureTestDatabaseAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT DB_NAME();", cancellationToken: cancellationToken));
        if (!string.Equals(database, "pr_t", StringComparison.Ordinal)) throw new LegacyWriteNotConfiguredException("Legacy write test database is not pr_t.", "database_guard", LegacyWriteConfigurationReason.InvalidExpectedDatabase);
    }
}
