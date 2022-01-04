/// <summary>
/// ExpirationSettingsOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ExpirationSettingsOutput
    {
        public bool ExpireAfterRead { get; set; }

        public bool ScheduleExpiration { get; set; }

        public DateTime ExpireAt { get; set; }
    }
}
