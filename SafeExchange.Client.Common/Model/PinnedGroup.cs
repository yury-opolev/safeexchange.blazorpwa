/// <summary>
/// PinnedGroup
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class PinnedGroup
    {
        public PinnedGroup()
        { }

        public PinnedGroup(PinnedGroupOutput pinnedGroupOutput)
            : this(pinnedGroupOutput.GroupId, pinnedGroupOutput.GroupDisplayName, pinnedGroupOutput.GroupMail)
        { }

        public PinnedGroup(string groupId, string groupDisplayName, string? groupMail)
        {
            this.GroupId = string.IsNullOrEmpty(groupId) ? throw new ArgumentNullException(nameof(groupId)) : groupId;
            this.GroupDisplayName = string.IsNullOrEmpty(groupDisplayName) ? throw new ArgumentNullException(nameof(groupDisplayName)) : groupDisplayName;
            this.GroupMail = groupMail;
        }

        public string GroupId { get; set; }

        public string GroupDisplayName { get; set; }

        public string? GroupMail { get; set; }
    }
}
