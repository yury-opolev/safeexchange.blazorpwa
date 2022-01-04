/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.Extensions.Configuration;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Threading.Tasks;

    public class NotificationsSubscriber
    {
        private readonly ApiClient apiClient;

        private readonly PushNotifications pushNotifications;

        private readonly string applicationServerPublicKey;

        private bool initialized;
        private bool isWebPushAvailable;

        public NotificationsSubscriber(ApiClient apiClient, PushNotifications pushNotifications, IConfiguration configuration)
        {
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            this.pushNotifications = pushNotifications ?? throw new ArgumentNullException(nameof(pushNotifications));
            var appServerPublicKey = configuration?.GetSection("WebPushApi")?.GetValue<string>("AppServerPublicKey");
            this.applicationServerPublicKey = appServerPublicKey ?? throw new ArgumentException($"Application server public key is not configured.");
        }

        public async ValueTask<bool> IsAvailable()
        {
            if (!initialized)
            {
                this.isWebPushAvailable = await pushNotifications.IsWebPushAvailable();
                this.initialized = true;
            }

            return this.isWebPushAvailable;
        }

        public async Task<bool> IsSubscribed()
        {
            if (!await this.IsAvailable())
            {
                return false;
            }

            var subscription = await pushNotifications.GetSubscription();
            return subscription != default(NotificationSubscription);
        }

        public async Task<ResponseStatus> Subscribe()
        {
            if (!await this.IsAvailable())
            {
                return new ResponseStatus()
                {
                    Status = "no_content",
                    Error = "Your browser does not support push notifications."
                };
            }

            var subscription = await pushNotifications.RequestSubscription(applicationServerPublicKey);
            if (subscription == default(NotificationSubscription))
            {
                return new ResponseStatus()
                {
                    Status = "no_content",
                    Error = "Could not request browser push notifications."
                };
            }

            var response = await apiClient.RegisterWebPushSubscriptionAsync(subscription.ToCreationDto());
            if (!response.Status.Equals("ok"))
            {
                await pushNotifications.DeleteSubscription();
            }

            return response.ToResponseStatus();
        }

        public async Task<ResponseStatus> Unsubscribe()
        {
            if (!await this.IsAvailable())
            {
                return new ResponseStatus()
                {
                    Status = "no_content",
                    Error = "Your browser does not support push notifications."
                };
            }

            var subscription = await pushNotifications.GetSubscription();
            if (subscription == default(NotificationSubscription))
            {
                return new ResponseStatus()
                {
                    Status = "no_content",
                    Error = "Not found browser push notifications subscription."
                };
            }
            
            await pushNotifications.DeleteSubscription();

            var response = await apiClient.UnregisterWebPushSubscriptionAsync(subscription.ToDeletionDto());
            return response.ToResponseStatus();
        }
    }
}
