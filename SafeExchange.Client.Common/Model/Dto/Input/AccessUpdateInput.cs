/// <summary>
/// AccessUpdateInput — body of PATCH /v2/access/{secretId}. Performs grant + revoke
/// atomically so the orphan-detection rule sees the post-change state, not an
/// intermediate one (which the legacy DELETE-then-POST flow could expose).
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System.Collections.Generic;

    public class AccessUpdateInput
    {
        public List<SubjectPermissionsInput> Add { get; set; }

        public List<SubjectPermissionsInput> Remove { get; set; }
    }
}
