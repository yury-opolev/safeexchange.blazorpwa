/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = string.Empty;
}
