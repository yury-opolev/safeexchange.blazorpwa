/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Classes.Helpers
{
    using Microsoft.JSInterop;
    using SafeExchange.Client.Web.Components.Model;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class LocalIdbStore : IAsyncDisposable
    {
        private const string StoreName = "accessRequests";

        private readonly Lazy<Task<IJSObjectReference>> helperModuleTask;
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public LocalIdbStore(IJSRuntime jsRuntime)
        {
            helperModuleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import",
                "./_content/SafeExchange.Client.Web.Components/idb-min.js").AsTask());
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import",
                "./_content/SafeExchange.Client.Web.Components/localIdbStore.js").AsTask());
        }

        public async ValueTask<DateTime> GetAccessRequestsFetchTimeUtc(string userId)
        {
            var module = await moduleTask.Value;
            var currentBundle = await module.InvokeAsync<AccessRequestsBundle>("get", StoreName, userId);
            return currentBundle?.LastUpdated ?? DateTime.MinValue;
        }

        public async ValueTask<AccessRequestsBundle> GetAccessRequests(string userId)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<AccessRequestsBundle>("get", StoreName, userId);
        }

        public async ValueTask PutAccessRequests(AccessRequestsBundle latestBundle)
        {
            if (latestBundle.LastUpdated == DateTime.MinValue)
            {
                latestBundle.LastUpdated = DateTime.UtcNow;
            }

            var module = await moduleTask.Value;
            await module.InvokeAsync<NotificationSubscription>("put", StoreName, latestBundle.UserId, latestBundle);
        }

        public async ValueTask<bool> ClearAccessRequests(string userId)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<bool>("delete", StoreName, userId);
        }

        public async ValueTask<List<AccessRequestsBundle>> GetAllAccessRequests()
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<List<AccessRequestsBundle>>("getAll", StoreName);
        }

        public async ValueTask<bool> ClearAllAccessRequests()
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<bool>("clear", StoreName);
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }

            if (helperModuleTask.IsValueCreated)
            {
                var helperModule = await helperModuleTask.Value;
                await helperModule.DisposeAsync();
            }
        }
    }
}
