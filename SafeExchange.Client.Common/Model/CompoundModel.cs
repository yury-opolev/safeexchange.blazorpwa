/// <summary>
/// CompoundModel
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class CompoundModel
    {
        public ObjectMetadata Metadata { get; set; }

        public List<SubjectPermissions> Permissions { get; set; }

        public string MainData { get; set; }
    }
}
