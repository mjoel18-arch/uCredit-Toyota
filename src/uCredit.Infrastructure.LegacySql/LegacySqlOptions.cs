namespace UCredit.Infrastructure.LegacySql;

public sealed class LegacySqlOptions
{
    public const string SectionName = "LegacySql";
    public string ReadConnectionString { get; set; } = string.Empty;
    public string WriteConnectionString { get; set; } = string.Empty;
    public bool AllowLegacyWriteTests { get; set; }
    public string LegacyWriteTestDatabase { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
}
