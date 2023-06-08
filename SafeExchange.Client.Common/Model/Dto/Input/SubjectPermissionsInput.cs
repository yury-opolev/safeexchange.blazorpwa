/// <summary>
/// SubjectPermissionsInput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class SubjectPermissionsInput
    {

        public SubjectTypeInput SubjectType { get; set; }

        public string SubjectName { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }
    }
}
