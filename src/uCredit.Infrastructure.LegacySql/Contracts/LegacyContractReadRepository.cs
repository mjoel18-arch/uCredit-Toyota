using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
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
    public DateTime? DisbursementDate { get; set; }
    public DateTime? FirstPaymentDate { get; set; }
    public DateTime? LastPaymentDate { get; set; }
    public int? StatusCode { get; set; }
    public string? StatusName { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
public sealed class LegacyContractReadRepository(
    IOptions<LegacySqlOptions> options) : IContractReadRepository
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

        EnsureConfigured();

        var parameters = CreateParameters(criteria);
        var predicates = CreatePredicates(criteria);
        var sortExpression = SortExpressions[criteria.Sort];
        var firstRow = ((long)criteria.Page - 1) * criteria.PageSize + 1;
        var lastRow = (long)criteria.Page * criteria.PageSize;
        parameters.Add("FirstRow", firstRow, DbType.Int64);
        parameters.Add("LastRow", lastRow, DbType.Int64);

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
            """;

        var whereClause = $"WHERE {string.Join(" AND ", predicates)}";
        var countSql = CreateCountSql(criteria);
        var pageSql = $"""
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
        const string sql = """
            SELECT TOP (1)
                C.CTO_FL_CVE AS ContractNumber,
                C.PNA_FL_PERSONA AS PersonId,
                P.PNA_DS_NOMBRE AS PersonName,
                C.TOP_CL_CVE AS OperationTypeCode,
                O.TOP_DS_DESCRIPCION AS OperationTypeName,
                C.DMO_FL_CVE AS AddressId,
                C.CTO_NO_MTO_FINANCIAR AS FinancedAmount,
                C.CTO_NO_SALDO AS OutstandingBalance,
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
            WHERE C.CTO_FL_CVE = @ContractNumber;
            """;

        EnsureConfigured();

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        var command = new CommandDefinition(
            sql,
            new { ContractNumber = contractNumber.Trim() },
            commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<LegacyContractSummaryRow>(command);
        return row is null ? null : MapRow(row);
    }

    internal static string CreateCountSql(ContractSearchCriteria criteria)
    {
        var predicates = CreatePredicates(criteria);
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
            Require(row.PersonName, nameof(row.PersonName)),
            Require(row.OperationTypeCode, nameof(row.OperationTypeCode)),
            Require(row.OperationTypeName, nameof(row.OperationTypeName)),
            row.AddressId,
            Require(row.FinancedAmount, nameof(row.FinancedAmount)),
            Require(row.OutstandingBalance, nameof(row.OutstandingBalance)),
            ToDateOnly(row.DisbursementDate),
            ToDateOnly(row.FirstPaymentDate),
            ToDateOnly(row.LastPaymentDate),
            Require(row.StatusCode, nameof(row.StatusCode)),
            Require(row.StatusName, nameof(row.StatusName)),
            row.ModifiedBy,
            ToDateTimeOffset(row.ModifiedAt));
    }

    private static DateOnly? ToDateOnly(DateTime? value) =>
        value is null ? null : DateOnly.FromDateTime(value.Value);

    // Legacy datetime has no offset; this adapter treats it as UTC at the boundary.
    private static DateTimeOffset? ToDateTimeOffset(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static T Require<T>(T? value, string fieldName)
        where T : struct =>
        value ?? throw new InvalidOperationException($"Legacy contract row requires '{fieldName}'.");

    private static string Require(string? value, string fieldName) =>
        value ?? throw new InvalidOperationException($"Legacy contract row requires '{fieldName}'.");
    internal static DynamicParameters CreateParameters(ContractSearchCriteria criteria)
    {
        var parameters = new DynamicParameters();
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
			parameters.Add(
				"Vin",
				criteria.Vin.Trim(),
				DbType.String,
				size: 20);
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

    internal static List<string> CreatePredicates(ContractSearchCriteria criteria)
    {
        var predicates = new List<string>();
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

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ReadConnectionString))
        {
            throw new InvalidOperationException(
                "LegacySql:ReadConnectionString must be provided by a secure configuration source.");
        }
    }
}
