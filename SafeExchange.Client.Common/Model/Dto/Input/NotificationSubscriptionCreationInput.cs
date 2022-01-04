/// <summary>
/// NotificationSubscriptionCreationInput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class NotificationSubscriptionCreationInput
    {
        public string Url { get; set; }

        public string P256dh { get; set; }

        public string Auth { get; set; }
    }
}
