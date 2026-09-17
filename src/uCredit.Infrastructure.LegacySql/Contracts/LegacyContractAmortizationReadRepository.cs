using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Infrastructure.LegacySql.Contracts;

internal sealed class LegacyContractAmortizationRow
{
    public long? PaymentId { get; set; }
    public string? ContractNumber { get; set; }
    public int? PaymentNumber { get; set; }
    public int? Version { get; set; }
    public int? GeneratedFlag { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? CalculationBase { get; set; }
    public decimal? OutstandingBalance { get; set; }
    public decimal? Amortization { get; set; }
    public decimal? Interest { get; set; }
    public decimal? Iva { get; set; }
    public decimal? Payment { get; set; }
    public decimal? PaymentWithIva { get; set; }
    public decimal? TotalPayment { get; set; }
}

public sealed class LegacyContractAmortizationReadRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext executionTenantContext) : IContractAmortizationReadRepository
{
    private const int FinancingType = 1;

    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _executionTenantContext = executionTenantContext;

    public async Task<ContractAmortizationSchedule?> GetScheduleByNumberAsync(
        string contractNumber,
        CancellationToken cancellationToken = default)
    {
        var executionTenant = await GetExecutionTenantAsync(cancellationToken);
        EnsureConfigured();

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        var rows = (await connection.QueryAsync<LegacyContractAmortizationRow>(
            new CommandDefinition(
                CreateScheduleSql(),
                CreateParameters(contractNumber, executionTenant.AllowedCompanyIds),
                commandTimeout: _options.CommandTimeoutSeconds,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken))).ToArray();

        return MapRows(rows);
    }

    internal static string CreateScheduleSql() => """
        SELECT
            P.CTP_FL_CVE AS PaymentId,
            C.CTO_FL_CVE AS ContractNumber,
            P.CTP_NO_PAGO AS PaymentNumber,
            P.CTP_NO_VERSION AS Version,
            P.CTP_FG_GENERADO AS GeneratedFlag,
            P.CTP_FE_INICIO AS StartDate,
            P.CTP_FE_FINAL AS EndDate,
            P.CTP_FE_EXIGIBILIDAD AS DueDate,
            P.CTP_NO_BASE_CALCULO AS CalculationBase,
            P.CTP_NO_MTO_SINSOLUTO AS OutstandingBalance,
            P.CTP_NO_MTO_AMORTIZACION AS Amortization,
            P.CTP_NO_MTO_INTERES AS Interest,
            P.CTP_NO_MTO_IVA AS Iva,
            P.CTP_NO_MTO_PAGO AS Payment,
            P.CTP_NO_MTO_PG_CON_IVA AS PaymentWithIva,
            P.CTP_NO_MTO_TOTPAGO AS TotalPayment
        FROM dbo.KTPAGO_CONTRATO AS P
        INNER JOIN dbo.KCONTRATO AS C
            ON C.CTO_FL_CVE = P.CTO_FL_CVE
        WHERE C.CTO_FL_CVE = @ContractNumber
          AND C.EMP_FL_CVE IN @AllowedCompanyIds
          AND P.CTP_CL_TTABLA = @FinancingType
          AND P.CTP_NO_VERSION =
          (
              SELECT MAX(V.CTP_NO_VERSION)
              FROM dbo.KTPAGO_CONTRATO AS V
              WHERE V.CTO_FL_CVE = C.CTO_FL_CVE
                AND V.CTP_CL_TTABLA = @FinancingType
          )
        ORDER BY P.CTP_NO_PAGO ASC;
        """;

    internal static DynamicParameters CreateParameters(
        string contractNumber,
        IReadOnlyCollection<int> allowedCompanyIds)
    {
        var parameters = new DynamicParameters();
        parameters.Add("ContractNumber", contractNumber.Trim(), DbType.String, size: 15);
        parameters.Add("AllowedCompanyIds", allowedCompanyIds.Distinct().ToArray());
        parameters.Add("FinancingType", FinancingType, DbType.Int32);
        return parameters;
    }

    internal static ContractAmortizationSchedule? MapRows(
        IReadOnlyList<LegacyContractAmortizationRow> rows)
    {
        if (rows.Count == 0)
        {
            return null;
        }

        var contractNumber = Require(rows[0].ContractNumber, nameof(LegacyContractAmortizationRow.ContractNumber));
        var version = Require(rows[0].Version, nameof(LegacyContractAmortizationRow.Version));
        var payments = rows
            .Select(MapRow)
            .OrderBy(payment => payment.PaymentNumber)
            .ToArray();
        var downPayments = payments.Where(payment => payment.PaymentNumber == 0).ToArray();
        if (downPayments.Length > 1)
        {
            throw new InvalidOperationException("Legacy amortization schedule contains multiple down payments.");
        }

        if (payments.Any(payment => payment.PaymentNumber < 0))
        {
            throw new InvalidOperationException("Legacy amortization schedule contains an invalid payment number.");
        }

        return new ContractAmortizationSchedule(
            contractNumber,
            FinancingType,
            version,
            downPayments.SingleOrDefault(),
            payments.Where(payment => payment.PaymentNumber > 0).ToArray());
    }

    private static ContractAmortizationPayment MapRow(LegacyContractAmortizationRow row) => new(
        Require(row.PaymentId, nameof(row.PaymentId)),
        Require(row.PaymentNumber, nameof(row.PaymentNumber)),
        Require(row.Version, nameof(row.Version)),
        MapStatus(row.GeneratedFlag),
        ToRequiredDateOnly(row.StartDate, nameof(row.StartDate)),
        ToRequiredDateOnly(row.EndDate, nameof(row.EndDate)),
        ToRequiredDateOnly(row.DueDate, nameof(row.DueDate)),
        Require(row.CalculationBase, nameof(row.CalculationBase)),
        Require(row.OutstandingBalance, nameof(row.OutstandingBalance)),
        Require(row.Amortization, nameof(row.Amortization)),
        Require(row.Interest, nameof(row.Interest)),
        Require(row.Iva, nameof(row.Iva)),
        Require(row.Payment, nameof(row.Payment)),
        Require(row.PaymentWithIva, nameof(row.PaymentWithIva)),
        Require(row.TotalPayment, nameof(row.TotalPayment)));

    private static ContractAmortizationPaymentStatus MapStatus(int? value) =>
        value switch
        {
            0 => ContractAmortizationPaymentStatus.Pending,
            1 => ContractAmortizationPaymentStatus.Generated,
            _ => throw new InvalidOperationException("Legacy amortization schedule contains an invalid generated flag.")
        };

    private static DateOnly ToRequiredDateOnly(DateTime? value, string fieldName) =>
        DateOnly.FromDateTime(Require(value, fieldName));

    private static T Require<T>(T? value, string fieldName)
        where T : struct =>
        value ?? throw new InvalidOperationException($"Legacy amortization row requires '{fieldName}'.");

    private static string Require(string? value, string fieldName) =>
        value ?? throw new InvalidOperationException($"Legacy amortization row requires '{fieldName}'.");

    private async Task<ExecutionTenant> GetExecutionTenantAsync(CancellationToken cancellationToken)
    {
        var executionTenant = await _executionTenantContext.GetAsync(cancellationToken);
        if (executionTenant is null || executionTenant.AllowedCompanyIds.Count == 0)
        {
            throw new InvalidOperationException("No active tenant company scope is available for the Legacy query.");
        }

        return executionTenant;
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
