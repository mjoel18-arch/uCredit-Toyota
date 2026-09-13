namespace UCredit.Modules.Branding;

public sealed class InMemoryBrandThemeProvider : IBrandThemeProvider
{
    private static readonly Dictionary<string, BrandTheme> Themes =
        new Dictionary<string, BrandTheme>(StringComparer.OrdinalIgnoreCase)
        {
            ["UCREDIT"] = BrandTheme.Default,
            ["DEMO"] = BrandTheme.Default with
            {
                TenantCode = "DEMO",
                ApplicationName = "uCredit Demo",
                PrimaryColor = "#7F56D9",
                SecondaryColor = "#53389E",
                AccentColor = "#F79009",
                NavigationColor = "#2D1B69"
            }
        };

    public ValueTask<BrandTheme> GetCurrentAsync(
        string? tenantCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(tenantCode) || !Themes.TryGetValue(tenantCode, out var theme))
        {
            theme = BrandTheme.Default;
        }

        return ValueTask.FromResult(theme);
    }
}

