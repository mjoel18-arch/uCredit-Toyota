using Microsoft.Extensions.Logging;

namespace UCredit.Api;

internal static partial class StartupConfigurationLogging
{
    [LoggerMessage(EventId = 4203, Level = LogLevel.Information, Message = "Legacy write configuration. IsDevelopment={IsDevelopment} WriteConnectionConfigured={WriteConnectionConfigured} AllowLegacyWriteTests={AllowLegacyWriteTests} ExpectedDatabaseConfigured={ExpectedDatabaseConfigured} PepCheckRequired={PepCheckRequired}")]
    internal static partial void Log(ILogger logger, bool isDevelopment, bool writeConnectionConfigured, bool allowLegacyWriteTests, bool expectedDatabaseConfigured, bool pepCheckRequired);
}
