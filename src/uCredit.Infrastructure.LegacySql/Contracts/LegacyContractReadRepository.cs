using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Infrastructure.LegacySql.Contracts;

internal sealed class LegacyContractSummaryRow
{
    public string? ContractNumber { get; set; }
    public int? PersonId { get; set; }
    public string? PersonName { get; set; }
    public string? OperationTypeCode { get; set; }
    public string? OperationTypeName { get; set; }
    public int? AddressId { get; set; }
    public decimal? FinancedAmount { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public string? CurrencyCode { get; set; }
    public string? CurrencyName { get; set; }
    public int? CurrentTerm { get; set; }
    public int? OriginalTerm { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? ActivationDate { get; set; }
    public DateTime? DisbursementDate { get; set; }
    public DateTime? FirstPaymentDate { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public int? StatusCode { get; set; }
    public string? StatusName { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

public sealed class LegacyContractReadRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext executionTenantContext) : IContractReadRepository
{
    private static readonly Dictionary<ContractSort, string> SortExpressions =
        new Dictionary<ContractSort, string>
        {
            [ContractSort.ContractNumberAsc] = "C.CTO_FL_CVE ASC",
            [ContractSort.ContractNumberDesc] = "C.CTO_FL_CVE DESC, C.CTO_FL_CVE ASC",
            [ContractSort.OutstandingBalanceAsc] = "C.CTO_NO_SALDO ASC, C.CTO_FL_CVE ASC",
            [ContractSort.OutstandingBalanceDesc] = "C.CTO_NO_SALDO DESC, C.CTO_FL_CVE ASC",
        };

    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _executionTenantContext = executionTenantContext;

    public async Task<PagedResult<ContractSummary>> SearchAsync(
        ContractSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var errors = ContractSearchValidator.Validate(criteria);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                string.Join(" ", errors.SelectMany(error => error.Value)),
                nameof(criteria));
        }

        var executionTenant = await GetExecutionTenantAsync(cancellationToken);
        EnsureConfigured();

        var parameters = CreateParameters(criteria, executionTenant.AllowedCompanyIds);
        var firstRow = ((long)criteria.Page - 1) * criteria.PageSize + 1;
        var lastRow = (long)criteria.Page * criteria.PageSize;
        parameters.Add("FirstRow", firstRow, DbType.Int64);
        parameters.Add("LastRow", lastRow, DbType.Int64);

        var countSql = CreateCountSql(criteria, executionTenant.AllowedCompanyIds);
        var pageSql = CreateSearchSql(criteria, executionTenant.AllowedCompanyIds, firstRow, lastRow);

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        var total = await connection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                countSql,
                parameters,
                commandTimeout: _options.CommandTimeoutSeconds,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<LegacyContractSummaryRow>(
            new CommandDefinition(
                pageSql,
                parameters,
                commandTimeout: _options.CommandTimeoutSeconds,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken))).Select(MapRow).ToList();

        return new PagedResult<ContractSummary>(items, criteria.Page, criteria.PageSize, checked((int)total));
    }

    public async Task<ContractSummary?> GetByNumberAsync(
        string contractNumber,
        CancellationToken cancellationToken = default)
    {
        var executionTenant = await GetExecutionTenantAsync(cancellationToken);
        EnsureConfigured();

        var sql = CreateGetByNumberSql();

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        var command = new CommandDefinition(
            sql,
            CreateContractParameters(contractNumber, executionTenant.AllowedCompanyIds),
            commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<LegacyContractSummaryRow>(command);
        return row is null ? null : MapRow(row);
    }

    internal static string CreateSearchSql(
        ContractSearchCriteria criteria,
        IReadOnlyCollection<int> allowedCompanyIds,
        long firstRow,
        long lastRow)
    {
        var predicates = CreatePredicates(criteria, allowedCompanyIds);
        var sortExpression = SortExpressions[criteria.Sort];
        var whereClause = $"WHERE {string.Join(" AND ", predicates)}";
        const string fromClause = """
            FROM dbo.KCONTRATO AS C
            INNER JOIN dbo.CPERSONA AS P
                ON P.PNA_FL_PERSONA = C.PNA_FL_PERSONA
            INNER JOIN dbo.KTOPERACION AS O
                ON O.TOP_CL_CVE = C.TOP_CL_CVE
            INNER JOIN dbo.CPARAMETRO AS S
                ON S.PAR_FL_CVE = 33
                AND S.PAR_CL_VALOR = C.CTO_FG_STATUS
            LEFT JOIN dbo.CUSUARIO AS U
                ON U.USR_CL_CVE = C.USR_CL_CVE
            LEFT JOIN dbo.CMONEDA AS M
                ON M.MON_FL_CVE = C.CTO_CL_MONEDA
            """;

        return $"""
            WITH RankedContracts AS
            (
                SELECT
                    C.CTO_FL_CVE AS ContractNumber,
                    C.PNA_FL_PERSONA AS PersonId,
                    P.PNA_DS_NOMBRE AS PersonName,
                    C.TOP_CL_CVE AS OperationTypeCode,
                    O.TOP_DS_DESCRIPCION AS OperationTypeName,
                    C.DMO_FL_CVE AS AddressId,
                    C.CTO_NO_MTO_FINANCIAR AS FinancedAmount,
                    C.CTO_NO_SALDO AS OutstandingBalance,
                    M.MON_CL_CLAVE AS CurrencyCode,
                    M.MON_DS_DESCRIPCION AS CurrencyName,
                    C.CTO_NO_PLAZO AS CurrentTerm,
                    C.CTO_NO_PLAZOORIGINAL AS OriginalTerm,
                    C.CTO_FE_INICIO AS StartDate,
                    C.CTO_FE_ACTIVACION AS ActivationDate,
                    C.CTO_FE_SOL_DESEMBOLSO AS DisbursementDate,
                    C.CTO_FE_PRIMER_PAGO AS FirstPaymentDate,
                    C.CTO_FE_ULTPAGO AS LastPaymentDate,
                    CONVERT(INT, C.CTO_FG_STATUS) AS StatusCode,
                    S.PAR_DS_DESCRIPCION AS StatusName,
                    U.USR_DS_NOMBRE AS ModifiedBy,
                    C.CTO_FE_ULTMOD AS ModifiedAt,
                    ROW_NUMBER() OVER (ORDER BY {sortExpression}) AS RowNumber
                {fromClause}
                {whereClause}
            )
            SELECT
                ContractNumber,
                PersonId,
                PersonName,
                OperationTypeCode,
                OperationTypeName,
                AddressId,
                FinancedAmount,
                OutstandingBalance,
                CurrencyCode,
                CurrencyName,
                CurrentTerm,
                OriginalTerm,
                StartDate,
                ActivationDate,
                DisbursementDate,
                FirstPaymentDate,
                LastPaymentDate,
                StatusCode,
                StatusName,
                ModifiedBy,
                ModifiedAt
            FROM RankedContracts
            WHERE RowNumber BETWEEN @FirstRow AND @LastRow
            ORDER BY RowNumber;
            """;
    }
    internal static string CreateGetByNumberSql() => """
        SELECT TOP (1)
            C.CTO_FL_CVE AS ContractNumber,
            C.PNA_FL_PERSONA AS PersonId,
            P.PNA_DS_NOMBRE AS PersonName,
            C.TOP_CL_CVE AS OperationTypeCode,
            O.TOP_DS_DESCRIPCION AS OperationTypeName,
            C.DMO_FL_CVE AS AddressId,
            C.CTO_NO_MTO_FINANCIAR AS FinancedAmount,
            C.CTO_NO_SALDO AS OutstandingBalance,
            M.MON_CL_CLAVE AS CurrencyCode,
            M.MON_DS_DESCRIPCION AS CurrencyName,
            C.CTO_NO_PLAZO AS CurrentTerm,
            C.CTO_NO_PLAZOORIGINAL AS OriginalTerm,
            C.CTO_FE_INICIO AS StartDate,
            C.CTO_FE_ACTIVACION AS ActivationDate,
            C.CTO_FE_SOL_DESEMBOLSO AS DisbursementDate,
            C.CTO_FE_PRIMER_PAGO AS FirstPaymentDate,
            C.CTO_FE_ULTPAGO AS LastPaymentDate,
            CONVERT(INT, C.CTO_FG_STATUS) AS StatusCode,
            S.PAR_DS_DESCRIPCION AS StatusName,
            U.USR_DS_NOMBRE AS ModifiedBy,
            C.CTO_FE_ULTMOD AS ModifiedAt
        FROM dbo.KCONTRATO AS C
        INNER JOIN dbo.CPERSONA AS P
            ON P.PNA_FL_PERSONA = C.PNA_FL_PERSONA
        INNER JOIN dbo.KTOPERACION AS O
            ON O.TOP_CL_CVE = C.TOP_CL_CVE
        INNER JOIN dbo.CPARAMETRO AS S
            ON S.PAR_FL_CVE = 33
            AND S.PAR_CL_VALOR = C.CTO_FG_STATUS
        LEFT JOIN dbo.CUSUARIO AS U
            ON U.USR_CL_CVE = C.USR_CL_CVE
        LEFT JOIN dbo.CMONEDA AS M
            ON M.MON_FL_CVE = C.CTO_CL_MONEDA
        WHERE C.CTO_FL_CVE = @ContractNumber
          AND C.EMP_FL_CVE IN @AllowedCompanyIds;
        """;
    internal static string CreateCountSql(
        ContractSearchCriteria criteria,
        IReadOnlyCollection<int> allowedCompanyIds)
    {
        var predicates = CreatePredicates(criteria, allowedCompanyIds);
        var whereClause = $"WHERE {string.Join(" AND ", predicates)}";
        const string countFromClause = """
            FROM dbo.KCONTRATO AS C
            INNER JOIN dbo.CPERSONA AS P
                ON P.PNA_FL_PERSONA = C.PNA_FL_PERSONA
            """;

        return $"""
            SELECT COUNT_BIG(1)
            {countFromClause}
            {whereClause};
            """;
    }

    internal static ContractSummary MapRow(LegacyContractSummaryRow row)
    {
        return new ContractSummary(
            Require(row.ContractNumber, nameof(row.ContractNumber)),
            Require(row.PersonId, nameof(row.PersonId)),
            row.PersonName,
            row.OperationTypeCode,
            row.OperationTypeName,
            row.AddressId,
            row.FinancedAmount,
            row.OutstandingBalance,
            row.CurrencyCode,
            row.CurrencyName,
            Require(row.CurrentTerm, nameof(row.CurrentTerm)),
            row.OriginalTerm,
            ToRequiredDateOnly(row.StartDate, nameof(row.StartDate)),
            ToRequiredDateOnly(row.ActivationDate, nameof(row.ActivationDate)),
            ToDateOnly(row.DisbursementDate),
            ToDateOnly(row.FirstPaymentDate),
            ToDateOnly(row.LastPaymentDate),
            row.StatusCode,
            row.StatusName,
            row.ModifiedBy,
            ToDateTimeOffset(row.ModifiedAt));
    }

    private static DateOnly ToRequiredDateOnly(DateTime? value, string fieldName) =>
        DateOnly.FromDateTime(Require(value, fieldName));

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static T Require<T>(T? value, string fieldName)
        where T : struct =>
        value ?? throw new InvalidOperationException($"Legacy contract row requires '{fieldName}'.");

    private static string Require(string? value, string fieldName) =>
        value ?? throw new InvalidOperationException($"Legacy contract row requires '{fieldName}'.");

    internal static DynamicParameters CreateParameters(
        ContractSearchCriteria criteria,
        IReadOnlyCollection<int> allowedCompanyIds)
    {
        var parameters = new DynamicParameters();
        parameters.Add("AllowedCompanyIds", allowedCompanyIds.Distinct().ToArray());

        if (!string.IsNullOrWhiteSpace(criteria.ContractNumber))
        {
            parameters.Add("ContractNumber", criteria.ContractNumber.Trim(), DbType.String, size: 15);
        }

        if (criteria.PersonId is > 0)
        {
            parameters.Add("PersonId", criteria.PersonId.Value, DbType.Int32);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Rfc))
        {
            parameters.Add("Rfc", criteria.Rfc.Trim(), DbType.AnsiString, size: 13);
        }

        if (!string.IsNullOrWhiteSpace(criteria.PersonName))
        {
            parameters.Add("PersonNamePrefix", criteria.PersonName.Trim() + "%", DbType.AnsiString, size: 200);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Vin))
        {
            parameters.Add("Vin", criteria.Vin.Trim(), DbType.String, size: 20);
        }

        if (!string.IsNullOrWhiteSpace(criteria.OperationType))
        {
            parameters.Add("OperationType", criteria.OperationType.Trim(), DbType.String, size: 4);
        }

        if (criteria.Status is not null)
        {
            parameters.Add("Status", criteria.Status.Value, DbType.Int32);
        }

        return parameters;
    }

