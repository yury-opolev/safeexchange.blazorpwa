﻿/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.Extensions.Configuration;
    using SafeExchange.Client.Web.Components.Model;
    using System;
    using System.Threading.Tasks;

    public class NotificationsSubscriber
    {
        private readonly ApiClient apiClient;

        private readonly PushNotifications pushNotifications;

        private readonly string applicationServerPublicKey;

        public NotificationsSubscriber(ApiClient apiClient, PushNotifications pushNotifications, IConfiguration configuration)
        {
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.pushNotifications = pushNotifications ?? throw new ArgumentNullException(nameof(pushNotifications));
            var appServerPublicKey = configuration?.GetSection("WebPushApi")?.GetValue<string>("AppServerPublicKey");
            this.applicationServerPublicKey = appServerPublicKey ?? throw new ArgumentException($"Application server public key is not configured.");
        }

        public async Task<bool> IsSubscribed()
        {
            var subscription = await pushNotifications.GetSubscription();
            return subscription != default(NotificationSubscription);
        }

        public async Task<NotificationSubscriptionOperationResult> Subscribe()
        { 
            var subscription = await pushNotifications.RequestSubscription(applicationServerPublicKey);
            if (subscription == default(NotificationSubscription))
            {
                return new NotificationSubscriptionOperationResult()
                {
                    Status = "no_content",
                    Error = "Could not request browser push notifications."
                };
            }

            var response = await apiClient.Subscribe(subscription);
            if (!response.Status.Equals("ok"))
            {
                await pushNotifications.DeleteSubscription();
            }

            return new NotificationSubscriptionOperationResult()
            {
                Status = response.Status,
                Error = response.Error
            };
        }

        public async Task<NotificationSubscriptionOperationResult> Unsubscribe()
        {
            var subscription = await pushNotifications.GetSubscription();
            if (subscription == default(NotificationSubscription))
            {
                return new NotificationSubscriptionOperationResult()
                {
                    Status = "no_content",
                    Error = "Not found browser push notifications subscription."
                };
            }
            
            await pushNotifications.DeleteSubscription();
            var response = await apiClient.Unsubscribe(subscription);
            return new NotificationSubscriptionOperationResult()
            {
                Status = response.Status,
                Error = response.Error
            };
        }
    }
}
