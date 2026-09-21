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

public sealed partial class LegacyCustomerAccountWriteRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext tenantContext,
    IHostEnvironment hostEnvironment,
    IConfiguration configuration,
    ILogger<LegacyCustomerAccountWriteRepository> logger) : ICustomerAccountWriteRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _tenantContext = tenantContext;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<LegacyCustomerAccountWriteRepository> _logger = logger;

    internal sealed class LegacyAccountRow
    {
        public int AccountId { get; init; }
        public int PersonId { get; init; }
        public short BankId { get; init; }
        public string? BankName { get; init; }
        public short BranchNumber { get; init; }
        public byte CurrencyCode { get; init; }
        public string? CurrencyName { get; init; }
        public byte AccountTypeCode { get; init; }
        public string? AccountTypeName { get; init; }
        public byte Status { get; init; }
        public DateTime ModifiedAt { get; init; }
        public int? PaymentMethodCode { get; init; }
        public string? AccountNumber { get; init; }
        public string? Clabe { get; init; }
    }

    internal const string SelectSql = """
        SELECT C.PCT_FL_CVE AS AccountId, C.PNA_FL_PERSONA AS PersonId, C.BCO_FL_CVE AS BankId,
               B.BCO_DS_NOMBRE AS BankName, C.PCT_NO_SUCURSAL AS BranchNumber,
               C.PCT_CL_MONEDA AS CurrencyCode, M.PAR_DS_DESCRIPCION AS CurrencyName,
               C.PCT_CL_TCUENTA AS AccountTypeCode, T.PAR_DS_DESCRIPCION AS AccountTypeName,
               C.PCT_FG_STATUS AS Status, C.PCT_FE_ULTMOD AS ModifiedAt,
               C.PCT_CL_MPAGO AS PaymentMethodCode, C.PCT_NO_CUENTA AS AccountNumber,
               C.PCT_NO_CLABE AS Clabe
        FROM dbo.CPCUENTA AS C
        LEFT JOIN dbo.CBANCO AS B ON B.BCO_FL_CVE = C.BCO_FL_CVE
        LEFT JOIN dbo.CPARAMETRO AS M ON M.PAR_FL_CVE = 4 AND M.PAR_CL_VALOR = C.PCT_CL_MONEDA AND M.PAR_FG_STATUS = 1
        LEFT JOIN dbo.CPARAMETRO AS T ON T.PAR_FL_CVE = 71 AND T.PAR_CL_VALOR = C.PCT_CL_TCUENTA AND T.PAR_FG_STATUS = 1
        WHERE C.PNA_FL_PERSONA = @PersonId AND C.PCT_FL_CVE = @AccountId;
        """;

    internal const string InsertSql = """
        INSERT INTO dbo.CPCUENTA
        (PCT_FL_CVE, BCO_FL_CVE, PCT_NO_SUCURSAL, PCT_NO_CUENTA, PCT_NO_CLABE,
         PCT_CL_MONEDA, PCT_CL_TCUENTA, PCT_FG_STATUS, PCT_FE_ULTMOD,
         USR_CL_CVE, PNA_FL_PERSONA, PCT_DS_INST_DEPOSITO, PCT_CL_MPAGO)
        VALUES (@AccountId, @BankId, @BranchNumber, @AccountNumber, @Clabe,
                @CurrencyCode, @AccountTypeCode, 1, @OperationDate,
                @LegacyUserCode, @PersonId, NULL, NULL);
        """;

    internal const string UpdateSql = """
        UPDATE dbo.CPCUENTA
        SET BCO_FL_CVE = @BankId, PCT_NO_SUCURSAL = @BranchNumber,
            PCT_NO_CUENTA = @AccountNumber, PCT_NO_CLABE = @Clabe,
            PCT_CL_MONEDA = @CurrencyCode, PCT_CL_TCUENTA = @AccountTypeCode,
            PCT_FE_ULTMOD = @OperationDate, USR_CL_CVE = @LegacyUserCode
        WHERE PCT_FL_CVE = @AccountId AND PNA_FL_PERSONA = @PersonId
          AND PCT_FE_ULTMOD = @ExpectedModifiedAt;
        """;

    internal const string StateUpdateSql = """
        UPDATE dbo.CPCUENTA
        SET PCT_FG_STATUS = @Status, PCT_FE_ULTMOD = @OperationDate,
            USR_CL_CVE = @LegacyUserCode
        WHERE PCT_FL_CVE = @AccountId AND PNA_FL_PERSONA = @PersonId
          AND PCT_FE_ULTMOD = @ExpectedModifiedAt;
        """;

    public Task<ManagedCustomerAccount> CreateAsync(CustomerAccountCreateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("account", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var normalized = NormalizeCreate(command);
            await EnsurePersonAsync(connection, transaction, command.PersonId, cancellationToken);
            await ValidateCatalogsAsync(connection, transaction, normalized.BankId, normalized.CurrencyCode, normalized.AccountTypeCode, cancellationToken);
            await EnsureNoDuplicateAsync(connection, transaction, command.PersonId, normalized.AccountNumber, normalized.Clabe, null, cancellationToken);
            var accountId = await NextIdAsync(connection, transaction, "CPCUENTA", "PCT_FL_CVE", cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(InsertSql, Parameters(command.PersonId, accountId, normalized, operationDate, legacyUserCode), transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            await WriteAuditAsync(connection, transaction, 4, operationDate, legacyUserCode, cancellationToken);
            return ToModel(await LoadAsync(connection, transaction, command.PersonId, accountId, cancellationToken));
        }, cancellationToken);

    public Task<ManagedCustomerAccount> UpdateAsync(CustomerAccountUpdateCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) =>
        ExecuteAsync("account", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var current = await LoadAsync(connection, transaction, command.PersonId, command.AccountId, cancellationToken);
            var normalized = NormalizeUpdate(command, current);
            await ValidateCatalogsAsync(connection, transaction, normalized.BankId, normalized.CurrencyCode, normalized.AccountTypeCode, cancellationToken);
            await EnsureNoDuplicateAsync(connection, transaction, command.PersonId, command.AccountNumber is null ? null : normalized.AccountNumber, command.Clabe is null ? null : normalized.Clabe, command.AccountId, cancellationToken);
            var parameters = Parameters(command.PersonId, command.AccountId, normalized, operationDate, legacyUserCode);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime);
            var affected = await connection.ExecuteAsync(new CommandDefinition(UpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerAccountConflictException("account_modified", "The account was modified by another operation.");
            await WriteAuditAsync(connection, transaction, 5, operationDate, legacyUserCode, cancellationToken);
            return ToModel(await LoadAsync(connection, transaction, command.PersonId, command.AccountId, cancellationToken));
        }, cancellationToken);

    public Task<ManagedCustomerAccount> ActivateAsync(CustomerAccountStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) => ChangeStateAsync(command, 1, legacyUserCode, correlationId, cancellationToken);
    public Task<ManagedCustomerAccount> DeactivateAsync(CustomerAccountStateChangeCommand command, string legacyUserCode, string correlationId, CancellationToken cancellationToken = default) => ChangeStateAsync(command, 2, legacyUserCode, correlationId, cancellationToken);

    private Task<ManagedCustomerAccount> ChangeStateAsync(CustomerAccountStateChangeCommand command, byte status, string legacyUserCode, string correlationId, CancellationToken cancellationToken) =>
        ExecuteAsync("account", command.PersonId, legacyUserCode, correlationId, async (connection, transaction, operationDate) =>
        {
            var current = await LoadAsync(connection, transaction, command.PersonId, command.AccountId, cancellationToken);
            if (current.ModifiedAt != command.ExpectedModifiedAt) throw new CustomerAccountConflictException("account_modified", "The account was modified by another operation.");
            if (status == 1) await EnsureNoDuplicateAsync(connection, transaction, command.PersonId, current.AccountNumber, current.Clabe, command.AccountId, cancellationToken);
            var parameters = new DynamicParameters();
            parameters.Add("AccountId", command.AccountId, DbType.Int32); parameters.Add("PersonId", command.PersonId, DbType.Int32);
            parameters.Add("ExpectedModifiedAt", command.ExpectedModifiedAt, DbType.DateTime); parameters.Add("Status", status, DbType.Byte);
            parameters.Add("OperationDate", operationDate, DbType.DateTime); parameters.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8);
            var affected = await connection.ExecuteAsync(new CommandDefinition(StateUpdateSql, parameters, transaction, _options.CommandTimeoutSeconds, CommandType.Text, cancellationToken: cancellationToken));
            if (affected == 0) throw new CustomerAccountConflictException("account_modified", "The account was modified by another operation.");
            await WriteAuditAsync(connection, transaction, 5, operationDate, legacyUserCode, cancellationToken);
            return ToModel(await LoadAsync(connection, transaction, command.PersonId, command.AccountId, cancellationToken));
        }, cancellationToken);

    private async Task<T> ExecuteAsync<T>(string stage, int personId, string legacyUserCode, string correlationId, Func<SqlConnection, SqlTransaction, DateTime, Task<T>> operation, CancellationToken cancellationToken)
    {
        var tenant = await _tenantContext.GetAsync(cancellationToken);
        if (tenant is null || !string.Equals(tenant.TenantCode, _configuration["Deployment:TenantCode"], StringComparison.Ordinal)) throw new CustomerAccountValidationException("The selected tenant is not valid for this deployment.");
        if (legacyUserCode.Trim().Length is 0 or > 8) throw new CustomerAccountValidationException("A valid Legacy user code is required.");
        var reason = LegacyCustomerWriteRepository.GetConfigurationReason(_options, _hostEnvironment.IsDevelopment());
        if (reason is not null) throw new LegacyWriteNotConfiguredException("Legacy write is not configured.", "configuration", reason.Value);
        await using var connection = new SqlConnection(_options.WriteConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            var database = await connection.ExecuteScalarAsync<string>(new CommandDefinition("SELECT DB_NAME();", cancellationToken: cancellationToken));
            if (!string.Equals(database, "pr_t", StringComparison.Ordinal)) throw new LegacyWriteNotConfiguredException("Legacy write database is not approved.", "database_guard", LegacyWriteConfigurationReason.InvalidExpectedDatabase);
        }
        catch (LegacyWriteNotConfiguredException) { throw; }
        catch (SqlException exception) { throw new LegacyWriteUnavailableException("connection", exception); }
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try { var result = await operation(connection, transaction, DateTime.UtcNow); await transaction.CommitAsync(cancellationToken); return result; }
        catch (Exception exception) { await transaction.RollbackAsync(CancellationToken.None); LogFailure(_logger, exception.GetType().Name, stage, exception is SqlException sql ? sql.Number : null, correlationId); throw; }
    }

    private static async Task EnsurePersonAsync(SqlConnection connection, SqlTransaction transaction, int personId, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPERSONA WHERE PNA_FL_PERSONA = @PersonId) THEN 1 ELSE 0 END;", new { PersonId = personId }, transaction, cancellationToken: cancellationToken));
        if (exists == 0) throw new CustomerAccountNotFoundException("Customer was not found.");
    }

    private static async Task ValidateCatalogsAsync(SqlConnection connection, SqlTransaction transaction, short bankId, byte currencyCode, byte accountTypeCode, CancellationToken cancellationToken)
    {
        var bank = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CBANCO WHERE BCO_FL_CVE = @BankId AND BCO_FG_STATUS = 1 AND BCO_FG_REAL = 1) THEN 1 ELSE 0 END;", new { BankId = bankId }, transaction, cancellationToken: cancellationToken));
        var currency = await CatalogExists(connection, transaction, 4, currencyCode, cancellationToken);
        var type = await CatalogExists(connection, transaction, 71, accountTypeCode, cancellationToken);
        if (bank == 0 || !currency || !type) throw new CustomerAccountValidationException("One or more account catalogs are invalid.");
    }

    private static async Task<bool> CatalogExists(SqlConnection connection, SqlTransaction transaction, int catalog, byte value, CancellationToken cancellationToken) => await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPARAMETRO WHERE PAR_FL_CVE = @Catalog AND PAR_CL_VALOR = @Value AND PAR_FG_STATUS = 1) THEN 1 ELSE 0 END;", new { Catalog = catalog, Value = value }, transaction, cancellationToken: cancellationToken)) == 1;

    private static async Task EnsureNoDuplicateAsync(SqlConnection connection, SqlTransaction transaction, int personId, string? accountNumber, string? clabe, int? accountId, CancellationToken cancellationToken)
    {
        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.CPCUENTA WHERE PNA_FL_PERSONA = @PersonId AND PCT_FG_STATUS = 1 AND (@AccountId IS NULL OR PCT_FL_CVE <> @AccountId) AND ((@AccountNumber IS NOT NULL AND PCT_NO_CUENTA = @AccountNumber) OR (@Clabe IS NOT NULL AND PCT_NO_CLABE = @Clabe))) THEN 1 ELSE 0 END;", new { PersonId = personId, AccountId = accountId, AccountNumber = accountNumber, Clabe = clabe }, transaction, cancellationToken: cancellationToken));
        if (duplicate == 1) throw new CustomerAccountConflictException("account_duplicate", "An active account or CLABE already exists for this customer.");
    }

    private static async Task<LegacyAccountRow> LoadAsync(SqlConnection connection, SqlTransaction transaction, int personId, int accountId, CancellationToken cancellationToken) => await connection.QuerySingleOrDefaultAsync<LegacyAccountRow>(new CommandDefinition(SelectSql, new { PersonId = personId, AccountId = accountId }, transaction, cancellationToken: cancellationToken)) is { } row ? row : throw new CustomerAccountNotFoundException("Account was not found.");

    private static DynamicParameters Parameters(int personId, int accountId, CustomerAccountCreateCommand command, DateTime operationDate, string legacyUserCode)
    {
        var p = new DynamicParameters(); p.Add("AccountId", accountId, DbType.Int32); p.Add("PersonId", personId, DbType.Int32); p.Add("BankId", command.BankId, DbType.Int16); p.Add("BranchNumber", command.BranchNumber, DbType.Int16); p.Add("AccountNumber", command.AccountNumber, DbType.String, size: 20); p.Add("Clabe", command.Clabe, DbType.String, size: 20); p.Add("CurrencyCode", command.CurrencyCode, DbType.Byte); p.Add("AccountTypeCode", command.AccountTypeCode, DbType.Byte); p.Add("OperationDate", operationDate, DbType.DateTime); p.Add("LegacyUserCode", legacyUserCode.Trim(), DbType.String, size: 8); return p;
    }

    private static CustomerAccountCreateCommand NormalizeCreate(CustomerAccountCreateCommand command)
    {
        CustomerAccountRules.ValidateBranch(command.BranchNumber);
        return command with { AccountNumber = CustomerAccountRules.RequiredSensitive(command.AccountNumber, 20, "Account number"), Clabe = CustomerAccountRules.RequiredClabe(command.Clabe) };
    }
    private static CustomerAccountCreateCommand NormalizeUpdate(CustomerAccountUpdateCommand command, LegacyAccountRow current)
    {
        CustomerAccountRules.ValidateBranch(command.BranchNumber);
        return new(command.PersonId, command.BankId, command.BranchNumber, command.CurrencyCode, command.AccountTypeCode, command.AccountNumber is null ? current.AccountNumber ?? throw new CustomerAccountValidationException("Stored account number is unavailable.") : CustomerAccountRules.RequiredSensitive(command.AccountNumber, 20, "Account number"), command.Clabe is null ? current.Clabe ?? throw new CustomerAccountValidationException("Stored CLABE is unavailable.") : CustomerAccountRules.RequiredClabe(command.Clabe));
    }
    private static ManagedCustomerAccount ToModel(LegacyAccountRow row) => new(row.AccountId, row.PersonId, row.BankId, row.BankName, row.BranchNumber, row.CurrencyCode, row.CurrencyName, row.AccountTypeCode, row.AccountTypeName, row.PaymentMethodCode, row.Status, Mask(row.AccountNumber), Mask(row.Clabe), row.ModifiedAt);
    private static string? Mask(string? value) => string.IsNullOrEmpty(value) ? null : value.Length >= 4 ? $"••••{value[^4..]}" : "••••";
    private static async Task<int> NextIdAsync(SqlConnection connection, SqlTransaction transaction, string tableName, string fieldName, CancellationToken cancellationToken) => await connection.ExecuteScalarAsync<int?>(new CommandDefinition("UPDATE dbo.CCATCONSEC WITH (UPDLOCK, HOLDLOCK) SET CCT_NO_CONSECUTIVO = CCT_NO_CONSECUTIVO + 1 OUTPUT INSERTED.CCT_NO_CONSECUTIVO WHERE EMP_FL_CVE = 0 AND CCS_DS_NOMTABLA = @TableName AND CCT_DS_CAMPO = @FieldName;", new { TableName = tableName, FieldName = fieldName }, transaction, cancellationToken: cancellationToken)) ?? throw new InvalidOperationException("No Legacy consecutive configured.");
    private static async Task WriteAuditAsync(SqlConnection connection, SqlTransaction transaction, int activityCode, DateTime operationDate, string legacyUserCode, CancellationToken cancellationToken)
    {
        var id = await NextIdAsync(connection, transaction, "KBITACORA", "BIT_FL_CVE", cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("INSERT INTO dbo.KBITACORA (BIT_FL_CVE, BIT_FE_FECHA, ATV_FL_CVE, BIT_FE_OPERACION, BIT_DS_REFERENCIA, USR_CL_CVE, USR_CL_FIRMA, BIT_TOP_CVE) VALUES (@Id, SYSUTCDATETIME(), @ActivityCode, @OperationDate, @Reference, @LegacyUserCode, @LegacyUserCode, '');", new { Id = id, ActivityCode = activityCode, OperationDate = operationDate, Reference = "Customer account operation", LegacyUserCode = legacyUserCode.Trim() }, transaction, cancellationToken: cancellationToken));
    }

    [LoggerMessage(EventId = 4220, Level = LogLevel.Error, Message = "Customer account mutation failed. ExceptionType={ExceptionType} SqlNumber={SqlNumber} Stage={Stage} CorrelationId={CorrelationId}")]
    private static partial void LogFailure(ILogger logger, string exceptionType, string stage, int? sqlNumber, string correlationId);
}
