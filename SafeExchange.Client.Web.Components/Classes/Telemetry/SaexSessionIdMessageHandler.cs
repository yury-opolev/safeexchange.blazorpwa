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
/// backend middleware reads the header and stamps the same value on
/// server-side telemetry, so one Kusto query can stitch a single
/// browser session across the client and backend in Application
/// Insights.
///
/// Reads the id from the singleton <see cref="SessionCorrelation"/>
/// holder rather than from <see cref="TelemetryService"/> directly.
/// IHttpClientFactory creates its handler pipeline in a DI scope
/// separate from the UI scope, so a scoped TelemetryService resolved
/// here yields a different instance (and a different GUID) than the
/// one the dropdown shows. A singleton holder sidesteps that mismatch.
/// </summary>
public sealed class SaexSessionIdMessageHandler : DelegatingHandler
{
    public const string HeaderName = "x-saex-session-id";

    private readonly SessionCorrelation correlation;

    public SaexSessionIdMessageHandler(SessionCorrelation correlation)
    {
        this.correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sessionId = this.correlation.SessionId;
        if (!string.IsNullOrEmpty(sessionId) && !request.Headers.Contains(HeaderName))
        {
            request.Headers.TryAddWithoutValidation(HeaderName, sessionId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
