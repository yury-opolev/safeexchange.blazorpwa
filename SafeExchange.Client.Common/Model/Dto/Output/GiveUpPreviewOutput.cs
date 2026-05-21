/// <summary>
/// GiveUpPreviewOutput — body of GET /v2/access-giveup/{secretId}. Describes what
/// would happen if the caller relinquished their direct permission row right now.
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class GiveUpPreviewOutput
    {
        public bool HasDirectRow { get; set; }

        public bool WouldOrphan { get; set; }

        public DateTime? CurrentExpireAt { get; set; }

        public DateTime? ProspectiveExpireAt { get; set; }
    }
}
