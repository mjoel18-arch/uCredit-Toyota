using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Modules.Customers.Customers;

namespace UCredit.Infrastructure.LegacySql.Customers;

internal sealed class LegacyCustomerBaseRow
{
    public int? PersonId { get; set; }
    public string? Rfc { get; set; }
    public string? Name { get; set; }
    public int? LegalPersonalityCode { get; set; }
    public string? LegalPersonalityDescription { get; set; }
    public int? StatusCode { get; set; }
    public string? StatusDescription { get; set; }
}

internal sealed class LegacyCustomerAddressRow
{
    public int? PersonId { get; set; }
    public int? AddressId { get; set; }
    public string? PostalCode { get; set; }
    public string? State { get; set; }
    public string? Municipality { get; set; }
    public string? City { get; set; }
    public string? Neighborhood { get; set; }
    public string? StreetAndNumber { get; set; }
    public string? ExteriorNumber { get; set; }
    public string? InteriorNumber { get; set; }
    public int? AddressTypeCode { get; set; }
    public string? AddressTypeDescription { get; set; }
}

internal sealed class LegacyCustomerPhoneRow
{
    public int? PersonId { get; set; }
    public int? PhoneId { get; set; }
    public string? AreaCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Extension { get; set; }
    public byte? IsDefault { get; set; }
}

internal sealed class LegacyCustomerEmailRow
{
    public int? PersonId { get; set; }
    public int? EmailId { get; set; }
    public string? Contact { get; set; }
    public string? Email { get; set; }
}

internal sealed class LegacyCustomerRoleRow
{
    public int? PersonId { get; set; }
    public int? Code { get; set; }
    public string? Description { get; set; }
}

