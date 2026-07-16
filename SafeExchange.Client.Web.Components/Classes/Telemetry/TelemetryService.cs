/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SafeExchange.Client.Common;

/// <summary>
/// Mediator between C# code and the window.saexTelemetry wrapper in
/// wwwroot/js/telemetry.js.
///
/// Two gates decide whether telemetry actually leaves the browser:
///  1. The SDK is not initialised until the user is authenticated AND the
///     backend returns an Application Insights connection string from
///     GET /v2/telemetry/config.
///  2. The JS wrapper's isReady() check only returns true when
///     setAuthenticated(true) has been called.
///
/// The connection string lives only in the backend's Key Vault —
/// nothing in the public wwwroot/appsettings.json bundle. See
/// docs/telemetry/ for the full design and threat model.
/// </summary>
public sealed class TelemetryService : IAsyncDisposable
{
    // Derive from the shared client ApiVersion so this stays on the same version as every
    // other backend call (v3) instead of pinning to a stale one.
    private static readonly string ConfigEndpoint = $"{ApiClient.ApiVersion}/telemetry/config";
    private const string BackendApiClientName = "BackendApi";

    private readonly IJSRuntime jsRuntime;
    private readonly AuthenticationStateProvider authStateProvider;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly SessionCorrelation correlation;
    private readonly ILogger<TelemetryService> log;

    private bool sdkInitialized;
    private bool currentlyAuthenticated;
    private bool configFetchAttempted;

    public TelemetryService(
        IJSRuntime jsRuntime,
        AuthenticationStateProvider authStateProvider,
        IHttpClientFactory httpClientFactory,
        SessionCorrelation correlation,
        ILogger<TelemetryService> log)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        this.authStateProvider = authStateProvider ?? throw new ArgumentNullException(nameof(authStateProvider));
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        this.correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>
    /// Always true from a wiring standpoint — the real gate is the backend's
    /// response to GET /v2/telemetry/config plus the authentication state.
    /// Kept as a property so LoginDisplay can still show the session id
    /// (hiding it on anonymous sessions, visible once the SDK initialises).
    /// </summary>
    public bool IsEnabled => this.sdkInitialized;

    /// <summary>
    /// Fires whenever <see cref="IsEnabled"/> flips. Components that gate
    /// UI on IsEnabled (LoginDisplay showing the session id) should
    /// subscribe and call <c>StateHasChanged</c> so they re-render when
    /// the async backend config fetch completes. Without this signal the
    /// component only re-renders on the next unrelated user action, so
    /// the dropdown appears empty on first open and attached tooltips
    /// have no elements to bind to.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Per-session correlation identifier. Included as a custom property on
    /// every emitted event so a single user journey can be filtered across
    /// page views, exceptions, and traces.
    /// </summary>
    public string SessionOperationId => this.correlation.SessionId;

    public ValueTask InitializeAsync()
    {
        // Subscribe to authentication state and immediately apply the current
        // state. SDK initialisation is deferred to that applicator because
        // the connection string must be fetched from an authenticated
        // backend endpoint, so it is only available after sign-in.
        this.authStateProvider.AuthenticationStateChanged += this.OnAuthenticationStateChanged;
        return new ValueTask(Task.Run(async () =>
        {
            try
            {
                var authState = await this.authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
                await this.ApplyAuthenticationStateAsync(authState).ConfigureAwait(false);
            }
            catch
            {
                // Telemetry must never take the app down.
            }
        }));
    }

