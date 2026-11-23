/// <summary>
/// ApiClient.Settings — partial of ApiClient with the caller's own settings
/// endpoints (/me/settings).
/// </summary>

namespace SafeExchange.Client.Common
{
    using SafeExchange.Client.Common.Model;
    using System.Threading.Tasks;

    public partial class ApiClient
    {
        /// <summary>GET /v3/me/settings — the caller's own settings.</summary>
        public Task<BaseResponseObject<UserSettings>> GetMySettingsAsync()
            => GetAsync<UserSettings>($"{ApiVersion}/me/settings");

        /// <summary>
        /// PATCH /v3/me/settings — set the caller's own MCP opt-in. Enabling while an
        /// administrator has blocked MCP returns status "forbidden" with subStatus
        /// "mcp_blocked_by_admin".
        /// </summary>
        public Task<BaseResponseObject<UserSettings>> UpdateMySettingsAsync(bool mcpAllowed)
            => PatchAsync<UserSettings>($"{ApiVersion}/me/settings",
                new UserSettingsUpdateRequest { McpAllowed = mcpAllowed });
    }
}
