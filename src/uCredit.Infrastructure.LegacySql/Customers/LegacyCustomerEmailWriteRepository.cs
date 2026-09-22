using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

public sealed partial class LegacyCustomerEmailWriteRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration,
    ILogger<LegacyCustomerEmailWriteRepository> logger) : ICustomerEmailWriteRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<LegacyCustomerEmailWriteRepository> _logger = logger;

    internal const string EmailInsertSql = """
        INSERT INTO dbo.CPERSONA_EMAIL
            (MAI_FL_CVE, PNA_FL_PERSONA, MAI_DS_CONTACTO, MAI_DS_EMAIL, MAI_FG_STATUS, USR_CL_CVE, MAI_FE_ULTMOD, MAI_FG_OMITIR_ENVIO)
        VALUES (@EmailId, @PersonId, @Contact, @Email, 1, @LegacyUserCode, @OperationDate, 0);
        """;
    internal const string EmailUpdateSql = """
        UPDATE dbo.CPERSONA_EMAIL SET MAI_DS_EMAIL = @Email, MAI_DS_CONTACTO = @Contact,
            MAI_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode
        WHERE MAI_FL_CVE = @EmailId AND PNA_FL_PERSONA = @PersonId AND MAI_FE_ULTMOD = @ExpectedModifiedAt;
        """;
    internal const string EmailMetadataUpdateSql = """
        UPDATE dbo.CPERSONA_EMAIL SET MAI_DS_CONTACTO = @Contact,
            MAI_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode
        WHERE MAI_FL_CVE = @EmailId AND PNA_FL_PERSONA = @PersonId AND MAI_FE_ULTMOD = @ExpectedModifiedAt;
        """;
    internal const string EmailStateUpdateSql = """
        UPDATE dbo.CPERSONA_EMAIL SET MAI_FG_STATUS = @StatusCode,
            MAI_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode
        WHERE MAI_FL_CVE = @EmailId AND PNA_FL_PERSONA = @PersonId AND MAI_FE_ULTMOD = @ExpectedModifiedAt;
        """;
    internal const string DuplicateSql = """
        SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA_EMAIL
        WHERE PNA_FL_PERSONA = @PersonId AND MAI_FG_STATUS = 1
          AND (@EmailId IS NULL OR MAI_FL_CVE <> @EmailId)
          AND UPPER(LTRIM(RTRIM(MAI_DS_EMAIL))) = UPPER(LTRIM(RTRIM(@Email)))) THEN 1 ELSE 0 END;
        """;
    internal const string BlacklistSql = """
        SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPARAMETRO
        WHERE PAR_FL_CVE = 248 AND PAR_FG_STATUS = 1
          AND UPPER(LTRIM(RTRIM(PAR_DS_DESCRIPCION))) = UPPER(LTRIM(RTRIM(@Email)))) THEN 1 ELSE 0 END;
        """;
    internal const string UsageDeleteSql = "DELETE FROM dbo.KEMAIL_USO WHERE MAI_FL_CVE = @EmailId;";
    internal const string UsageInsertSql = "INSERT INTO dbo.KEMAIL_USO (PAR_CL_VALOR, MAI_FL_CVE, USR_CL_CVE, USO_FE_MODIFICACION) VALUES (@UsageCode, @EmailId, @LegacyUserCode, @OperationDate);";
    internal const string AuditSql = "INSERT INTO dbo.KBITACORA (BIT_FL_CVE, BIT_FE_FECHA, ATV_FL_CVE, BIT_FE_OPERACION, BIT_DS_REFERENCIA, USR_CL_CVE, USR_CL_FIRMA, BIT_TOP_CVE) VALUES (@BitacoraId, SYSUTCDATETIME(), @Activity, @OperationDate, @Reference, @LegacyUserCode, @LegacyUserCode, '');";

    public Task<ManagedCustomerEmail> CreateAsync(CustomerEmailCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("email", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var email = CustomerEmailRules.NormalizeEmail(command.Email);
            var contact = CustomerEmailRules.NormalizeContact(command.Contact);
            var usages = await ValidateUsagesAsync(connection, transaction, command.UsageCodes, cancellationToken);
            await EnsurePersonExistsAsync(connection, transaction, command.PersonId, cancellationToken);
            await EnsureNotBlacklistedAsync(connection, transaction, email, cancellationToken);
            await EnsureNotDuplicateAsync(connection, transaction, command.PersonId, email, null, cancellationToken);
            var emailId = await NextIdAsync(connection, transaction, cancellationToken);
            var parameters = EmailParameters(emailId, command.PersonId, contact, email, legacyUserCode, operationDate);
            await connection.ExecuteAsync(new CommandDefinition(EmailInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            await ReplaceUsagesAsync(connection, transaction, emailId, usages, legacyUserCode, operationDate, cancellationToken);
            await WriteAuditAsync(connection, transaction, 4, emailId, legacyUserCode, operationDate, cancellationToken);
            return await LoadAsync(connection, transaction, command.PersonId, emailId, cancellationToken);
        }, cancellationToken);

    public Task<ManagedCustomerEmail> UpdateAsync(CustomerEmailUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("email", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var current = await LoadAsync(connection, transaction, command.PersonId, command.EmailId, cancellationToken);
            EnsureExpected(current, command.ExpectedModifiedAt);
            var emailChanged = command.Email is not null;
            var email = emailChanged ? CustomerEmailRules.NormalizeEmail(command.Email!) : current.Email;
            var contact = CustomerEmailRules.NormalizeContact(command.Contact);
            var usages = await ValidateUsagesAsync(connection, transaction, command.UsageCodes, cancellationToken);
            if (emailChanged)
            {
                await EnsureNotBlacklistedAsync(connection, transaction, email, cancellationToken);
                await EnsureNotDuplicateAsync(connection, transaction, command.PersonId, email, command.EmailId, cancellationToken);
            }
            var parameters = EmailParameters(command.EmailId, command.PersonId, contact, email, legacyUserCode, operationDate);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime);
            var affected = await connection.ExecuteAsync(new CommandDefinition(emailChanged ? EmailUpdateSql : EmailMetadataUpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerEmailConflictException("email_modified", "The email was modified by another operation.");
            await ReplaceUsagesAsync(connection, transaction, command.EmailId, usages, legacyUserCode, operationDate, cancellationToken);
            await WriteAuditAsync(connection, transaction, 5, command.EmailId, legacyUserCode, operationDate, cancellationToken);
            return await LoadAsync(connection, transaction, command.PersonId, command.EmailId, cancellationToken);
        }, cancellationToken);

    public Task<ManagedCustomerEmail> ActivateAsync(CustomerEmailStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) => ChangeStateAsync(command, 1, legacyUserCode, correlationId, cancellationToken);
    public Task<ManagedCustomerEmail> DeactivateAsync(CustomerEmailStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) => ChangeStateAsync(command, 2, legacyUserCode, correlationId, cancellationToken);

    private static DynamicParameters EmailParameters(int emailId, int personId, string? contact, string email, string legacyUserCode, DateTime operationDate)
    {
        var parameters = new DynamicParameters();
        parameters.Add("EmailId", emailId, DbType.Int32); parameters.Add("PersonId", personId, DbType.Int32);
        parameters.Add("Contact", contact, DbType.String, size: 250); parameters.Add("Email", email, DbType.String, size: 250);
        parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8); parameters.Add("OperationDate", operationDate, DbType.DateTime);
        return parameters;
    }

    private Task<ManagedCustomerEmail> ChangeStateAsync(CustomerEmailStateChangeCommand command, int status, string legacyUserCode, string correlationId, CancellationToken cancellationToken) =>
        ExecuteAsync("email", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var current = await LoadAsync(connection, transaction, command.PersonId, command.EmailId, cancellationToken);
            EnsureExpected(current, command.ExpectedModifiedAt);
            if (status == 1) await EnsureNotDuplicateAsync(connection, transaction, command.PersonId, current.Email, command.EmailId, cancellationToken);
            var parameters = new DynamicParameters();
            parameters.Add("EmailId", command.EmailId, DbType.Int32); parameters.Add("PersonId", command.PersonId, DbType.Int32);
            parameters.Add("StatusCode", status, DbType.Int32); parameters.Add("OperationDate", operationDate, DbType.DateTime);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime); parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
            var affected = await connection.ExecuteAsync(new CommandDefinition(EmailStateUpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerEmailConflictException("email_modified", "The email was modified by another operation.");
            await WriteAuditAsync(connection, transaction, 5, command.EmailId, legacyUserCode, operationDate, cancellationToken);
            return await LoadAsync(connection, transaction, command.PersonId, command.EmailId, cancellationToken);
        }, cancellationToken);

    private async Task<T> ExecuteAsync<T>(string stage, int personId, string legacyUserCode, string correlationId, Func<SqlConnection, SqlTransaction, DateTime, Task<T>> operation, CancellationToken cancellationToken)
    {
        var tenant = await _tenantContext.GetAsync(cancellationToken);
        if (tenant is null || !string.Equals(tenant.TenantCode, _configuration["Deployment:TenantCode"], StringComparison.Ordinal))
            throw new CustomerEmailValidationException("The selected tenant is not valid for this deployment.");
        if (string.IsNullOrWhiteSpace(legacyUserCode) || legacyUserCode.Trim().Length > 8)
            throw new CustomerEmailValidationException("A valid Legacy user code is required.");
        var reason = LegacyCustomerWriteRepository.GetConfigurationReason(_options, _hostEnvironment.IsDevelopment());
        if (reason is not null) throw new LegacyWriteNotConfiguredException("Legacy write is not configured.", "configuration", reason.Value);

        await using var connection = new SqlConnection(_options.WriteConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await EnsureTestDatabaseAsync(connection, cancellationToken);
        }
        catch (LegacyWriteNotConfiguredException) { throw; }
        catch (SqlException exception) { throw new LegacyWriteUnavailableException("connection", exception); }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation(connection, transaction, DateTime.UtcNow);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            LogFailure(_logger, exception.GetType().Name, stage, exception is SqlException sql ? sql.Number : null, correlationId);
            throw;
        }
    }

    private static async Task<int[]> ValidateUsagesAsync(SqlConnection connection, SqlTransaction transaction, IReadOnlyList<int> requested, CancellationToken cancellationToken)
    {
        var usages = CustomerEmailRules.NormalizeUsages(requested);
        var rows = await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT PAR_CL_VALOR FROM dbo.CPARAMETRO WHERE PAR_FL_CVE = 244 AND PAR_CL_VALOR > 0 AND PAR_FG_STATUS = 1 AND PAR_CL_VALOR IN @UsageCodes;",
            new { UsageCodes = usages }, transaction, cancellationToken: cancellationToken));
        if (rows.ToHashSet().Count != usages.Length)
            throw new CustomerEmailValidationException("One or more email uses are invalid.");
        return usages;
    }

    private static async Task EnsurePersonExistsAsync(SqlConnection connection, SqlTransaction transaction, int personId, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END;", new { PersonId = personId }, transaction, cancellationToken: cancellationToken));
        if (exists == 0) throw new CustomerEmailNotFoundException("Customer was not found.");
    }

    private static async Task EnsureNotDuplicateAsync(SqlConnection connection, SqlTransaction transaction, int personId, string email, int? emailId, CancellationToken cancellationToken)
    {
        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(DuplicateSql, new { PersonId = personId, Email = email, EmailId = emailId }, transaction, cancellationToken: cancellationToken));
        if (duplicate == 1) throw new CustomerEmailConflictException("email_duplicate", "An active email already exists for this customer.");
    }

    private static async Task EnsureNotBlacklistedAsync(SqlConnection connection, SqlTransaction transaction, string email, CancellationToken cancellationToken)
    {
        var blacklisted = await connection.ExecuteScalarAsync<int>(new CommandDefinition(BlacklistSql, new { Email = email }, transaction, cancellationToken: cancellationToken));
        if (blacklisted == 1) throw new CustomerEmailValidationException("The email cannot be registered.");
    }

    private async Task ReplaceUsagesAsync(SqlConnection connection, SqlTransaction transaction, int emailId, IEnumerable<int> usages, string legacyUserCode, DateTime operationDate, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(UsageDeleteSql, new { EmailId = emailId }, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
        foreach (var usage in usages)
        {
            var parameters = new DynamicParameters();
            parameters.Add("UsageCode", usage, DbType.Int32); parameters.Add("EmailId", emailId, DbType.Int32);
            parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8); parameters.Add("OperationDate", operationDate, DbType.DateTime);
            await connection.ExecuteAsync(new CommandDefinition(UsageInsertSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
        }
    }

    private static async Task<ManagedCustomerEmail> LoadAsync(SqlConnection connection, SqlTransaction transaction, int personId, int emailId, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<LegacyManagedCustomerEmailRow>(new CommandDefinition(
            "SELECT MAI_FL_CVE AS EmailId, PNA_FL_PERSONA AS PersonId, MAI_DS_CONTACTO AS Contact, MAI_DS_EMAIL AS Email, MAI_FG_STATUS AS StatusCode, MAI_FE_ULTMOD AS ModifiedAt FROM dbo.CPERSONA_EMAIL WHERE PNA_FL_PERSONA = @PersonId AND MAI_FL_CVE = @EmailId;",
            new { PersonId = personId, EmailId = emailId }, transaction, cancellationToken: cancellationToken));
        if (row is null) throw new CustomerEmailNotFoundException("Email was not found.");
        var usages = await connection.QueryAsync<int>(new CommandDefinition("SELECT PAR_CL_VALOR FROM dbo.KEMAIL_USO WHERE MAI_FL_CVE = @EmailId ORDER BY PAR_CL_VALOR;", new { EmailId = emailId }, transaction, cancellationToken: cancellationToken));
        return new ManagedCustomerEmail(row.EmailId, row.PersonId, row.Contact, row.Email ?? throw new InvalidOperationException("Legacy email was unexpectedly null."), row.StatusCode, usages.ToArray(), row.ModifiedAt);
    }

    private static void EnsureExpected(ManagedCustomerEmail current, DateTime expected)
    {
        if (current.ModifiedAt != expected)
            throw new CustomerEmailConflictException("email_modified", "The email was modified by another operation.");
    }

    private static async Task<int> NextIdAsync(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = 'CPERSONA_EMAIL';",
            transaction: transaction, cancellationToken: cancellationToken));
        return id ?? throw new InvalidOperationException("No Legacy consecutive configured for CPERSONA_EMAIL.");
    }

    private static async Task WriteAuditAsync(SqlConnection connection, SqlTransaction transaction, int activity, int emailId, string legacyUserCode, DateTime operationDate, CancellationToken cancellationToken)
    {
        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = 'KBITACORA';",
            transaction: transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            AuditSql,
            new { BitacoraId = id, Activity = activity, OperationDate = operationDate, Reference = $"Se actualizo el correo con clave {emailId}", LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));
    }

    private static async Task EnsureTestDatabaseAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT DB_NAME();", cancellationToken: cancellationToken));
        if (!string.Equals(database, "pr_t", StringComparison.Ordinal))
            throw new LegacyWriteNotConfiguredException("Legacy write database is not approved.", "database_guard", LegacyWriteConfigurationReason.InvalidExpectedDatabase);
    }

    [LoggerMessage(EventId = 4221, Level = LogLevel.Error, Message = "Customer email mutation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}")]
    private static partial void LogFailure(ILogger logger, string exceptionType, string stage, int? sqlNumber, string correlationId);
}
