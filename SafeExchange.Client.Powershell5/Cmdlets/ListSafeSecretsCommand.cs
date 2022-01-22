/// <summary>
/// ListSafeSecretsCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Collections.Generic;
    using System.Management.Automation;

    [Cmdlet("List", Constants.SafeSecretsNoun)]
    [OutputType(typeof(List<SubjectPermissionsOutput>))]
    public class ListSafeSecretsCommand : BaseSafeSecretCommand
    {
        protected override void ProcessRecord()
        {
            var response = this.apiClient.ListSecretMetadataAsync().GetAwaiter().GetResult(); 

            if (!"ok".Equals(response.Status))
            {
                WriteError(new ErrorRecord(new Exception(response.Error), response.Status, ErrorCategory.NotSpecified, null));
                return;
            }

            WriteObject(response.Result);
        }
    }
}
