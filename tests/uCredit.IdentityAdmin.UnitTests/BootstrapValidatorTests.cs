using Xunit;

namespace UCredit.IdentityAdmin.UnitTests;

public sealed class BootstrapValidatorTests
{
    [Fact]
    public void RejectsAnEnvironmentOtherThanDevelopment()
    {
        var result = BootstrapValidator.Validate(CreateInput(environmentName: "Production"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Development", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsExecutionWithoutApply()
    {
        var result = BootstrapValidator.Validate(CreateInput(apply: false));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("--apply", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsAnIdentityDatabaseThatIsNotDevelopment()
    {
        var result = BootstrapValidator.Validate(CreateInput(
            connectionString: "Server=(local);Database=uCreditIdentity_Qa;Trusted_Connection=True;"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("_Dev", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsAnIdentityConnectionWithoutADatabaseName()
    {
        var result = BootstrapValidator.Validate(CreateInput(
            connectionString: "Server=(local);Trusted_Connection=True;"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("database name", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsInvalidTenantCodeAndEmail()
    {
        var result = BootstrapValidator.Validate(CreateInput(
            tenantCode: "not valid!",
            adminEmail: "not-an-email"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("TENANT_CODE", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("ADMIN_EMAIL", StringComparison.Ordinal));
    }

    [Fact]
    public void RejectsMissingPassword()
    {
        var result = BootstrapValidator.Validate(CreateInput(adminPassword: string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("ADMIN_PASSWORD", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationAndOutputDoNotExposeSecrets()
    {
        const string password = "OnlyTest-Password-123!";
        const string connectionString = "Server=(local);Database=uCreditIdentity_Dev;Trusted_Connection=True;";
        var result = BootstrapValidator.Validate(CreateInput(
            connectionString: connectionString,
            adminPassword: password,
            adminEmail: "invalid email"));
        var output = BootstrapOutput.Format(new BootstrapAction(
            "ApplicationUser",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            null,
            true));

        Assert.DoesNotContain(password, string.Join("\n", result.Errors), StringComparison.Ordinal);
        Assert.DoesNotContain(connectionString, string.Join("\n", result.Errors), StringComparison.Ordinal);
        Assert.DoesNotContain(password, output, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", output, StringComparison.Ordinal);
        Assert.DoesNotContain("SecurityStamp", output, StringComparison.Ordinal);
        Assert.DoesNotContain("token", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", output, StringComparison.Ordinal);
    }

    private static BootstrapInput CreateInput(
        string environmentName = "Development",
        bool apply = true,
        string connectionString = "Server=(local);Database=uCreditIdentity_Dev;Trusted_Connection=True;",
        string tenantCode = "DEV",
        string tenantName = "Development tenant",
        string adminEmail = "admin@example.test",
        string adminPassword = "OnlyTest-Password-123!") =>
        new(environmentName, apply, connectionString, tenantCode, tenantName, adminEmail, adminPassword);
}


