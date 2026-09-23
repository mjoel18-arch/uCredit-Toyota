using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Modules.Customers.Customers;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Infrastructure.LegacySql.Contracts;

public sealed class LegacyContractFoundationRepository(
    IOptions<LegacySqlOptions> options,
    ICustomerAddressReadRepository customerAddressReadRepository)
    : IContractFoundationRepository
{
    private readonly LegacySqlOptions _options = options.Value;
    private readonly ICustomerAddressReadRepository _customerAddressReadRepository = customerAddressReadRepository;

    internal const string OperationsSql = """
        SELECT TOP (100)
            O.TOP_CL_CVE AS Code,
            O.TOP_DS_DESCRIPCION AS Description,
            O.EMP_FL_CVE AS CompanyId
        FROM dbo.KTOPERACION AS O
        WHERE O.TOP_FG_STATUS = 1
          AND O.TOP_FG_PP = 0
          AND O.EMP_FL_CVE IN @CompanyIds
        ORDER BY O.TOP_DS_DESCRIPCION ASC, O.TOP_CL_CVE ASC;
        """;

    internal const string CnbvSql = """
        SELECT
            N.CNB_FL_CVE AS Id,
            N.CNB_DS_DESCRIPCION AS Description,
            CONVERT(bit, CASE WHEN N.CNB_FG_REGDEFAULT = 1 THEN 1 ELSE 0 END) AS IsDefault
        FROM dbo.CCNB AS N
        WHERE N.CNB_FG_STATUS = 1
          AND N.TOP_CL_CVE = @OperationCode
          AND N.EMP_FL_CVE IN @CompanyIds
        ORDER BY N.CNB_FG_REGDEFAULT DESC, N.CNB_DS_DESCRIPCION ASC, N.CNB_FL_CVE ASC;
        """;

    internal const string CustomerSql = """
        SELECT TOP (1)
            P.PNA_FL_PERSONA AS PersonId,
            CONVERT(bit, CASE WHEN P.PNA_FG_STATUS = 1 THEN 1 ELSE 0 END) AS IsActive,
            P.RFI_CL_CLAVE AS TaxRegimeCode,
            CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.CDOMICILIO D WHERE D.PNA_FL_PERSONA = P.PNA_FL_PERSONA AND D.DMO_FG_STATUS = 1) THEN 1 ELSE 0 END) AS HasAddress,
            CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.CTELEFONO T WHERE T.PNA_FL_PERSONA = P.PNA_FL_PERSONA AND T.TFN_FG_STATUS = 1) THEN 1 ELSE 0 END) AS HasPhone,
            CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.CPCUENTA A WHERE A.PNA_FL_PERSONA = P.PNA_FL_PERSONA AND A.PCT_FG_STATUS = 1) THEN 1 ELSE 0 END) AS HasAccount
        FROM dbo.CPERSONA AS P
        WHERE P.PNA_FL_PERSONA = @PersonId;
        """;

    internal const string CfdiUsesSql = """
        SELECT DISTINCT
            U.UCO_CL_CLAVE AS Code,
            U.UCO_DS_DESCRIPCION AS Description
        FROM dbo.CPERSONA AS P
        INNER JOIN dbo.KRELACION_REGIMEN_USOCFDI AS R
            ON R.RFI_CL_CLAVE = P.RFI_CL_CLAVE
           AND R.RRU_FG_STATUS = 1
        INNER JOIN dbo.CUSO_COMPROBANTES AS U
            ON U.UCO_CL_CLAVE = R.UCO_CL_CLAVE
           AND U.UCO_FG_STATUS = 1
        WHERE P.PNA_FL_PERSONA = @PersonId
          AND P.PNA_FG_STATUS = 1
        ORDER BY U.UCO_CL_CLAVE ASC;
        """;

    internal const string OrdinaryRateSql = """
        SELECT TOP (1)
            T.TAS_FL_CVE AS RateId,
            T.TAS_DS_TASA AS Description,
            CONVERT(bit, CASE WHEN T.TAS_FG_TTASA = 1 THEN 1 ELSE 0 END) AS IsFixed,
            T.TAS_CL_MONEDA AS CurrencyCode,
            CONVERT(bit, CASE WHEN T.TAS_FG_STATUS = 1 THEN 1 ELSE 0 END) AS IsActive,
            CONVERT(bit, CASE WHEN T.TAS_FG_REVISION = 1 THEN 1 ELSE 0 END) AS HasRevision
        FROM dbo.CTASA AS T
        WHERE T.TAS_FL_CVE = 1
          AND T.TAS_FG_STATUS = 1
          AND T.TAS_FG_TTASA = 1
          AND T.TAS_CL_MONEDA = @CurrencyCode
          AND T.TAS_FG_REVISION = 0
        ORDER BY T.TAS_FL_CVE;
        """;

    internal const string LateRateSql = """
        SELECT TOP (1)
            M.TAS_FL_CVE AS RateId,
            M.TLO_FL_CVE AS CalculationTypeId,
            M.TMR_NO_PUNTOS AS Points,
            M.TMR_NO_FACTOR AS Factor,
            M.TMR_CL_MONEDA AS CurrencyCode,
            CONVERT(bit, 1) AS IsConfigured
        FROM dbo.CTMORATORIO AS M
        INNER JOIN dbo.KTOPERACION AS O ON O.TOP_CL_CVE = M.TOP_CL_CVE
        WHERE M.TOP_CL_CVE = @OperationCode
          AND M.TMR_CL_MONEDA = @CurrencyCode
          AND O.TOP_FG_STATUS = 1
          AND O.TOP_FG_PP = 0
        ORDER BY M.TMR_FL_CVE;
        """;

    internal const string BusinessDateSql = """
        SELECT CFECHA_OPERACION
        FROM dbo.CFECHA_OPERACION;
        """;

    public Task<IReadOnlyList<ContractOperationCatalog>> GetOperationsAsync(IReadOnlyCollection<int> companyIds, CancellationToken cancellationToken = default) =>
        QueryAsync<ContractOperationCatalog>(OperationsSql, new { CompanyIds = companyIds }, cancellationToken);

    public Task<IReadOnlyList<ContractCnbvCatalog>> GetCnbvAsync(string operationCode, IReadOnlyCollection<int> companyIds, CancellationToken cancellationToken = default) =>
        QueryAsync<ContractCnbvCatalog>(CnbvSql, new { OperationCode = operationCode.Trim(), CompanyIds = companyIds }, cancellationToken);

    public async Task<ContractCustomerContext?> GetCustomerAsync(int personId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<CustomerRow>(CustomerSql, new { PersonId = personId }, cancellationToken);
        return rows.SingleOrDefault() is { } row
            ? new ContractCustomerContext(row.PersonId, row.IsActive, row.TaxRegimeCode, row.HasAddress, row.HasPhone, row.HasAccount)
            : null;
    }

    public Task<IReadOnlyList<ContractCfdiUseCatalog>> GetCfdiUsesAsync(int personId, CancellationToken cancellationToken = default) =>
        QueryAsync<ContractCfdiUseCatalog>(CfdiUsesSql, new { PersonId = personId }, cancellationToken);

    public async Task<IReadOnlyList<ContractAddressOption>?> GetActiveAddressesAsync(int personId, CancellationToken cancellationToken = default)
    {
        var addresses = await _customerAddressReadRepository.GetByPersonIdAsync(personId, cancellationToken);
        if (addresses is null)
            return null;

        return addresses
            .Where(address => address.IsActive)
            .Select(address => new ContractAddressOption(
                address.AddressId,
                address.AddressTypeCode,
                address.AddressTypeDescription?.Trim()))
            .ToArray();
    }

    public async Task<ContractRateConfiguration?> GetOrdinaryRateAsync(string operationCode, int currencyCode, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<ContractRateConfiguration>(OrdinaryRateSql, new { OperationCode = operationCode.Trim(), CurrencyCode = currencyCode }, cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<ContractLateRateConfiguration?> GetLateRateAsync(string operationCode, int currencyCode, CancellationToken cancellationToken = default)
    {
        var rows = await QueryAsync<ContractLateRateConfiguration>(LateRateSql, new { OperationCode = operationCode.Trim(), CurrencyCode = currencyCode }, cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<DateOnly?> GetBusinessDateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        var values = (await connection.QueryAsync<DateTime>(new CommandDefinition(BusinessDateSql, commandTimeout: _options.CommandTimeoutSeconds, commandType: CommandType.Text, cancellationToken: cancellationToken))).ToArray();
        return values.Length == 1 ? DateOnly.FromDateTime(values[0]) : null;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, commandTimeout: _options.CommandTimeoutSeconds, commandType: CommandType.Text, cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private SqlConnection CreateConnection() => new(_options.ReadConnectionString);

    private sealed class CustomerRow
    {
        public int PersonId { get; set; }
        public bool IsActive { get; set; }
        public string? TaxRegimeCode { get; set; }
        public bool HasAddress { get; set; }
        public bool HasPhone { get; set; }
        public bool HasAccount { get; set; }
    }
}
