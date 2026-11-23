/// <summary>
/// SequenceHttpMessageHandler — fake HttpMessageHandler that answers requests
/// from a queue of responders, in order, and records every request (and its
/// body) it receives. Used where one test drives several ApiClient calls.
/// </summary>

namespace SafeExchange.Client.Common.Tests.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class SequenceHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> responders = new();

        public List<HttpRequestMessage> Requests { get; } = new();

        public List<string?> RequestBodies { get; } = new();

        public static HttpResponseMessage Json(HttpStatusCode statusCode, string jsonBody)
            => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
            };

        public SequenceHttpMessageHandler Enqueue(HttpStatusCode statusCode, string jsonBody)
            => this.Enqueue(_ => Task.FromResult(Json(statusCode, jsonBody)));

        public SequenceHttpMessageHandler Enqueue(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            this.responders.Enqueue(responder);
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            this.RequestBodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

            if (this.responders.Count == 0)
            {
                throw new InvalidOperationException($"Unexpected request: {request.Method} {request.RequestUri}");
            }

            return await this.responders.Dequeue()(request).ConfigureAwait(false);
        }
    }
}
