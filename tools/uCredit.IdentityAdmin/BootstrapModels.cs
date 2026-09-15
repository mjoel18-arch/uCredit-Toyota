using System.Net.Mail;
using Microsoft.Data.SqlClient;

namespace UCredit.IdentityAdmin;

public sealed class BootstrapInput
{
    public BootstrapInput(
        string? environmentName,
        bool apply,
        string? identityConnectionString,
        string? tenantCode,
        string? tenantName,
        string? adminEmail,
        string? adminPassword,
        IReadOnlyList<string>? unknownArguments = null)
    {
        EnvironmentName = environmentName;
        Apply = apply;
        IdentityConnectionString = identityConnectionString;
        TenantCode = tenantCode;
        TenantName = tenantName;
        AdminEmail = adminEmail;
        AdminPassword = adminPassword;
        UnknownArguments = unknownArguments ?? [];
    }

    public string? EnvironmentName { get; }
    public bool Apply { get; }
    public string? IdentityConnectionString { get; }
    public string? TenantCode { get; }
    public string? TenantName { get; }
    public string? AdminEmail { get; }
    public string? AdminPassword { get; }
    public IReadOnlyList<string> UnknownArguments { get; }
}

public sealed class BootstrapOptions
{
    public required string IdentityConnectionString { get; init; }
    public required string TenantCode { get; init; }
    public required string TenantName { get; init; }
    public required string AdminEmail { get; init; }
    public required string AdminPassword { get; init; }
    public bool Apply { get; init; }
}

public sealed class BootstrapValidationResult
{
    public BootstrapValidationResult(BootstrapOptions? options, IReadOnlyList<string> errors)
    {
        Options = options;
        Errors = errors;
    }

    public BootstrapOptions? Options { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool IsValid => Options is not null && Errors.Count == 0;
}

public static class BootstrapValidator
{
    public static BootstrapValidationResult Validate(BootstrapInput input)
    {
        var errors = new List<string>();

        if (input.UnknownArguments.Count > 0)
            errors.Add("Only the explicit --apply argument is supported.");

        if (!string.Equals(input.EnvironmentName, "Development", StringComparison.Ordinal))
            errors.Add("DOTNET_ENVIRONMENT must be Development.");

        if (!input.Apply)
            errors.Add("The --apply argument is required.");

        var connectionString = input.IdentityConnectionString?.Trim();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("IdentitySql__ConnectionString is required.");
        }
        else
        {
            try
            {
                var sqlConnectionString = new SqlConnectionStringBuilder(connectionString);
                var databaseName = sqlConnectionString.InitialCatalog?.Trim();
                if (string.IsNullOrWhiteSpace(databaseName))
                    errors.Add("The Identity database name is required.");
                else if (!databaseName.EndsWith("_Dev", StringComparison.OrdinalIgnoreCase))
                    errors.Add("The Identity database name must end with _Dev.");
            }
            catch (ArgumentException)
            {
                errors.Add("IdentitySql__ConnectionString is invalid.");
            }
        }

        var tenantCode = input.TenantCode?.Trim().ToUpperInvariant();
        if (tenantCode is null || tenantCode.Length is < 1 or > 64 ||
            !tenantCode.All(character => char.IsLetterOrDigit(character) || character is '_' or '-'))
        {
            errors.Add("UCREDIT_BOOTSTRAP_TENANT_CODE is invalid.");
        }

        var tenantName = input.TenantName?.Trim();
        if (string.IsNullOrWhiteSpace(tenantName) || tenantName.Length > 200)
            errors.Add("UCREDIT_BOOTSTRAP_TENANT_NAME is invalid.");

        var adminEmail = input.AdminEmail?.Trim();
        if (!IsValidEmail(adminEmail))
            errors.Add("UCREDIT_BOOTSTRAP_ADMIN_EMAIL is invalid.");

        if (string.IsNullOrEmpty(input.AdminPassword))
            errors.Add("UCREDIT_BOOTSTRAP_ADMIN_PASSWORD is required.");

        if (errors.Count > 0)
            return new BootstrapValidationResult(null, errors);

        return new BootstrapValidationResult(
            new BootstrapOptions
            {
                IdentityConnectionString = connectionString!,
                TenantCode = tenantCode!,
                TenantName = tenantName!,
                AdminEmail = adminEmail!,
                AdminPassword = input.AdminPassword!,
                Apply = true
            },
            errors);
    }

    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || email.Contains(' '))
            return false;

        return MailAddress.TryCreate(email, out var parsed) &&
            string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
    }
}
