using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Infrastructure.LegacySql.Contracts;

public sealed class LegacyContractReadRepository(
    IOptions<LegacySqlOptions> options) : IContractReadRepository
{
    private static readonly Dictionary<ContractSort, string> SortExpressions =
        new Dictionary<ContractSort, string>
        {
            [ContractSort.ContractNumberAsc] = "C.CTO_FL_CVE ASC, C.CTO_FL_CVE ASC",
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
        var countSql = $"""
            SELECT COUNT_BIG(1)
            {fromClause}
            {whereClause};
            """;
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

        var items = (await connection.QueryAsync<ContractSummary>(
            new CommandDefinition(
                pageSql,
                parameters,
                commandTimeout: _options.CommandTimeoutSeconds,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken))).AsList();

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

        return await connection.QuerySingleOrDefaultAsync<ContractSummary>(command);
    }

    private static DynamicParameters CreateParameters(ContractSearchCriteria criteria)
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
            parameters.Add("Rfc", criteria.Rfc.Trim(), DbType.String, size: 13);
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

    private static List<string> CreatePredicates(ContractSearchCriteria criteria)
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
            predicates.Add("P.PNA_DS_RFC = @Rfc");
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
