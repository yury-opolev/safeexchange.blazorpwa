/// <summary>
/// Group
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class Group
    {
        public Group()
        { }

        public Group(GroupOverviewOutput groupOverview)
            : this(groupOverview.DisplayName, groupOverview.GroupMail)
        { }

        public Group(string displayName, string groupMail)
        {
            this.DisplayName = string.IsNullOrEmpty(displayName) ? throw new ArgumentNullException(nameof(displayName)) : displayName;
            this.GroupMail = string.IsNullOrEmpty(groupMail) ? throw new ArgumentNullException(nameof(groupMail)) : groupMail;
        }

        public string DisplayName { get; set; }

        public string GroupMail { get; set; }
    }
}
