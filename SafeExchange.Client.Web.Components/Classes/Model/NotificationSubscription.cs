/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using System;

    public class NotificationSubscription
    {
        public long NotificationSubscriptionId { get; set; }

        public string UserId { get; set; }

        public string Url { get; set; }

        public string P256dh { get; set; }

        public string Auth { get; set; }

        public override string ToString()
        {
            return $"Id:{this.NotificationSubscriptionId}, UserId:{this.UserId}, Endpoint:{this.Url.Substring(0, 30)}...";
        }
    }
}