    internal static List<string> CreatePredicates(
        ContractSearchCriteria criteria,
        IReadOnlyCollection<int> allowedCompanyIds)
    {
        var predicates = new List<string> { "C.EMP_FL_CVE IN @AllowedCompanyIds" };

        if (!string.IsNullOrWhiteSpace(criteria.ContractNumber))
        {
            predicates.Add("C.CTO_FL_CVE = @ContractNumber");
        }

        if (criteria.PersonId is > 0)
        {
            predicates.Add("C.PNA_FL_PERSONA = @PersonId");
        }

        if (!string.IsNullOrWhiteSpace(criteria.Rfc))
        {
            predicates.Add("P.PNA_CL_RFC = @Rfc");
        }

        if (!string.IsNullOrWhiteSpace(criteria.PersonName))
        {
            predicates.Add("P.PNA_DS_NOMBRE LIKE @PersonNamePrefix");
        }

        if (!string.IsNullOrWhiteSpace(criteria.Vin))
        {
            predicates.Add(
                """
                EXISTS
                (
                    SELECT 1
                    FROM dbo.KPRODUCTO_FACTURA AS KPF
                    INNER JOIN dbo.KCARAC_PROD_FACT AS KCF
                        ON KCF.FAC_FL_CVE = KPF.FAC_FL_CVE
                       AND KCF.PRD_FL_CVE = KPF.PRD_FL_CVE
                       AND KCF.KPF_NO_CONSECUTIVO = KPF.KPF_NO_CONSECUTIVO
                    WHERE KPF.CTO_FL_CVE = C.CTO_FL_CVE
                      AND KCF.CAR_FL_CVE = 1
                      AND KCF.CFP_DS_CARACT = @Vin
                )
                """);
        }

        if (!string.IsNullOrWhiteSpace(criteria.OperationType))
        {
            predicates.Add("C.TOP_CL_CVE = @OperationType");
        }

        if (criteria.Status is not null)
        {
            predicates.Add("C.CTO_FG_STATUS = @Status");
        }

        return predicates;
    }

    private async Task<ExecutionTenant> GetExecutionTenantAsync(CancellationToken cancellationToken)
    {
        var executionTenant = await _executionTenantContext.GetAsync(cancellationToken);
        if (executionTenant is null || executionTenant.AllowedCompanyIds.Count == 0)
        {
            throw new InvalidOperationException("No active tenant company scope is available for the Legacy query.");
        }

        return executionTenant;
    }

    private static DynamicParameters CreateContractParameters(
        string contractNumber,
        IReadOnlyCollection<int> allowedCompanyIds)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ContractNumber", contractNumber.Trim(), DbType.String, size: 15);
        parameters.Add("AllowedCompanyIds", allowedCompanyIds.Distinct().ToArray());
        return parameters;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ReadConnectionString))
        {
            throw new InvalidOperationException(
                "LegacySql:ReadConnectionString must be provided by a secure configuration source.");
        }
    }
}
