/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using SafeExchange.Client.Web.Components.Model;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class StateContainer
    {
        public string CurrentPageHeader { get; private set; }

        public NotificationData Notification { get; private set; }

        public event Action OnChange;

        public bool IsFetchingAccessRequests { get; set; }

        public ApiAccessRequestsListReply CachedAccessRequests { get; set; }

        public int IncomingAccessRequestsCount => this.CachedAccessRequests?.AccessRequests.Where(ar => (ar.RequestType == AccessRequestType.Incoming)).Count() ?? 0;

        public int OutgoingAccessRequestsCount => this.CachedAccessRequests?.AccessRequests.Where(ar => (ar.RequestType == AccessRequestType.Outgoing)).Count() ?? 0;

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

        public async Task<ApiAccessRequestsListReply> TryFetchAccessRequestsAsync(ApiClient apiClient)
        {
            this.IsFetchingAccessRequests = true;
            try
            {
                this.CachedAccessRequests = null;
                this.CachedAccessRequests = await apiClient.GetAccessRequestsAsync();
            }
            finally
            {
                this.IsFetchingAccessRequests = false;
                NotifyStateChanged();
            }

            return this.CachedAccessRequests;
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
