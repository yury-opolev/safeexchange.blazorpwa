// Thin wrapper around the Application Insights JS SDK.
//
// Design goals:
//   1. Emit telemetry only when (a) initialized with a connection string
//      AND (b) the user is authenticated. Both gates must be true.
//   2. Expose a single window object (window.saexTelemetry) that C# can
//      drive via IJSRuntime.
//   3. Degrade to silent no-op when disabled — never throw back to the
//      caller, never block the UI.
//
// The SDK itself (ai.<version>.min.js) is bundled in wwwroot/js/ rather
// than loaded from the Microsoft CDN so the page's CSP can keep
// script-src strict ('self' only).

window.saexTelemetry = (() => {
    let appInsights = null;
    let initialized = false;
    let authenticated = false;

    function isReady() {
        return initialized && authenticated && appInsights !== null;
    }

    return {
        initialize: (connectionString) => {
            if (initialized) {
                return;
            }
            if (!connectionString) {
                return;
            }
            if (!window.Microsoft || !window.Microsoft.ApplicationInsights) {
                console.warn("App Insights SDK script failed to load; telemetry disabled.");
                return;
            }

            const { ApplicationInsights } = window.Microsoft.ApplicationInsights;
            appInsights = new ApplicationInsights({
                config: {
                    connectionString: connectionString,

                    // Blazor drives navigation itself — avoid double page views
                    // and let the C# side call trackPageView on NavigationManager
                    // LocationChanged events.
                    enableAutoRouteTracking: false,

                    // Anonymous-session gate lives in this wrapper (see
                    // isReady() below). Do NOT set disableTelemetry: true
                    // at construction — the v3 SDK treats that value as
                    // effectively immutable, so flipping it at runtime
                    // after setAuthenticated(true) does not actually
                    // re-enable emission.

                    // W3C Trace Context for correlation across client and
                    // backend. distributedTracingMode = 2 == AI_AND_W3C.
                    distributedTracingMode: 2,
                    enableCorsCorrelation: true,
                    enableRequestHeaderTracking: true,
                    enableResponseHeaderTracking: false,

                    // Capture exceptions that escape all handlers as a
                    // backstop; the Blazor ErrorBoundary catches the ones
                    // it can reach via .NET.
                    autoTrackPageVisitTime: true,

                    // Sampling is applied at the SDK. 100% for now; lower
                    // if daily volume grows.
                    samplingPercentage: 100
                }
            });
            appInsights.loadAppInsights();
            initialized = true;
        },

        setAuthenticated: (isAuth, userId) => {
            if (!initialized) {
                return;
            }
            authenticated = isAuth;
            if (isAuth) {
                if (userId) {
                    // We pass an opaque oid rather than UPN/email to avoid
                    // leaking PII into telemetry. See
                    // docs/telemetry/security-considerations.md.
                    appInsights.setAuthenticatedUserContext(userId, undefined, true);
                }
            } else {
                appInsights.clearAuthenticatedUserContext();
            }
        },

        trackEvent: (name, properties) => {
            if (!isReady()) {
                return;
            }
            appInsights.trackEvent({ name: name }, properties || undefined);
        },

        trackException: (message, stack, properties) => {
            if (!isReady()) {
                return;
            }
            const err = new Error(message || "(no message)");
            if (stack) {
                err.stack = stack;
            }
            appInsights.trackException({ exception: err, properties: properties || undefined });
        },

        trackTrace: (message, severityLevel, properties) => {
            if (!isReady()) {
                return;
            }
            appInsights.trackTrace({ message: message, severityLevel: severityLevel }, properties || undefined);
        },

        trackPageView: (name, uri) => {
            if (!isReady()) {
                return;
            }
            appInsights.trackPageView({ name: name, uri: uri });
        },

        flush: () => {
            if (!initialized || !appInsights) {
                return;
            }
            appInsights.flush();
        }
    };
})();
