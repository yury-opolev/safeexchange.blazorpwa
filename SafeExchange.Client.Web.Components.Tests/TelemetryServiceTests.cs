/// <summary>
/// TelemetryServiceTests — verifies telemetry is decoupled from the calling
/// operation: a Track* call must return synchronously (fire-and-forget) even
/// when the underlying JS telemetry send never completes, so telemetry can
/// never sit in an awaited critical path and stall the UI.
/// </summary>

namespace SafeExchange.Client.Web.Components.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Security.Claims;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components.Authorization;
    using Microsoft.Extensions.Logging;
    using Microsoft.JSInterop;
    using NUnit.Framework;
    using SafeExchange.Client.Web.Components;

    [TestFixture]
    public class TelemetryServiceTests
    {
        [Test]
        public async Task TrackEventAsync_ReturnsSynchronously_WhenJsSendHangs()
        {
            var service = new TelemetryService(
                new HangingTelemetryJsRuntime(),
                new AuthenticatedStateProvider(),
                new ConfigHttpClientFactory(),
                new SessionCorrelation(),
                new NoopLogger<TelemetryService>());

            // Drives the SDK to "initialized" so we exercise the post-init path,
            // not the trivial !sdkInitialized early-return.
            await service.InitializeAsync();
            Assert.That(service.IsEnabled, Is.True, "SDK should initialise for a meaningful test.");

            // The fake JS runtime never completes a trackEvent for "BlockTest".
            // If TrackEventAsync awaited the interop, this ValueTask would be
            // incomplete; fire-and-forget makes it complete synchronously.
            var pending = service.TrackEventAsync("BlockTest");

            Assert.That(pending.IsCompleted, Is.True,
                "TrackEventAsync must not await the JS telemetry send.");
        }

        // ----------------------------- fakes -----------------------------

        private sealed class HangingTelemetryJsRuntime : IJSRuntime
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
                => this.InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "saexTelemetry.trackEvent"
                    && args is { Length: > 0 } && (args[0] as string) == "BlockTest")
                {
                    // Never completes — simulates a stuck/slow telemetry send.
                    return new ValueTask<TValue>(new TaskCompletionSource<TValue>().Task);
                }

                return new ValueTask<TValue>(default(TValue)!);
            }
        }

        private sealed class AuthenticatedStateProvider : AuthenticationStateProvider
        {
            public override Task<AuthenticationState> GetAuthenticationStateAsync()
                => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity("test"))));
        }

        private sealed class ConfigHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name)
                => new HttpClient(new ConfigHandler()) { BaseAddress = new Uri("https://test.example/api/") };
        }

        private sealed class ConfigHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                const string json = "{\"status\":\"ok\",\"result\":{\"enabled\":true,\"connectionString\":\"InstrumentationKey=test;IngestionEndpoint=https://x/\"}}";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class NoopLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => false;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }
        }
    }
}