public sealed class LegacyCustomerReadRepository(
    IOptions<LegacySqlOptions> options,
    IExecutionTenantContext executionTenantContext) : ICustomerReadRepository
{
    private static readonly Dictionary<CustomerSort, string> SortExpressions = new()
    {
        [CustomerSort.PersonIdAsc] = "P.PNA_FL_PERSONA ASC",
        [CustomerSort.NameAsc] = "P.PNA_DS_NOMBRE ASC, P.PNA_FL_PERSONA ASC",
        [CustomerSort.NameDesc] = "P.PNA_DS_NOMBRE DESC, P.PNA_FL_PERSONA ASC",
    };

    private readonly LegacySqlOptions _options = options.Value;
    private readonly IExecutionTenantContext _executionTenantContext = executionTenantContext;

    public async Task<PagedCustomers> SearchAsync(
        CustomerSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        EnsureValid(criteria);
        await GetExecutionTenantAsync(cancellationToken);
        EnsureConfigured();

        var firstRow = ((long)criteria.Page - 1) * criteria.PageSize + 1;
        var lastRow = (long)criteria.Page * criteria.PageSize;
        var parameters = CreateParameters(criteria, firstRow, lastRow);

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        var total = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            CreateCountSql(criteria), parameters, commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text, cancellationToken: cancellationToken));
        var rows = (await connection.QueryAsync<LegacyCustomerBaseRow>(new CommandDefinition(
            CreateSearchSql(criteria), parameters, commandTimeout: _options.CommandTimeoutSeconds,
            commandType: CommandType.Text, cancellationToken: cancellationToken))).ToList();

        var customers = await LoadRelatedAsync(connection, rows, cancellationToken);
        return new PagedCustomers(customers, criteria.Page, criteria.PageSize, checked((int)total));
    }

    public async Task<Customer?> GetByPersonIdAsync(int personId, CancellationToken cancellationToken = default)
    {
        if (personId <= 0) throw new ArgumentException("personId must be greater than zero.", nameof(personId));
        await GetExecutionTenantAsync(cancellationToken);
        EnsureConfigured();

        await using var connection = new SqlConnection(_options.ReadConnectionString);
        var row = await connection.QuerySingleOrDefaultAsync<LegacyCustomerBaseRow>(new CommandDefinition(
            CreateGetByPersonIdSql(), new { PersonId = personId },
            commandTimeout: _options.CommandTimeoutSeconds, commandType: CommandType.Text,
            cancellationToken: cancellationToken));
        if (row is null) return null;

        var customers = await LoadRelatedAsync(connection, [row], cancellationToken);
        return customers.Single();
    }

    internal static string CreateSearchSql(CustomerSearchCriteria criteria) => $"""
        WITH RankedCustomers AS
        (
            SELECT
                P.PNA_FL_PERSONA AS PersonId,
                P.PNA_CL_RFC AS Rfc,
                COALESCE(NULLIF(F.PFI_DS_NOMBRE + CASE WHEN F.PFI_DS_APATERNO IS NULL THEN '' ELSE ' ' + F.PFI_DS_APATERNO END + CASE WHEN F.PFI_DS_AMATERNO IS NULL THEN '' ELSE ' ' + F.PFI_DS_AMATERNO END, ''), NULLIF(M.PMO_DS_RAZON_SOCIAL, ''), P.PNA_DS_NOMBRE) AS Name,
                P.PNA_CL_PJURIDICA AS LegalPersonalityCode,
                J.PAR_DS_DESCRIPCION AS LegalPersonalityDescription,
                P.PNA_FG_STATUS AS StatusCode,
                S.PAR_DS_DESCRIPCION AS StatusDescription,
                ROW_NUMBER() OVER (ORDER BY {SortExpressions[criteria.Sort]}) AS RowNumber
            FROM dbo.CPERSONA AS P
            OUTER APPLY (SELECT TOP (1) F.PFI_DS_NOMBRE, F.PFI_DS_APATERNO, F.PFI_DS_AMATERNO FROM dbo.CPFISICA AS F WHERE F.PNA_FL_PERSONA = P.PNA_FL_PERSONA ORDER BY F.PNA_FL_PERSONA) AS F
            OUTER APPLY (SELECT TOP (1) M.PMO_DS_RAZON_SOCIAL FROM dbo.CPMORAL AS M WHERE M.PNA_FL_PERSONA = P.PNA_FL_PERSONA ORDER BY M.PNA_FL_PERSONA) AS M
            LEFT JOIN dbo.CPARAMETRO AS S ON S.PAR_FL_CVE = 1 AND S.PAR_CL_VALOR = P.PNA_FG_STATUS
            LEFT JOIN dbo.CPARAMETRO AS J ON J.PAR_FL_CVE = 6 AND J.PAR_CL_VALOR = P.PNA_CL_PJURIDICA
            WHERE 1 = 1
              {CreatePredicates(criteria)}
        )
        SELECT PersonId, Rfc, Name, LegalPersonalityCode, LegalPersonalityDescription, StatusCode, StatusDescription
        FROM RankedCustomers
        WHERE RowNumber BETWEEN @FirstRow AND @LastRow
        ORDER BY RowNumber;
        """;

    internal static string CreateCountSql(CustomerSearchCriteria criteria) => $"""
        SELECT COUNT_BIG(1)
        FROM dbo.CPERSONA AS P
        WHERE 1 = 1
          {CreatePredicates(criteria)};
        """;

    internal static string CreateGetByPersonIdSql() => """
        SELECT TOP (1)
            P.PNA_FL_PERSONA AS PersonId,
            P.PNA_CL_RFC AS Rfc,
            COALESCE(NULLIF(F.PFI_DS_NOMBRE + CASE WHEN F.PFI_DS_APATERNO IS NULL THEN '' ELSE ' ' + F.PFI_DS_APATERNO END + CASE WHEN F.PFI_DS_AMATERNO IS NULL THEN '' ELSE ' ' + F.PFI_DS_AMATERNO END, ''), NULLIF(M.PMO_DS_RAZON_SOCIAL, ''), P.PNA_DS_NOMBRE) AS Name,
            P.PNA_CL_PJURIDICA AS LegalPersonalityCode,
            J.PAR_DS_DESCRIPCION AS LegalPersonalityDescription,
            P.PNA_FG_STATUS AS StatusCode,
            S.PAR_DS_DESCRIPCION AS StatusDescription
        FROM dbo.CPERSONA AS P
        OUTER APPLY (SELECT TOP (1) F.PFI_DS_NOMBRE, F.PFI_DS_APATERNO, F.PFI_DS_AMATERNO FROM dbo.CPFISICA AS F WHERE F.PNA_FL_PERSONA = P.PNA_FL_PERSONA ORDER BY F.PNA_FL_PERSONA) AS F
        OUTER APPLY (SELECT TOP (1) M.PMO_DS_RAZON_SOCIAL FROM dbo.CPMORAL AS M WHERE M.PNA_FL_PERSONA = P.PNA_FL_PERSONA ORDER BY M.PNA_FL_PERSONA) AS M
        LEFT JOIN dbo.CPARAMETRO AS S ON S.PAR_FL_CVE = 1 AND S.PAR_CL_VALOR = P.PNA_FG_STATUS
        LEFT JOIN dbo.CPARAMETRO AS J ON J.PAR_FL_CVE = 6 AND J.PAR_CL_VALOR = P.PNA_CL_PJURIDICA
        WHERE P.PNA_FL_PERSONA = @PersonId;
        """;

    internal static string CreateRolesSql() => """
        SELECT T.PNA_FL_PERSONA AS PersonId, T.PTI_FG_VALOR AS Code, C.PAR_DS_DESCRIPCION AS Description
        FROM dbo.CPTIPO AS T
        LEFT JOIN dbo.CPARAMETRO AS C ON C.PAR_FL_CVE = 5 AND C.PAR_CL_VALOR = T.PTI_FG_VALOR
        WHERE T.PNA_FL_PERSONA IN @PersonIds
          AND EXISTS (SELECT 1 FROM dbo.CPARAMETRO AS A WHERE A.PAR_FL_CVE = 5 AND A.PAR_CL_VALOR = T.PTI_FG_VALOR AND A.PAR_FG_STATUS = 1)
        ORDER BY T.PNA_FL_PERSONA, T.PTI_FG_VALOR;
        """;

    internal static string CreateAddressesSql() => """
        WITH DefaultAddresses AS
        (
            SELECT D.PNA_FL_PERSONA AS PersonId, D.DMO_FL_CVE AS AddressId, D.DMO_CL_CPOSTAL AS PostalCode,
                D.DMO_DS_EFEDERATIVA AS State, D.DMO_DS_MUNICIPIO AS Municipality, D.DMO_DS_CIUDAD AS City,
                D.DMO_DS_COLONIA AS Neighborhood, D.DMO_DS_CALLE_NUM AS StreetAndNumber,
                D.DMO_DS_NUMEXT AS ExteriorNumber, D.DMO_DS_NUMINT AS InteriorNumber,
                D.DMO_FG_TDIRECCION AS AddressTypeCode, C.PAR_DS_DESCRIPCION AS AddressTypeDescription,
                ROW_NUMBER() OVER (PARTITION BY D.PNA_FL_PERSONA ORDER BY D.DMO_FL_CVE ASC) AS RowNumber
            FROM dbo.CDOMICILIO AS D
            LEFT JOIN dbo.CPARAMETRO AS C ON C.PAR_FL_CVE = 7 AND C.PAR_CL_VALOR = D.DMO_FG_TDIRECCION
            WHERE D.PNA_FL_PERSONA IN @PersonIds AND D.DMO_FG_STATUS = 1 AND D.DMO_FG_REGDEFAULT = 1
        )
        SELECT PersonId, AddressId, PostalCode, State, Municipality, City, Neighborhood, StreetAndNumber,
            ExteriorNumber, InteriorNumber, AddressTypeCode, AddressTypeDescription
        FROM DefaultAddresses WHERE RowNumber = 1;
        """;

    internal static string CreatePhonesSql() => """
        SELECT PNA_FL_PERSONA AS PersonId, TFN_FL_CVE AS PhoneId, TFN_CL_LADA AS AreaCode,
            TFN_CL_TELEFONO AS PhoneNumber, TFN_CL_EXTENSION AS Extension, TFN_FG_REGDEFAULT AS IsDefault
        FROM dbo.CTELEFONO
        WHERE PNA_FL_PERSONA IN @PersonIds AND TFN_FG_STATUS = 1
        ORDER BY PNA_FL_PERSONA, TFN_FG_REGDEFAULT DESC, TFN_FL_CVE ASC;
        """;

    internal static string CreateEmailsSql() => """
        SELECT PNA_FL_PERSONA AS PersonId, MAI_FL_CVE AS EmailId, MAI_DS_CONTACTO AS Contact, MAI_DS_EMAIL AS Email
        FROM dbo.CPERSONA_EMAIL
        WHERE PNA_FL_PERSONA IN @PersonIds AND MAI_FG_STATUS = 1
        ORDER BY PNA_FL_PERSONA, MAI_FL_CVE ASC;
        """;

    internal static DynamicParameters CreateParameters(CustomerSearchCriteria criteria, long firstRow = 1, long lastRow = long.MaxValue)
    {
        var parameters = new DynamicParameters();
        if (criteria.PersonId is not null) parameters.Add("PersonId", criteria.PersonId.Value, DbType.Int32);
        if (!string.IsNullOrWhiteSpace(criteria.Rfc)) parameters.Add("Rfc", criteria.Rfc.Trim(), DbType.String, size: CustomerSearchValidator.MaxRfcLength);
        if (!string.IsNullOrWhiteSpace(criteria.Name)) parameters.Add("NamePrefix", criteria.Name.Trim() + "%", DbType.String, size: CustomerSearchValidator.MaxNameLength + 1);
        if (criteria.LegalPersonalityCode is not null) parameters.Add("LegalPersonalityCode", criteria.LegalPersonalityCode.Value, DbType.Int32);
        parameters.Add("FirstRow", firstRow, DbType.Int64);
        parameters.Add("LastRow", lastRow, DbType.Int64);
        return parameters;
    }

    private static string CreatePredicates(CustomerSearchCriteria criteria)
    {
        var predicates = new List<string>();
        if (criteria.PersonId is not null) predicates.Add("P.PNA_FL_PERSONA = @PersonId");
        if (!string.IsNullOrWhiteSpace(criteria.Rfc)) predicates.Add("P.PNA_CL_RFC = @Rfc");
        if (!string.IsNullOrWhiteSpace(criteria.Name)) predicates.Add("P.PNA_DS_NOMBRE LIKE @NamePrefix");
        if (criteria.LegalPersonalityCode is not null) predicates.Add("P.PNA_CL_PJURIDICA = @LegalPersonalityCode");
        return predicates.Count == 0 ? string.Empty : "AND " + string.Join(" AND ", predicates);
    }

    private async Task<IReadOnlyList<Customer>> LoadRelatedAsync(
        SqlConnection connection,
        IReadOnlyList<LegacyCustomerBaseRow> rows,
        CancellationToken cancellationToken)
    {
        var personIds = rows.Select(row => Require(row.PersonId, nameof(row.PersonId))).ToArray();
        var parameters = new { PersonIds = personIds };
        var roles = await connection.QueryAsync<LegacyCustomerRoleRow>(new CommandDefinition(CreateRolesSql(), parameters, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken));
        var addresses = await connection.QueryAsync<LegacyCustomerAddressRow>(new CommandDefinition(CreateAddressesSql(), parameters, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken));
        var phones = await connection.QueryAsync<LegacyCustomerPhoneRow>(new CommandDefinition(CreatePhonesSql(), parameters, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken));
        var emails = await connection.QueryAsync<LegacyCustomerEmailRow>(new CommandDefinition(CreateEmailsSql(), parameters, commandTimeout: _options.CommandTimeoutSeconds, cancellationToken: cancellationToken));

        return rows.Select(row =>
        {
            var personId = Require(row.PersonId, nameof(row.PersonId));
            var activePhones = phones.Where(phone => phone.PersonId == personId).Select(MapPhone).ToArray();
            return new Customer(personId, row.Rfc, Require(row.Name, nameof(row.Name)), Require(row.LegalPersonalityCode, nameof(row.LegalPersonalityCode)), row.LegalPersonalityDescription, Require(row.StatusCode, nameof(row.StatusCode)), row.StatusDescription,
                addresses.Where(address => address.PersonId == personId).Select(MapAddress).FirstOrDefault(),
                CustomerPhoneSelector.SelectPrimary(activePhones),
                roles.Where(role => role.PersonId == personId).Select(role => new CustomerRole(Require(role.Code, nameof(role.Code)), role.Description)).ToArray(),
                activePhones,
                emails.Where(email => email.PersonId == personId).Select(email => new CustomerEmail(Require(email.EmailId, nameof(email.EmailId)), email.Contact, email.Email)).ToArray());
        }).ToArray();
    }

    private static CustomerAddress MapAddress(LegacyCustomerAddressRow row) => new(Require(row.AddressId, nameof(row.AddressId)), row.PostalCode, row.State, row.Municipality, row.City, row.Neighborhood, row.StreetAndNumber, row.ExteriorNumber, row.InteriorNumber, Require(row.AddressTypeCode, nameof(row.AddressTypeCode)), row.AddressTypeDescription);
    private static CustomerPhone MapPhone(LegacyCustomerPhoneRow row) => new(Require(row.PhoneId, nameof(row.PhoneId)), row.AreaCode, row.PhoneNumber, row.Extension, row.IsDefault == 1);
    private static int Require(int? value, string name) => value ?? throw new InvalidOperationException($"Required customer field {name} was null.");
    private static string Require(string? value, string name) => value ?? throw new InvalidOperationException($"Required customer field {name} was null.");

    private static void EnsureValid(CustomerSearchCriteria criteria)
    {
        var errors = CustomerSearchValidator.Validate(criteria);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors.SelectMany(error => error.Value)), nameof(criteria));
    }

    private async Task<ExecutionTenant> GetExecutionTenantAsync(CancellationToken cancellationToken) =>
        await _executionTenantContext.GetAsync(cancellationToken) ?? throw new InvalidOperationException("No execution tenant is selected.");

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ReadConnectionString)) throw new InvalidOperationException("Legacy read connection is not configured.");
    }
}
