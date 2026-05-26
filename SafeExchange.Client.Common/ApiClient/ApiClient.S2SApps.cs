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
