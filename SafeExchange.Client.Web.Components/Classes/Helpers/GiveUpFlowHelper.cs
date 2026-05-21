/// <summary>
/// GiveUpFlowHelper — shared post-give-up handler. The Notification copy,
/// telemetry event, and navigation are identical across ListData / ViewData /
/// EditData, so the call sites just delegate to AfterGiveUpAsync.
/// </summary>

namespace SafeExchange.Client.Web.Components.Helpers
{
    using Microsoft.AspNetCore.Components;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Web.Components;
    using SafeExchange.Client.Web.Components.Model;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public static class GiveUpFlowHelper
    {
        public static async Task AfterGiveUpAsync(
            string objectName,
            GiveUpResultOutput result,
            StateContainer stateContainer,
            TelemetryService telemetry,
            NavigationManager navigationManager)
        {
            var safeObjectName = objectName ?? string.Empty;
            var wasOrphaned = result?.WasOrphaned == true;

            var message = wasOrphaned && result!.ExpireAt.HasValue
                ? $"You gave up access to '{safeObjectName}'. Nobody else has access; the secret is scheduled to be deleted on {result.ExpireAt.Value:yyyy-MM-dd HH:mm 'UTC'}."
                : $"You gave up access to '{safeObjectName}'.";

            stateContainer?.SetNextNotification(new NotificationData
            {
                Type = NotificationType.Success,
                Status = "ok",
                Message = message
            });

            if (telemetry != null)
            {
                await telemetry.TrackEventAsync("GiveUpAccess", new Dictionary<string, string>
                {
                    ["name"] = safeObjectName,
                    ["orphaned"] = wasOrphaned.ToString()
                });
            }

            navigationManager?.NavigateTo("listdata");
        }
    }
}