    // Track* methods are intentionally NOT awaited internally: telemetry must
    // never sit in an awaited critical path and delay a UI operation. They kick
    // off the JS interop fire-and-forget and return a completed ValueTask, so
    // existing `await Telemetry.Track...` call sites complete instantly.
    // SafeInvokeAsync swallows all exceptions, so the discarded task is safe.
    public ValueTask TrackEventAsync(string name, IDictionary<string, string>? properties = null)
    {
        if (!this.sdkInitialized)
        {
            return ValueTask.CompletedTask;
        }

        var merged = this.WithSessionCorrelation(properties);
        _ = this.SafeInvokeAsync("saexTelemetry.trackEvent", name, merged);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackExceptionAsync(Exception exception, IDictionary<string, string>? properties = null)
    {
        if (!this.sdkInitialized || exception is null)
        {
            return ValueTask.CompletedTask;
        }

        var merged = this.WithSessionCorrelation(properties);
        _ = this.SafeInvokeAsync(
            "saexTelemetry.trackException",
            exception.Message,
            exception.StackTrace ?? string.Empty,
            merged);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackTraceAsync(string message, LogSeverityLevel severity, IDictionary<string, string>? properties = null)
    {
        if (!this.sdkInitialized)
        {
            return ValueTask.CompletedTask;
        }

        var merged = this.WithSessionCorrelation(properties);
        _ = this.SafeInvokeAsync(
            "saexTelemetry.trackTrace",
            message,
            (int)severity,
            merged);
        return ValueTask.CompletedTask;
    }

    public ValueTask TrackPageViewAsync(string name, string uri)
    {
        if (!this.sdkInitialized)
        {
            return ValueTask.CompletedTask;
        }

        _ = this.SafeInvokeAsync("saexTelemetry.trackPageView", name, uri);
        return ValueTask.CompletedTask;
    }

    public async ValueTask FlushAsync()
    {
        if (!this.sdkInitialized)
        {
            return;
        }

        await this.SafeInvokeAsync("saexTelemetry.flush").ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        this.authStateProvider.AuthenticationStateChanged -= this.OnAuthenticationStateChanged;
        await this.FlushAsync().ConfigureAwait(false);
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var state = await task.ConfigureAwait(false);
                await this.ApplyAuthenticationStateAsync(state).ConfigureAwait(false);
            }
            catch
            {
                // Swallowed deliberately — any failure here is a telemetry
                // concern, not an application concern.
            }
        });
    }

    private async ValueTask ApplyAuthenticationStateAsync(AuthenticationState state)
    {
        var user = state?.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true;

        if (isAuthenticated == this.currentlyAuthenticated)
        {
            return;
        }
        var transitionedToAuthenticated = isAuthenticated && !this.currentlyAuthenticated;
        this.currentlyAuthenticated = isAuthenticated;

        if (!isAuthenticated)
        {
            if (this.sdkInitialized)
            {
                await this.SafeInvokeAsync("saexTelemetry.setAuthenticated", false).ConfigureAwait(false);
            }
            return;
        }

        // Authenticated. Fetch the connection string from the backend if we
        // have not already tried. One attempt per session — a failure here
        // silently disables telemetry for this page load rather than
        // retry-storming the backend.
        if (!this.configFetchAttempted)
        {
            this.configFetchAttempted = true;
            await this.TryInitialiseSdkAsync().ConfigureAwait(false);
        }

        if (!this.sdkInitialized)
        {
            return;
        }

        // No user identifier flows to the SDK — saex.sessionId is enough for
        // within-session correlation, and we do not want ai.user.authUserId
        // (the oid) or ai.user.id (the SDK's ~1-year cookie) to persist.
        await this.SafeInvokeAsync("saexTelemetry.setAuthenticated", true).ConfigureAwait(false);

        if (transitionedToAuthenticated)
        {
            await this.TrackEventAsync("SessionStarted").ConfigureAwait(false);
        }
    }

    private async ValueTask TryInitialiseSdkAsync()
    {
        try
        {
            using var httpClient = this.httpClientFactory.CreateClient(BackendApiClientName);
            using var response = await httpClient.GetAsync(ConfigEndpoint).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                this.log.LogWarning("Telemetry config endpoint returned {StatusCode}; telemetry stays disabled.", (int)response.StatusCode);
                return;
            }

            var payload = await response.Content
                .ReadFromJsonAsync<ConfigEnvelope>(JsonOptions)
                .ConfigureAwait(false);
            if (payload?.Result is null || !payload.Result.Enabled || string.IsNullOrWhiteSpace(payload.Result.ConnectionString))
            {
                return;
            }

            await this.jsRuntime
                .InvokeVoidAsync("saexTelemetry.initialize", payload.Result.ConnectionString)
                .ConfigureAwait(false);
            this.sdkInitialized = true;
            this.StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            // Silent fallback — a telemetry outage must never break the app.
            this.log.LogWarning(ex, "Failed to initialise telemetry SDK from backend config.");
        }
    }

    private IDictionary<string, string> WithSessionCorrelation(IDictionary<string, string>? source)
    {
        var merged = source is null ? new Dictionary<string, string>() : new Dictionary<string, string>(source);
        merged["saex.sessionId"] = this.correlation.SessionId;
        return merged;
    }

    private async ValueTask SafeInvokeAsync(string identifier, params object?[] args)
    {
        try
        {
            await this.jsRuntime.InvokeVoidAsync(identifier, args).ConfigureAwait(false);
        }
        catch
        {
            // Intentionally swallowed — see InitializeAsync for the rationale.
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class ConfigEnvelope
    {
        public string? Status { get; set; }

        public ConfigPayload? Result { get; set; }
    }

    private sealed class ConfigPayload
    {
        public bool Enabled { get; set; }

        public string ConnectionString { get; set; } = string.Empty;
    }
}

public enum LogSeverityLevel
{
    Verbose = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
