/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using System;

/// <summary>
/// Singleton holder for the per-browser-session correlation id.
///
/// Both <see cref="TelemetryService"/> and
/// <see cref="SaexSessionIdMessageHandler"/> must agree on the same
/// value — one emits it as a custom dimension on every client event,
/// the other attaches it to every outgoing BackendApi request so the
/// backend can stamp it on server-side telemetry.
///
/// Previously, <c>TelemetryService</c> generated the id in its
/// constructor and <c>SaexSessionIdMessageHandler</c> read it from
/// the injected service. This was empirically wrong on Blazor WASM:
/// <c>IHttpClientFactory</c> spins handler pipelines in a DI scope
/// separate from the UI scope, so the handler received a different
/// <c>TelemetryService</c> instance (and therefore a different GUID).
/// The client and the backend logged two different session ids for
/// the same browser load, breaking end-to-end correlation.
///
/// Registering a singleton <c>SessionCorrelation</c> means every
/// consumer — regardless of which scope it lives in — reads the
/// same GUID.
/// </summary>
public sealed class SessionCorrelation
{
    public SessionCorrelation()
    {
        this.SessionId = Guid.NewGuid().ToString("n");
    }

    public string SessionId { get; }
}
