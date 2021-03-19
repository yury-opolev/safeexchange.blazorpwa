/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using System;

    public class NotificationSubscription
    {
        public string Url { get; set; }

        public string P256dh { get; set; }

        public string Auth { get; set; }

        public override string ToString()
        {
            return $"Endpoint:{this.Url.Substring(0, 30)}..., , P256dh Length:{this.P256dh?.Length ?? 0}, Auth Length:{this.Auth?.Length ?? 0}";
        }
    }
}
