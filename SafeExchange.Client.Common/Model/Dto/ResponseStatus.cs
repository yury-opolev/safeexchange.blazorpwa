/// <summary>
/// ResponseStatus
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ResponseStatus
    {
        public string Status { get; set; } = string.Empty;

        public string? Error { get; set; }
    }
}
