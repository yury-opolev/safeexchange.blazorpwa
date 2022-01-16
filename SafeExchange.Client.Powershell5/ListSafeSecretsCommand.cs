/// <summary>
/// ListSafeSecretsCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common.Model;
    using System.Collections.Generic;
    using System.Management.Automation;

    [Cmdlet("List", "SafeSecrets")]
    [OutputType(typeof(List<SubjectPermissionsOutput>))]
    public class ListSafeSecretsCommand : BaseSafeSecretCommand
    {
        protected override void ProcessRecord()
        {
            var secretsList = this.apiClient.ListSecretMetadataAsync().GetAwaiter().GetResult(); 
            WriteObject(secretsList);
        }
    }
}
