/// <summary>
/// NewClientTokenProviderCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using SafeExchange.Client.Common.Model;
    using System.Collections.Generic;
    using System.Management.Automation;

    [Cmdlet(VerbsCommon.New, Constants.ClientTokenProviderNoun)]
    [OutputType(typeof(List<SubjectPermissionsOutput>))]
    public class NewClientTokenProviderCommand : Cmdlet
    {
        [Parameter(Mandatory = true, ParameterSetName = "Default")]
        public SwitchParameter Default { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "SpecifiedSettings")]
        public string ClientId { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "SpecifiedSettings")]
        public string Authority { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "SpecifiedSettings")]
        public string TenantId { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "SpecifiedSettings")]
        public string RedirectUri { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "SpecifiedSettings")]
        public IEnumerable<string> Scopes { get; set; }

        protected override void ProcessRecord()
        {
            ClientTokenProvider result;
            if (this.Default)
            {
                result = new ClientTokenProvider(
                    BaseSafeSecretCommand.DefaultClientId,
                    BaseSafeSecretCommand.DefaultAuthority,
                    BaseSafeSecretCommand.DefaultTenantId,
                    BaseSafeSecretCommand.DefaultRedirectUri,
                    BaseSafeSecretCommand.DefaultScopes);
            }
            else
            {
                result = new ClientTokenProvider(this.ClientId, this.Authority, this.TenantId, this.RedirectUri, this.Scopes);
            }

            WriteObject(result);
        }
    }
}
