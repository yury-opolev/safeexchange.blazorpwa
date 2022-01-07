﻿/// <summary>
/// ExpirationSettingsOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ExpirationSettingsOutput
    {
        public bool ScheduleExpiration { get; set; }

        public DateTime ExpireAt { get; set; }

        public bool ExpireOnIdleTime { get; set; }

        public TimeSpan IdleTimeToExpire { get; set; }
    }
}
