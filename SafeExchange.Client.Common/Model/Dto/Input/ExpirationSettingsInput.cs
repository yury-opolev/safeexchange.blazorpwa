﻿/// <summary>
/// ExpirationSettingsInput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ExpirationSettingsInput
    {
        public bool ExpireAfterRead { get; set; }

        public bool ScheduleExpiration { get; set; }

        public DateTime ExpireAt { get; set; }
    }
}
