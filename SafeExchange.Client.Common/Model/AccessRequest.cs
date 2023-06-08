/// <summary>
/// AccessRequest
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;
    using System.Text.Json.Serialization;

    public class AccessRequest
    {
        public AccessRequest()
        { }

        public AccessRequest(AccessRequestOutput source)
        {
            this.Id = source.Id;
            this.RequestorType = source.SubjectType.ToSubjectType();
            this.Requestor = source.SubjectName;
            this.SecretName = source.ObjectName;

            this.CanRead = source.CanRead;
            this.CanWrite = source.CanWrite;
            this.CanGrantAccess = source.CanGrantAccess;
            this.CanRevokeAccess = source.CanRevokeAccess;

            this.RequestedAt = source.RequestedAt;
        }

        public string Id { get; set; }

        public SubjectType RequestorType { get; set; }

        public string Requestor { get; set; }

        public string SecretName { get; set; }

        public bool CanRead { get; set; }

        public bool CanWrite { get; set; }

        public bool CanGrantAccess { get; set; }

        public bool CanRevokeAccess { get; set; }

        public DateTime RequestedAt { get; set; }

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

        public SubjectPermissionsInput ToCreationDto() => new SubjectPermissionsInput()
        {
            CanRead = this.CanRead,
            CanWrite = this.CanWrite,
            CanGrantAccess = this.CanGrantAccess,
            CanRevokeAccess = this.CanRevokeAccess
        };

        public AccessRequestUpdateInput ToUpdateDto(bool approve) => new AccessRequestUpdateInput()
        {
            RequestId = this.Id,
            Approve = approve
        };

        public AccessRequestDeletionInput ToDeletionDto() => new AccessRequestDeletionInput()
        {
            RequestId = this.Id
        };
    }
}
