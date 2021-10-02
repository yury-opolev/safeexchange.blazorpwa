/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.JSInterop;
    using System;
    using System.Threading.Tasks;

    public class PushNotifications : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public PushNotifications(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
               "import", "./_content/SafeExchange.Client.Web.Components/pushNotifications.js").AsTask());
        }

        public async ValueTask<bool> IsWebPushAvailable()
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<bool>("isPushManagerAvailable");
        }

        public async ValueTask<NotificationSubscription> RequestSubscription(string applicationServerPublicKey)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<NotificationSubscription>("requestSubscription", applicationServerPublicKey);
        }

        public async ValueTask<NotificationSubscription> GetSubscription()
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<NotificationSubscription>("getSubscription");
        }

        public async ValueTask<bool> DeleteSubscription()
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<bool>("deleteSubscription");
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}
