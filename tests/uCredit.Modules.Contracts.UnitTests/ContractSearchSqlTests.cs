using UCredit.Infrastructure.LegacySql.Contracts;
using UCredit.Modules.Contracts.Contracts;

namespace UCredit.Modules.Contracts.UnitTests;

public sealed class ContractSearchSqlTests
{
    [Fact]
    public void VinUsesExactMatchWithConfirmedCharacteristic()
    {
        var criteria = CreateCriteria(vin: "  VIN-123  ");
        var parameters = LegacyContractReadRepository.CreateParameters(criteria);
        var vinPredicate = LegacyContractReadRepository.CreatePredicates(criteria).Single(predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));

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
        var vinPredicate = LegacyContractReadRepository.CreatePredicates(CreateCriteria(vin: "VIN-123"))
            .Single(predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));

        Assert.StartsWith("EXISTS", vinPredicate, StringComparison.Ordinal);
        Assert.DoesNotContain("JOIN dbo.KPRODUCTO_FACTURA", vinPredicate, StringComparison.Ordinal);
    }

    [Fact]
    public void VinParameterIsTrimmedAndNotConcatenatedIntoSql()
    {
        var parameters = LegacyContractReadRepository.CreateParameters(CreateCriteria(vin: "  VIN-123  "));
        var vinPredicate = LegacyContractReadRepository.CreatePredicates(CreateCriteria(vin: "VIN-123"))
            .Single(predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));

        Assert.Equal("VIN-123", parameters.Get<string>("Vin"));
        Assert.DoesNotContain("VIN-123", vinPredicate);
    }

    [Fact]
    public void EmptyVinProducesNoVinParameterOrPredicate()
    {
        var criteria = CreateCriteria(vin: "   ");

        Assert.DoesNotContain("Vin", LegacyContractReadRepository.CreateParameters(criteria).ParameterNames);
        Assert.DoesNotContain(LegacyContractReadRepository.CreatePredicates(criteria), predicate => predicate.Contains("KPRODUCTO_FACTURA", StringComparison.Ordinal));
    }

    [Fact]
    public void UserVinIsSentAsParameter()
    {
        const string maliciousValue = "VIN' OR 1=1 --";
        var criteria = CreateCriteria(vin: maliciousValue);
        var parameters = LegacyContractReadRepository.CreateParameters(criteria);
        var sql = string.Join(" ", LegacyContractReadRepository.CreatePredicates(criteria));

        Assert.DoesNotContain(maliciousValue, sql);
        Assert.Equal(maliciousValue, parameters.Get<string>("Vin"));
    }

    private static ContractSearchCriteria CreateCriteria(string? vin = null) => new(
        null, null, null, null, vin, null, null, null);
}
