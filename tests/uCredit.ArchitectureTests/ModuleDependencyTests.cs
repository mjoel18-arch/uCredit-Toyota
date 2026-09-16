using UCredit.Application.Execution;
using UCredit.Infrastructure.Identity;
using UCredit.Infrastructure.LegacySql.Contracts;
using UCredit.Modules.Contracts.Contracts;
using UCredit.Modules.Branding;

namespace UCredit.ArchitectureTests;

public sealed class ModuleDependencyTests
{
    [Fact]
    public void ContractsModuleDoesNotReferenceLegacyInfrastructure()
    {
        var references = typeof(ContractSummary).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.DoesNotContain("uCredit.Infrastructure.LegacySql", references);
    }

    [Fact]
    public void ContractsModuleDoesNotReferenceApiOrWeb()
    {
        var references = typeof(ContractSummary).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name is "uCredit.Api" or "uCredit.Web");
    }

    [Fact]
    public void BrandingModuleDoesNotReferenceContractsOrLegacyInfrastructure()
    {
        var references = typeof(BrandTheme).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name is "uCredit.Modules.Contracts" or "uCredit.Infrastructure.LegacySql");
    }

    [Fact]
    public void FunctionalModulesDoNotReferenceLegacyWebTypes()
    {
        var functionalAssemblies = new[]
        {
            typeof(ContractSummary).Assembly,
            typeof(BrandTheme).Assembly,
        };

        foreach (var assembly in functionalAssemblies)
        {
            var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name);
            var typeNames = assembly.GetTypes().Select(type => type.FullName ?? type.Name);

            Assert.DoesNotContain("System.Web", references);
            Assert.DoesNotContain("System.Data", references);
            Assert.DoesNotContain(typeNames, typeName => typeName.Contains("System.Web", StringComparison.Ordinal));
            Assert.DoesNotContain(typeNames, typeName => typeName.Contains("DataSet", StringComparison.Ordinal));
            Assert.DoesNotContain(typeNames, typeName => typeName.Contains("DataTable", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void LegacySqlCanImplementContractsInterfaces()
    {
        Assert.True(typeof(IContractReadRepository).IsAssignableFrom(typeof(LegacyContractReadRepository)));
    }

    [Fact]
    public void IdentityInfrastructureDoesNotReferenceLegacySql()
    {
        var references = typeof(IdentityDbContext).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain("uCredit.Infrastructure.LegacySql", references.Select(reference => reference.Name));
    }

    [Fact]
    public void LegacySqlDoesNotReferenceIdentityInfrastructure()
    {
        var references = typeof(LegacyContractReadRepository).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain("uCredit.Infrastructure.Identity", references.Select(reference => reference.Name));
    }

    [Fact]
    public void ExecutionContextAbstractionDoesNotReferenceInfrastructure()
    {
        var references = typeof(IExecutionTenantContext).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(
            references,
            reference => reference.Name is "uCredit.Infrastructure.Identity" or "uCredit.Infrastructure.LegacySql");
    }
}
