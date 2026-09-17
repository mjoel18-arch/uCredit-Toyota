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
                ProductName = "uCredit Demo",
                CustomerName = "Demo",
                PrimaryColor = "#7F56D9",
                SecondaryColor = "#53389E",
                AccentColor = "#F79009",
                NavigationColor = "#2D1B69",
                BrowserTitle = "uCredit Demo"
            },
            ["TOYOTA"] = BrandTheme.Default with
            {
                TenantCode = "TOYOTA",
                ProductName = "uCredit-auto",
                CustomerName = "Toyota Financial Services",
                LogoUrl = "/branding/toyota/logo.png",
                PrimaryColor = "#EB0A1E",
                SecondaryColor = "#1D1D1F",
                AccentColor = "#EB0A1E",
                NavigationColor = "#1D1D1F",
                BrowserTitle = "uCredit-auto | Toyota Financial Services",
                FaviconUrl = null
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

