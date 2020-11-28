/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA
{
    using SafeExchange.BlazorPWA.Model;
    using System;

    public class StateContainer
    {
        public string CurrentPageHeader { get; private set; }

        public NotificationData Notification { get; private set; }

        public event Action OnChange;

        public void SetNextNotification(NotificationData notification)
        {
            this.Notification = notification;
        }

        public NotificationData TakeNotification()
        {
            var result = this.Notification;
            this.Notification = null;
            return result;
        }

        public void SetCurrentPageHeader(string pageHeader)
        {
            CurrentPageHeader = pageHeader;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
