/// <summary>
/// ApiClient.S2SApps — partial of ApiClient with the self-service S2S app
/// endpoints. Kept in its own file so the main ApiClient.cs doesn't grow a
/// "and another verb here" linear-scroll problem.
/// </summary>

namespace SafeExchange.Client.Common
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    public partial class ApiClient
    {
        /// <summary>POST /v2/s2sapps — self-service register.</summary>
        public async Task<BaseResponseObject<S2SApp>> RegisterS2SAppAsync(S2SAppRegistrationRequest request)
        {
            var url = new Uri(this.client.BaseAddress!, $"{ApiVersion}/s2sapps");
            using var http = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(request, options: this.jsonOptions),
            };
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<S2SApp>(response);
        }

        /// <summary>GET /v2/s2sapps/mine — apps where the caller is a direct user-owner.</summary>
        public async Task<BaseResponseObject<List<S2SAppOverview>>> ListMyS2SAppsAsync()
        {
            var url = new Uri(this.client.BaseAddress!, $"{ApiVersion}/me/s2sapps");
            using var http = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<List<S2SAppOverview>>(response);
        }

        /// <summary>GET /v2/s2sapps/{displayName} — detail (owner-only).</summary>
        public async Task<BaseResponseObject<S2SApp>> GetS2SAppAsync(string displayName)
        {
            var url = new Uri(this.client.BaseAddress!, $"{ApiVersion}/s2sapps/{Uri.EscapeDataString(displayName)}");
            using var http = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<S2SApp>(response);
        }

        /// <summary>DELETE /v2/s2sapps/{displayName} — delete app (owner-only; cascades owner rows).</summary>
        public async Task<BaseResponseObject<S2SApp>> DeleteS2SAppAsync(string displayName)
        {
            var url = new Uri(this.client.BaseAddress!, $"{ApiVersion}/s2sapps/{Uri.EscapeDataString(displayName)}");
            using var http = new HttpRequestMessage(HttpMethod.Delete, url);
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<S2SApp>(response);
        }

        /// <summary>GET /v2/s2sapps/{displayName}/owners — list owners (owner-only).</summary>
        public async Task<BaseResponseObject<List<S2SAppOwner>>> ListS2SAppOwnersAsync(string displayName)
        {
            var url = new Uri(this.client.BaseAddress!, $"{ApiVersion}/s2sapps/{Uri.EscapeDataString(displayName)}/owners");
            using var http = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<List<S2SAppOwner>>(response);
        }

        /// <summary>POST /v2/s2sapps/{displayName}/owners — add owner (owner-only; idempotent).</summary>
        public async Task<BaseResponseObject<S2SAppOwner>> AddS2SAppOwnerAsync(string displayName, S2SAppOwnerInput owner)
        {
            var url = new Uri(this.client.BaseAddress!, $"{ApiVersion}/s2sapps/{Uri.EscapeDataString(displayName)}/owners");
            using var http = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(owner, options: this.jsonOptions),
            };
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<S2SAppOwner>(response);
        }

        /// <summary>DELETE /v2/s2sapps/{displayName}/owners/{subjectType}/{subjectId} — remove owner (owner-only; refuses if invariant would break).</summary>
        public async Task<BaseResponseObject<S2SAppOwner>> RemoveS2SAppOwnerAsync(string displayName, OwnerSubjectType subjectType, string subjectId)
        {
            var url = new Uri(this.client.BaseAddress!, $"{ApiVersion}/s2sapps/{Uri.EscapeDataString(displayName)}/owners/{subjectType}/{Uri.EscapeDataString(subjectId)}");
            using var http = new HttpRequestMessage(HttpMethod.Delete, url);
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<S2SAppOwner>(response);
        }

        // Small helper, lifted to keep both methods readable. Returns the
        // typed response when the server returned JSON; otherwise an error
        // envelope so the caller's switch on `.Status` works uniformly.
        private async Task<BaseResponseObject<T>> DeserializeOrErrorAsync<T>(HttpResponseMessage response)
            where T : class
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                // S2SAppsSelfService feature flag off → server returns 204.
                return new BaseResponseObject<T> { Status = "disabled", Error = "Feature is disabled." };
            }

            // Defensive: if we point at the wrong backend (e.g. PWA still configured for
            // the deployed env), the response will be 404 HTML, not JSON. Catch that
            // before attempting JSON parsing so the UI gets a useful diagnostic.
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!mediaType.Contains("json", System.StringComparison.OrdinalIgnoreCase))
            {
                var snippet = await response.Content.ReadAsStringAsync();
                if (snippet.Length > 200) { snippet = snippet.Substring(0, 200) + "…"; }
                return new BaseResponseObject<T>
                {
                    Status = "error",
                    Error = $"Backend returned HTTP {(int)response.StatusCode} with {(string.IsNullOrEmpty(mediaType) ? "no content-type" : mediaType)} — expected JSON. Check that BackendApi:BaseAddress points to a running SafeExchange backend." +
                            (string.IsNullOrEmpty(snippet) ? string.Empty : $" Body: {snippet}"),
                };
            }

            try
            {
                var typed = await response.Content.ReadFromJsonAsync<BaseResponseObject<T>>(this.jsonOptions);
                return typed ?? new BaseResponseObject<T> { Status = "error", Error = "Empty response body." };
            }
            catch (Exception ex)
            {
                return new BaseResponseObject<T> { Status = "error", Error = $"Response was not parsable JSON: {ex.Message}" };
            }
        }
    }
}
