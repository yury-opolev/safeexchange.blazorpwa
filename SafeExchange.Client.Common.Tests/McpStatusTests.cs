/// <summary>
/// McpStatusTests — the AdminPanel MCP column: Off / On / Blocked, with an
/// administrator block winning over the user's own choice.
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common.Helpers;

    [TestFixture]
    public class McpStatusTests
    {
        [TestCase(false, false, "Off", "off")]
        [TestCase(true, false, "On", "ok")]
        [TestCase(false, true, "Blocked", "attn")]
        [TestCase(true, true, "Blocked", "attn")]
        public void LabelAndPillClass_ReflectFlags_BlockedWins(bool mcpAllowed, bool mcpBlockedByAdmin, string expectedLabel, string expectedPillClass)
        {
            Assert.That(McpStatus.Label(mcpAllowed, mcpBlockedByAdmin), Is.EqualTo(expectedLabel));
            Assert.That(McpStatus.AdminPillClass(mcpAllowed, mcpBlockedByAdmin), Is.EqualTo(expectedPillClass));
        }
    }
}
