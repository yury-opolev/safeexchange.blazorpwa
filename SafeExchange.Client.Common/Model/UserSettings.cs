/// <summary>
/// UserSettings — the caller's own settings as returned by GET/PATCH /me/settings.
/// Mirrors the backend response; only McpAllowed is writable by the user.
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    public class UserSettings
    {
        public bool McpAllowed { get; set; }

        public bool McpBlockedByAdmin { get; set; }

        public bool McpEffective { get; set; }
    }

    public class UserSettingsUpdateRequest
    {
        public bool McpAllowed { get; set; }
    }
}
