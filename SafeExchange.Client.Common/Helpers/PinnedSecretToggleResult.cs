/// <summary>
/// PinnedSecretToggleResult
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    public sealed class PinnedSecretToggleResult
    {
        public bool Succeeded { get; init; }

        public string Status { get; init; } = string.Empty;

        public string? Error { get; init; }
    }
}
