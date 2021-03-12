/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class NotificationData
    {
        public NotificationType Type { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }

        public string CopyableUri { get; set; }

        public string GetAlertClass()
        {
            switch (this.Type)
            {
                case NotificationType.Primary:
                    return "alert-primary";

                case NotificationType.Secondary:
                    return "alert-secondary";

                case NotificationType.Success:
                    return "alert-success";

                case NotificationType.Warning:
                    return "alert-warning";

                case NotificationType.Danger:
                    return "alert-danger";

                default:
                    return null;
            }
        }
    }
}
