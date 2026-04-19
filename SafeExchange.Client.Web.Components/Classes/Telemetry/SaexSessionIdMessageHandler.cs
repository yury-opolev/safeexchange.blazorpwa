/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Stamps the current browser-session correlation id on every outgoing
/// request to the backend API as the x-saex-session-id header. A
/// backend middleware reads the header and pushes it into an ILogger
/// scope so every log entry in that request carries it in
/// customDimensions. Pairs with TelemetryService.SessionOperationId
/// on the client side so one Kusto query can stitch a single browser
/// session across the client and backend in Application Insights.
/// </summary>
public sealed class SaexSessionIdMessageHandler : DelegatingHandler
{
    public const string HeaderName = "x-saex-session-id";

    private readonly TelemetryService telemetry;

    public SaexSessionIdMessageHandler(TelemetryService telemetry)
    {
        this.telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sessionId = this.telemetry.SessionOperationId;
        if (!string.IsNullOrEmpty(sessionId) && !request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, sessionId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
