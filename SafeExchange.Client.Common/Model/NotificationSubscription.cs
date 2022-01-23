/// <summary>
/// NotificationSubscription
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class NotificationSubscription
    {
        public NotificationSubscription()
        { }

        public string Url { get; set; }

        public string P256dh { get; set; }

        public string Auth { get; set; }

        public NotificationSubscriptionCreationInput ToCreationDto() => new NotificationSubscriptionCreationInput()
        {
            Auth = this.Auth,
            P256dh = this.P256dh,
            Url = this.Url
        };

        public NotificationSubscriptionDeletionInput ToDeletionDto() => new NotificationSubscriptionDeletionInput()
        {
            Url = this.Url
        };
    }
}
