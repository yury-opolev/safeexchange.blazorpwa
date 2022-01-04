/// <summary>
/// SubjectPermissions
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Text.Json.Serialization;

    public class SubjectPermissions
    {
        public SubjectPermissions()
        { }

        public SubjectPermissions(SubjectPermissionsOutput source)
        {
            this.ObjectName = source.ObjectName;
            this.SubjectName = source.SubjectName;

            this.CanRead = source.CanRead;
            this.CanWrite = source.CanWrite;
            this.CanGrantAccess = source.CanGrantAccess;
            this.CanRevokeAccess = source.CanRevokeAccess;
        }

        public string ObjectName { get; set; }

        public string SubjectName { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        [JsonIgnore]
        public string PermissionsString
        {
            get => PermissionsConverter.ToPermissionString(this.CanRead, this.CanWrite, this.CanGrantAccess, this.CanRevokeAccess);

            set
            {
                if (PermissionsConverter.TryParsePermissionString(value, out bool read, out bool write, out bool grantAccess, out bool revokeAccess))
                {
                    this.CanRead = read;
                    this.CanWrite = write;
                    this.CanGrantAccess = grantAccess;
                    this.CanRevokeAccess = revokeAccess;
                }
            }
        }

        public SubjectPermissionsInput ToDto() => new ()
        {
            SubjectName = this.SubjectName,

            CanRead = this.CanRead,
            CanWrite = this.CanWrite,
            CanGrantAccess = this.CanGrantAccess,
            CanRevokeAccess = this.CanRevokeAccess
        };
    }
}
