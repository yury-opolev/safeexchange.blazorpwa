/// <summary>
/// StateContainer
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Web.Components.Model;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class StateContainer
    {
        public string CurrentPageHeader { get; private set; }

        public NotificationData Notification { get; private set; }

        public event Action OnChange;

        private bool isInProgress = false;

        public bool IsInProgress
        {
            get => this.isInProgress;

            set
            {
                this.isInProgress = value;
                this.NotifyStateChanged();
            }
        }

        public bool IsFetchingAccessRequests { get; set; }

        public List<AccessRequest> IncomingAccessRequests { get; set; }

        public List<AccessRequest> OutgoingAccessRequests { get; set; }

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

        public async Task<ResponseStatus> TryFetchAccessRequestsAsync(ApiClient apiClient, string currentUserUpn)
        {
            this.IsFetchingAccessRequests = true;
            this.IncomingAccessRequests = new List<AccessRequest>();
            this.OutgoingAccessRequests = new List<AccessRequest>();

            try
            {
                var requests = await apiClient.GetAccessRequestsAsync();
                if (requests.Status == "ok")
                {
                    var accessRequests = requests.Result.Select(ar => new AccessRequest(ar)).ToList();
                    foreach (var accessRequest in accessRequests)
                    {
                        if (accessRequest.Requestor.Equals(currentUserUpn))
                        {
                            this.OutgoingAccessRequests.Add(accessRequest);
                        }
                        else
                        {
                            this.IncomingAccessRequests.Add(accessRequest);
                        }
                    }
                }
                else
                {
                    // no-op
                }

                return requests.ToResponseStatus();
            }
            finally
            {
                this.IsFetchingAccessRequests = false;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
