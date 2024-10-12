/// <summary>
/// StateContainer
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
    using Microsoft.Extensions.Logging;
    using SafeExchange.Client.Common;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Web.Components.Classes;
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

        private object fetchingAccessRequestsLock = new();

        public event EventHandler<DataFetchedEventArgs> OnAccessRequestsFetched;

        public List<AccessRequest> IncomingAccessRequests { get; set; }

        public List<AccessRequest> OutgoingAccessRequests { get; set; }

        public bool IsFetchingApplications { get; set; }

        public List<Application> RegisteredApplications { get; set; }

        public bool IsFetchingGroups { get; set; }

        public List<Group> RegisteredGroups { get; set; }

        private ILogger logger;

        public StateContainer(ILogger<StateContainer> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

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

        public async Task TryFetchAccessRequestsAsync(ApiClient apiClient, string currentUserUpn)
        {
            if (this.IsFetchingAccessRequests)
            {
                this.logger.LogDebug($"{nameof(TryFetchAccessRequestsAsync)} returned - already fetching.");
                return;
            }

            lock (this.fetchingAccessRequestsLock)
            {
                if (this.IsFetchingAccessRequests)
                {
                    this.logger.LogDebug($"{nameof(TryFetchAccessRequestsAsync)} returned in the lock - already fetching.");
                    return;
                }

                this.IsFetchingAccessRequests = true;
            }

            this.logger.LogDebug($"{nameof(TryFetchAccessRequestsAsync)} started.");

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

                this.OnAccessRequestsFetched?.Invoke(this, new DataFetchedEventArgs() { ResponseStatus = requests.ToResponseStatus() });
                return;
            }
            catch (AccessTokenNotAvailableException exception)
            {
                exception.Redirect();
            }
            finally
            {
                this.IsFetchingAccessRequests = false;
                NotifyStateChanged();

                this.logger.LogDebug($"{nameof(TryFetchAccessRequestsAsync)} finished.");
            }
        }

        public async Task<ResponseStatus> TryFetchRegisteredApplications(ApiClient apiClient)
        {
            if (this.RegisteredApplications != null)
            {
                return new ResponseStatus() { Status = "ok" }; // already fetched
            }

            this.IsFetchingApplications = true;

            try
            {
                var applicationsResponse = await apiClient.GetRegisteredApplicationsAsync();
                if (applicationsResponse.Status == "ok")
                {
                    var applications = applicationsResponse.Result.Select(a => new Application(a)).ToList();
                    this.RegisteredApplications = applications ?? [];
                }
                else
                {
                    // no-op
                }

                return applicationsResponse.ToResponseStatus();
            }
            finally
            {
                this.IsFetchingApplications = false;
                NotifyStateChanged();
            }
        }

        public async Task<ResponseStatus> TryFetchRegisteredGroups(ApiClient apiClient)
        {
            if (this.RegisteredGroups != null)
            {
                return new ResponseStatus() { Status = "ok" }; // already fetched
            }

            this.IsFetchingGroups = true;

            try
            {
                var groupsResponse = await apiClient.GetRegisteredGroupsAsync();
                if (groupsResponse.Status == "ok")
                {
                    var groups = groupsResponse.Result.Select(g => new Group(g)).ToList();
                    this.RegisteredGroups = groups ?? [];
                }
                else
                {
                    // no-op
                }

                return groupsResponse.ToResponseStatus();
            }
            finally
            {
                this.IsFetchingGroups = false;
                NotifyStateChanged();
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
