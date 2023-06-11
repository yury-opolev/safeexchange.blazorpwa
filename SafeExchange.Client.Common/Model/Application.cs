/// <summary>
/// AccessRequestType
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class Application
    {
        public Application()
        { }

        public Application(ApplicationOverviewOutput applicationOverview)
            : this(applicationOverview.DisplayName, applicationOverview.Enabled)
        { }

        public Application(string displayName, bool enabled)
        {
            this.DisplayName = displayName ?? throw new ArgumentNullException();
            this.Enabled = enabled;
        }

        public string DisplayName { get; set; }

        public bool Enabled { get; set; }

        public string AadTenantId { get; set; }

        public string AadClientId { get; set; }

        public string ContactEmail { get; set; }

    }
}
