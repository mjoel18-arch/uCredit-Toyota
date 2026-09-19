using Microsoft.Extensions.Configuration;

namespace UCredit.IdentityAdmin;

public static class BootstrapConfiguration
{
    public static BootstrapInput Load(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var unknownArguments = args
            .Where(argument => !string.Equals(argument, "--apply", StringComparison.Ordinal))
            .ToArray();

        return new BootstrapInput(
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
            args.Count(argument => string.Equals(argument, "--apply", StringComparison.Ordinal)) == 1,
            Environment.GetEnvironmentVariable("IdentitySql__ConnectionString"),
            configuration["UCREDIT_BOOTSTRAP_TENANT_CODE"],
            configuration["UCREDIT_BOOTSTRAP_TENANT_NAME"],
            configuration["UCREDIT_BOOTSTRAP_ADMIN_EMAIL"],
            configuration["UCREDIT_BOOTSTRAP_ADMIN_PASSWORD"],
            unknownArguments,
            configuration["UCREDIT_BOOTSTRAP_COMPANY_IDS"],
            configuration["UCREDIT_BOOTSTRAP_LEGACY_USER_CODE"]);
    }
}
