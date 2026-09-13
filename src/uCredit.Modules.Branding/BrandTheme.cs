namespace UCredit.Modules.Branding;

public sealed record BrandTheme(
    string TenantCode,
    string ApplicationName,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string NavigationColor,
    string SurfaceColor,
    string TextColor,
    string FontFamily,
    string ThemeMode)
{
    public static BrandTheme Default { get; } = new(
        TenantCode: "UCREDIT",
        ApplicationName: "uCredit",
        LogoUrl: null,
        PrimaryColor: "#155EEF",
        SecondaryColor: "#344054",
        AccentColor: "#12B76A",
        NavigationColor: "#101828",
        SurfaceColor: "#FFFFFF",
        TextColor: "#101828",
        FontFamily: "Inter, system-ui, sans-serif",
        ThemeMode: "light");
}

