namespace UCredit.Infrastructure.LegacySql;

public sealed class LegacySqlOptions
{
    public const string SectionName = "LegacySql";

    public string ReadConnectionString { get; init; } = string.Empty;

    public int CommandTimeoutSeconds { get; init; } = 30;
}

