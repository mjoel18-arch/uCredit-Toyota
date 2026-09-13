using UCredit.Modules.Contracts.Contracts;

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
}

