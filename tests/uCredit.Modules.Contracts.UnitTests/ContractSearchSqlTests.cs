using UCredit.Infrastructure.LegacySql.Contracts;
using UCredit.Modules.Contracts.Contracts;

using System.Collections;
using System.Data;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.Options;
using UCredit.Application.Execution;
using UCredit.Infrastructure.LegacySql;

namespace UCredit.Modules.Contracts.UnitTests;

public sealed class ContractSearchSqlTests
{
    [Fact]
    public void VinUsesExactMatchWithConfirmedCharacteristic()
    {
        var criteria = CreateCriteria(vin: "  VIN-123  ");
        var parameters = LegacyContractReadRepository.CreateParameters(criteria, [101, 202]);
        var vinPredicate = LegacyContractReadRepository.CreatePredicates(criteria, [101, 202]).Single(predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));

        Assert.Equal("VIN-123", parameters.Get<string>("Vin"));
        Assert.Contains("KCF.CFP_DS_CARACT = @Vin", vinPredicate);
        Assert.Contains("KCF.CAR_FL_CVE = 1", vinPredicate);
        Assert.Contains("KCF.FAC_FL_CVE = KPF.FAC_FL_CVE", vinPredicate);
        Assert.Contains("KCF.PRD_FL_CVE = KPF.PRD_FL_CVE", vinPredicate);
        Assert.Contains("KCF.KPF_NO_CONSECUTIVO = KPF.KPF_NO_CONSECUTIVO", vinPredicate);
    }

    [Fact]
    public void VinUsesExistsToAvoidDuplicateContracts()
    {
        var vinPredicate = LegacyContractReadRepository.CreatePredicates(CreateCriteria(vin: "VIN-123"), [101, 202])
            .Single(predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));

        Assert.StartsWith("EXISTS", vinPredicate, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN dbo.KPRODUCTO_FACTURA", vinPredicate, StringComparison.Ordinal);
    }

    [Fact]
    public void VinParameterIsTrimmedAndNotConcatenatedIntoSql()
    {
        var parameters = LegacyContractReadRepository.CreateParameters(CreateCriteria(vin: "  VIN-123  "), [101, 202]);
        var vinPredicate = LegacyContractReadRepository.CreatePredicates(CreateCriteria(vin: "VIN-123"), [101, 202])
            .Single(predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));

        Assert.Equal("VIN-123", parameters.Get<string>("Vin"));
        Assert.DoesNotContain("VIN-123", vinPredicate);
    }

    [Fact]
    public void EmptyVinProducesNoVinParameterOrPredicate()
    {
        var criteria = CreateCriteria(vin: "   ");

        Assert.DoesNotContain("Vin", LegacyContractReadRepository.CreateParameters(criteria, [101, 202]).ParameterNames);
        Assert.DoesNotContain(LegacyContractReadRepository.CreatePredicates(criteria, [101, 202]), predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));
    }

    [Fact]
    public void UserVinIsSentAsParameter()
    {
        const string maliciousValue = "VIN' OR 1=1 --";
        var criteria = CreateCriteria(vin: maliciousValue);
        var parameters = LegacyContractReadRepository.CreateParameters(criteria, [101, 202]);
        var sql = string.Join(" ", LegacyContractReadRepository.CreatePredicates(criteria, [101, 202]));

        Assert.DoesNotContain(maliciousValue, sql);
        Assert.Equal(maliciousValue, parameters.Get<string>("Vin"));
    }

    [Fact]
    public void LegacyRowMapsNullableColumnsAndLegacyDateTypes()
    {
        var modifiedAt = new DateTime(2026, 9, 13, 10, 30, 0, DateTimeKind.Unspecified);
        var row = new LegacyContractSummaryRow
        {
            ContractNumber = "CONTRACT-1",
            PersonId = 42,
            PersonName = "Sample Person",
            OperationTypeCode = "OP",
            OperationTypeName = "Operation",
            AddressId = null,
            FinancedAmount = 123.45m,
            OutstandingBalance = 67.89m,
            DisbursementDate = new DateTime(2026, 1, 2),
            FirstPaymentDate = null,
            LastPaymentDate = new DateTime(2026, 3, 4),
            StatusCode = 1,
            StatusName = "Active",
            ModifiedBy = null,
            ModifiedAt = modifiedAt,
        };

        var result = LegacyContractReadRepository.MapRow(row);

        Assert.Equal("CONTRACT-1", result.ContractNumber);
        Assert.Equal(42, result.PersonId);
        Assert.Null(result.AddressId);
        Assert.Equal(123.45m, result.FinancedAmount);
        Assert.Equal(67.89m, result.OutstandingBalance);
        Assert.Equal(new DateOnly(2026, 1, 2), result.DisbursementDate);
        Assert.Null(result.FirstPaymentDate);
        Assert.Equal(new DateOnly(2026, 3, 4), result.LastPaymentDate);
        Assert.Equal(1, result.StatusCode);
        Assert.Null(result.ModifiedBy);
        Assert.Equal(new DateTimeOffset(modifiedAt, TimeSpan.Zero), result.ModifiedAt);
    }
    [Fact]
    public void LegacyRowDoesNotConvertNullRequiredColumnsToDefaults()
    {
        var row = new LegacyContractSummaryRow
        {
            PersonId = null,
            ContractNumber = "CONTRACT-1",
            PersonName = "Sample Person",
            OperationTypeCode = "OP",
            OperationTypeName = "Operation",
            FinancedAmount = 1m,
            OutstandingBalance = 0m,
            StatusCode = 1,
            StatusName = "Active",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => LegacyContractReadRepository.MapRow(row));

        Assert.Contains(nameof(LegacyContractSummaryRow.PersonId), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RfcUsesAnsiStringAndLengthMatchingLegacyColumn()
    {
        var parameters = LegacyContractReadRepository.CreateParameters(CreateCriteria(rfc: "RFC-123"), [101, 202]);

        var metadata = GetParameterMetadata(parameters, "Rfc");

        Assert.Equal(DbType.AnsiString, metadata.DbType);
        Assert.Equal(13, metadata.Size);
    }

    [Fact]
    public void PersonNamePrefixUsesAnsiStringAndLengthMatchingLegacyColumn()
    {
        var parameters = LegacyContractReadRepository.CreateParameters(CreateCriteria(personName: "Sample"), [101, 202]);

        var metadata = GetParameterMetadata(parameters, "PersonNamePrefix");

        Assert.Equal(DbType.AnsiString, metadata.DbType);
        Assert.Equal(200, metadata.Size);
    }

    [Fact]
    public void CountSqlUsesOnlyContractAndPersonTablesAndParameterizedPredicates()
    {
        var sql = LegacyContractReadRepository.CreateCountSql(CreateCriteria(rfc: "RFC-123", personName: "Sample"), [101, 202]);

        Assert.Contains("SELECT COUNT_BIG(1)", sql);
        Assert.Contains("FROM dbo.KCONTRATO AS C", sql);
        Assert.Contains("INNER JOIN dbo.CPERSONA AS P", sql);
        Assert.Contains("P.PNA_CL_RFC = @Rfc", sql);
        Assert.Contains("P.PNA_DS_NOMBRE LIKE @PersonNamePrefix", sql);
        Assert.DoesNotContain("KTOPERACION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CPARAMETRO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CUSUARIO", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ROW_NUMBER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void RfcAndPersonNamePredicatesDoNotApplyFunctionsToLegacyColumns()
    {
        var criteria = CreateCriteria(rfc: "RFC-123", personName: "Sample");
        var predicates = LegacyContractReadRepository.CreatePredicates(criteria, [101, 202]);

        Assert.Contains("P.PNA_CL_RFC = @Rfc", predicates);
        Assert.Contains("P.PNA_DS_NOMBRE LIKE @PersonNamePrefix", predicates);
        Assert.DoesNotContain(predicates, predicate => predicate.Contains("CAST", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(predicates, predicate => predicate.Contains("CONVERT", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(predicates, predicate => predicate.Contains("UPPER", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(predicates, predicate => predicate.Contains("LOWER", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetByNumberSqlUsesTheSameParameterizedCompanyScope()
    {
        var sql = LegacyContractReadRepository.CreateGetByNumberSql();

        Assert.Contains("C.EMP_FL_CVE IN @AllowedCompanyIds", sql);
        Assert.Contains("C.CTO_FL_CVE = @ContractNumber", sql);
        Assert.DoesNotContain("101", sql);
        Assert.DoesNotContain("202", sql);
    }
    [Fact]
    public void CompanyScopeIsParameterizedAndAppliedToSearchAndCountSql()
    {
        var companyIds = new[] { 101, 202 };
        var criteria = CreateCriteria(contractNumber: "CONTRACT-1");
        var parameters = LegacyContractReadRepository.CreateParameters(criteria, companyIds);
        var predicates = LegacyContractReadRepository.CreatePredicates(criteria, companyIds);
        var countSql = LegacyContractReadRepository.CreateCountSql(criteria, companyIds);

        Assert.Contains("C.EMP_FL_CVE IN @AllowedCompanyIds", predicates);
        Assert.Contains("C.EMP_FL_CVE IN @AllowedCompanyIds", countSql);
        Assert.DoesNotContain("101", string.Join(" ", predicates));
        Assert.DoesNotContain("202", string.Join(" ", predicates));
        Assert.Equal(companyIds, parameters.Get<int[]>("AllowedCompanyIds"));
    }

    [Fact]
    public async Task MissingExecutionScopeFailsClosedBeforeOpeningLegacyConnection()
    {
        var repository = new LegacyContractReadRepository(
            Options.Create(new LegacySqlOptions { ReadConnectionString = "Server=unreachable;Database=NeverOpen;" }),
            new FixedExecutionTenantContext(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.SearchAsync(CreateCriteria(contractNumber: "CONTRACT-1"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.GetByNumberAsync("CONTRACT-1", TestContext.Current.CancellationToken));
    }
    private static object? GetMemberValue(Type type, object instance, string name)
    {
        var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property?.GetValue(instance)
            ?? type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance);
    }
    private static (DbType? DbType, int? Size) GetParameterMetadata(DynamicParameters parameters, string name)
    {
        var field = typeof(DynamicParameters).GetField("parameters", BindingFlags.Instance | BindingFlags.NonPublic);
        var values = (IDictionary)field!.GetValue(parameters)!;
        var info = values[name]!;
        var infoType = info.GetType();
        var dbType = (DbType?)GetMemberValue(infoType, info, "DbType");
        var size = (int?)GetMemberValue(infoType, info, "Size");
        return (dbType, size);
    }
    private static ContractSearchCriteria CreateCriteria(string? contractNumber = null, string? vin = null, string? rfc = null, string? personName = null) => new(
        contractNumber, null, rfc, personName, vin, null, null);

    private sealed class FixedExecutionTenantContext(ExecutionTenant? executionTenant) : IExecutionTenantContext
    {
        public Task<ExecutionTenant?> GetAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(executionTenant);
        }
    }
}
