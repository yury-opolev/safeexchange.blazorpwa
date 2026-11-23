/// <summary>
/// McpStatus — maps a user's MCP flags to the single status shown in the
/// AdminPanel. An administrator block always wins over the user's own choice.
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    public static class McpStatus
    {
        public const string Off = "Off";

        public const string On = "On";

        public const string Blocked = "Blocked";

        public static string Label(bool mcpAllowed, bool mcpBlockedByAdmin)
            => mcpBlockedByAdmin ? Blocked : mcpAllowed ? On : Off;

        /// <summary>AdminPanel <c>admin-pill</c> modifier class for the status.</summary>
        public static string AdminPillClass(bool mcpAllowed, bool mcpBlockedByAdmin)
            => mcpBlockedByAdmin ? "attn" : mcpAllowed ? "ok" : "off";
    }
}
