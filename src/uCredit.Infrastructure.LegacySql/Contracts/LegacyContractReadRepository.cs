using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Infrastructure.LegacySql.Contracts;

public sealed class LegacyContractReadRepository(
    IOptions<LegacySqlOptions> options) : IContractReadRepository
{
    private readonly LegacySqlOptions _options = options.Value;

    public async Task<PagedResult<ContractSummary>> SearchAsync(
        ContractSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        // The executable SQL will be added after validating database version,
        // collation, tenant scope and representative query plans.
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        return new PagedResult<ContractSummary>([], criteria.Page, criteria.PageSize, 0);
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

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ReadConnectionString))
        {
            throw new InvalidOperationException(
                "LegacySql:ReadConnectionString must be provided by a secure configuration source.");
        }
    }
}

