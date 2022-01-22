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
        [Parameter(Mandatory = true)]
        public string ClientId { get; set; }

        [Parameter(Mandatory = true)]
        public string Authority { get; set; }

        [Parameter(Mandatory = true)]
        public string TenantId { get; set; }

        [Parameter(Mandatory = true)]
        public string RedirectUri { get; set; }

        [Parameter(Mandatory = true)]
        public IEnumerable<string> Scopes { get; set; }

        protected override void ProcessRecord()
        {
            var tokenProvider = new ClientTokenProvider(this.ClientId, this.Authority, this.TenantId, this.RedirectUri, this.Scopes);
            WriteObject(tokenProvider);
        }
    }
}
