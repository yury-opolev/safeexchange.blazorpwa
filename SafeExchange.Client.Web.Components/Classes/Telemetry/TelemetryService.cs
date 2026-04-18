/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

/// <summary>
/// Mediator between C# code and the window.saexTelemetry wrapper in
/// wwwroot/js/telemetry.js. Honors two independent gates: Enabled flag
/// in appsettings, and the current authentication state.
/// </summary>
public sealed class TelemetryService : IAsyncDisposable
{
    private readonly IJSRuntime jsRuntime;
    private readonly AuthenticationStateProvider authStateProvider;
    private readonly TelemetryOptions options;
    private readonly string sessionOperationId;

    private bool initialized;
    private bool currentlyAuthenticated;

    public TelemetryService(
        IJSRuntime jsRuntime,
        AuthenticationStateProvider authStateProvider,
        IOptions<TelemetryOptions> options)
    {
        this.jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        this.authStateProvider = authStateProvider ?? throw new ArgumentNullException(nameof(authStateProvider));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.sessionOperationId = Guid.NewGuid().ToString("n");
    }

    public bool IsEnabled => this.options.Enabled && !string.IsNullOrEmpty(this.options.ConnectionString);

    /// <summary>
    /// Per-session correlation identifier. Included as a custom property on
    /// every emitted event so a single user journey can be filtered across
    /// page views, exceptions, and traces.
    /// </summary>
    public string SessionOperationId => this.sessionOperationId;

    public async ValueTask InitializeAsync()
    {
        if (!this.IsEnabled || this.initialized)
        {
            return;
        }

        try
        {
            await this.jsRuntime.InvokeVoidAsync("saexTelemetry.initialize", this.options.ConnectionString).ConfigureAwait(false);
            this.initialized = true;

            this.authStateProvider.AuthenticationStateChanged += this.OnAuthenticationStateChanged;
            var authState = await this.authStateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
            await this.ApplyAuthenticationStateAsync(authState).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Telemetry must never take the app down. If JS interop fails
            // (SDK script blocked, window gone, etc.), we stay in the
            // uninitialized state and silently no-op from here on.
            this.initialized = false;
        }
    }

    public async ValueTask TrackEventAsync(string name, IDictionary<string, string>? properties = null)
    {
        if (!this.initialized)
        {
            return;
        }

        var merged = this.WithSessionCorrelation(properties);
        await this.SafeInvokeAsync("saexTelemetry.trackEvent", name, merged).ConfigureAwait(false);
    }

    public async ValueTask TrackExceptionAsync(Exception exception, IDictionary<string, string>? properties = null)
    {
        if (!this.initialized || exception is null)
        {
            return;
        }

        var merged = this.WithSessionCorrelation(properties);
        await this.SafeInvokeAsync(
            "saexTelemetry.trackException",
            exception.Message,
            exception.StackTrace ?? string.Empty,
            merged).ConfigureAwait(false);
    }

    public async ValueTask TrackTraceAsync(string message, LogSeverityLevel severity, IDictionary<string, string>? properties = null)
    {
        if (!this.initialized)
        {
            return;
        }

        var merged = this.WithSessionCorrelation(properties);
        await this.SafeInvokeAsync(
            "saexTelemetry.trackTrace",
            message,
            (int)severity,
            merged).ConfigureAwait(false);
    }

    public async ValueTask TrackPageViewAsync(string name, string uri)
    {
        if (!this.initialized)
        {
            return;
        }

        await this.SafeInvokeAsync("saexTelemetry.trackPageView", name, uri).ConfigureAwait(false);
    }

    public async ValueTask FlushAsync()
    {
        if (!this.initialized)
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
        this.currentlyAuthenticated = isAuthenticated;

        string? opaqueId = null;
        if (isAuthenticated && user is not null)
        {
            // Prefer the AAD oid claim — it is tenant-specific, opaque,
            // and not a human-readable identifier. Never pass UPN/email
            // to the SDK.
            opaqueId = user.FindFirst("oid")?.Value
                       ?? user.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                       ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        await this.SafeInvokeAsync("saexTelemetry.setAuthenticated", isAuthenticated, opaqueId).ConfigureAwait(false);
    }

    private IDictionary<string, string> WithSessionCorrelation(IDictionary<string, string>? source)
    {
        var merged = source is null ? new Dictionary<string, string>() : new Dictionary<string, string>(source);
        merged["saex.sessionId"] = this.sessionOperationId;
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
}

public enum LogSeverityLevel
{
    Verbose = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
    Critical = 4
}
