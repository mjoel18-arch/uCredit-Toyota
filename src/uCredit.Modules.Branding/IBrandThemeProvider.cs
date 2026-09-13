namespace UCredit.Modules.Branding;

public interface IBrandThemeProvider
{
    ValueTask<BrandTheme> GetCurrentAsync(
        string? tenantCode,
        CancellationToken cancellationToken = default);
}

