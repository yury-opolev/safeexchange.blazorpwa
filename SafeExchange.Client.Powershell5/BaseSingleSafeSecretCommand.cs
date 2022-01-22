/// <summary>
/// BaseSingleSafeSecretCommand
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using System;
    using System.Management.Automation;

    public class BaseSingleSafeSecretCommand : BaseSafeSecretCommand
    {
        [Parameter(Position = 0, Mandatory = true, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        [ValidatePattern(@"^[a-zA-Z][a-zA-Z0-9\-]+$")]
        public string Name { get; set; }

        // ...
    }
}
