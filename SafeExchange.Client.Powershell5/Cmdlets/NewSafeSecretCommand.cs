/// <summary>
/// NewSafeSecretCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Security;

    [Cmdlet(VerbsCommon.New, Constants.SafeSecretNoun)]
    [OutputType(typeof(List<SubjectPermissionsOutput>))]
    public class NewSafeSecretCommand : BaseSingleSafeSecretCommand
    {
        [Parameter(Position = 1, ValueFromPipeline = true, Mandatory = true)]
        [ValidateNotNullOrEmpty]
        public SecureString Content { get; set; }

        [Parameter()]
        [ValidateCount(0, 5)]
        public string[] FilesToAttach { get; set; }

        [Parameter()]
        public SwitchParameter ScheduleExpiration { get; set; }

        [Parameter()]
        public DateTime ExpireAt { get; set; }

        [Parameter()]
        public SwitchParameter ExpireOnIdleTime { get; set; }

        [Parameter()]
        public TimeSpan IdleTimeToExpire { get; set; }

        protected override void ProcessRecord()
        {
            var input = new CompoundModel()
            {
                Metadata = new ObjectMetadata()
                {
                    ObjectName = this.Name,
                    Content = new List<ContentMetadata>(),
                    ExpirationMetadata = new ExpirationMetadata()
                    {
                        ScheduleExpiration = this.ScheduleExpiration,
                        ExpireAt = this.ExpireAt,
                        ExpireOnIdleTime = this.ExpireOnIdleTime,
                        IdleTimeToExpire = this.IdleTimeToExpire
                    }
                },
                MainData = Content.ToUnsecureString(),
                Permissions = new List<SubjectPermissions>()
            };

            var attachments = this.CreateAttachmentsList(this.FilesToAttach);
            var response = this.apiClient.CreateFromCompoundModelAsync(input, attachments).GetAwaiter().GetResult();

            if (!"ok".Equals(response.Status))
            {
                WriteError(new ErrorRecord(new Exception(response.Error), response.Status, ErrorCategory.NotSpecified, null));
                return;
            }

            WriteObject(new { this.Name, Link = this.CreateSafeSecretLink(this.Name) });
        }

        private List<AttachmentModel> CreateAttachmentsList(string[] filesToAttach)
        {
            var result = new List<AttachmentModel>();

            foreach (var fileName in filesToAttach)
            {
                var attachmentModel = new AttachmentModel(new InputLocalFileModel(fileName));
                result.Add(attachmentModel);
            }

            return result;
        }
    }
}
