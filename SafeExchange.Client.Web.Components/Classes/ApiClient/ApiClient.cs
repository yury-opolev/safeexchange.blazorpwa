﻿/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using SafeExchange.Client.Web.Components.Model;
    using System;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class ApiClient
    {
        private readonly HttpClient client;

        private readonly JsonSerializerOptions jsonOptions;

        public ApiClient(IHttpClientFactory clientFactory)
        {
            if (clientFactory == null)
            {
                throw new ArgumentNullException(nameof(clientFactory));
            }
            this.client = clientFactory.CreateClient("BackendApi");

            this.jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public async Task<ApiNotificationSubscriptionReply> Subscribe(NotificationSubscription subscription)
        {
            var responseMessage = await client.PostAsJsonAsync($"notifications", subscription);
            return await this.HandleSubscriptionResponseAsync(responseMessage);
        }

        public async Task<ApiNotificationSubscriptionReply> Unsubscribe(NotificationSubscription subscription)
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"notifications")
            {
                Content = JsonContent.Create(subscription, mediaType: null, jsonOptions)
            };
            var responseMessage = await client.SendAsync(httpRequestMessage);
            return await this.HandleSubscriptionResponseAsync(responseMessage);
        }

        public async Task<ApiAccessRequestsListReply> GetAccessRequestsAsync()
        {
            var responseMessage = await client.GetAsync($"accessrequests");
            return await this.HandleAccessRequestsListAsync(responseMessage);
        }

        public async Task<ApiAccessRequestReply> CreateAccessRequestAsync(AccessRequestDataInput data)
        {
            var responseMessage = await client.PostAsJsonAsync("accessrequest", data);
            return await this.HandleAccessRequestResponseAsync(responseMessage);
        }

        public async Task<ApiAccessRequestReply> ProcessAccessRequestAsync(AccessRequestProcessingDataInput data)
        {
            var content = JsonContent.Create(data, mediaType: null, jsonOptions);
            var responseMessage = await client.PatchAsync("accessrequest", content);
            return await this.HandleAccessRequestResponseAsync(responseMessage);
        }

        public async Task<ApiAccessRequestReply> CancelAccessRequestAsync(AccessRequestCancellationDataInput data)
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, "accessrequest")
            {
                Content = JsonContent.Create(data, mediaType: null, jsonOptions)
            };
            var responseMessage = await client.SendAsync(httpRequestMessage);
            return await this.HandleAccessRequestResponseAsync(responseMessage);
        }

        public async Task<ApiSecretReply> CreateSecretDataAsync(string objectName, SecretDataInput data)
        {
            var responseMessage = await client.PostAsJsonAsync($"secrets/{objectName}", data);
            return await this.HandleResponseAsync(responseMessage);
        }

        public async Task<ApiSecretReply> GetSecretDataAsync(string objectName)
        {
            var responseMessage = await client.GetAsync($"secrets/{objectName}");
            return await this.HandleResponseAsync(responseMessage);
        }

        public async Task<ApiSecretReply> UpdateSecretDataAsync(string objectName, SecretDataInput data)
        {
            var content = JsonContent.Create(data, mediaType: null, jsonOptions);
            var responseMessage = await client.PatchAsync($"secrets/{objectName}", content);
            return await this.HandleResponseAsync(responseMessage);
        }

        public async Task<ApiSecretReply> DeleteSecretDataAsync(string objectName)
        {
            var responseMessage = await client.DeleteAsync($"secrets/{objectName}");
            return await this.HandleResponseAsync(responseMessage);
        }

        public async Task<ApiSecretsListReply> ListSecretsAsync()
        {
            var responseMessage = await client.GetAsync($"secrets/");
            return await this.HandleListResponseAsync(responseMessage);
        }

        public async Task<ApiAccessReply> GrantAccessAsync(string objectName, AccessDataInput data)
        {
            var responseMessage = await client.PostAsJsonAsync($"access/{objectName}", data);
            return await this.HandleAccessResponseAsync(responseMessage);
        }

        public async Task<ApiAccessReply> ReadAccessAsync(string objectName)
        {
            var responseMessage = await client.GetAsync($"access/{objectName}");
            return await this.HandleAccessResponseAsync(responseMessage);
        }

        public async Task<ApiAccessReply> RevokeAccessAsync(string objectName, AccessDataInput data)
        {
            var httpRequestMessage = new HttpRequestMessage(HttpMethod.Delete, $"access/{objectName}")
            {
                Content = JsonContent.Create(data, mediaType: null, jsonOptions)
            };
            var responseMessage = await client.SendAsync(httpRequestMessage);
            return await this.HandleAccessResponseAsync(responseMessage);
        }

        private async Task<ApiSecretReply> HandleResponseAsync(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
            {
                return await message.Content.ReadFromJsonAsync<ApiSecretReply>();
            }

            var responseContent = await message.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<ApiSecretReply>(responseContent, this.jsonOptions);

            if (!string.IsNullOrEmpty(response.Status))
            {
                return response;
            }

            return new ApiSecretReply()
            {
                Status = message.StatusCode.ToString(),
                Error = responseContent
            };
        }

        private async Task<ApiSecretsListReply> HandleListResponseAsync(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
            {
                return await message.Content.ReadFromJsonAsync<ApiSecretsListReply>();
            }

            var responseContent = await message.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<ApiSecretsListReply>(responseContent, this.jsonOptions);

            if (!string.IsNullOrEmpty(response.Status))
            {
                return response;
            }

            return new ApiSecretsListReply()
            {
                Status = message.StatusCode.ToString(),
                Error = responseContent
            };
        }

        private async Task<ApiAccessReply> HandleAccessResponseAsync(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
            {
                return await message.Content.ReadFromJsonAsync<ApiAccessReply>();
            }

            var responseContent = await message.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<ApiAccessReply>(responseContent, this.jsonOptions);

            if (!string.IsNullOrEmpty(response.Status))
            {
                return response;
            }

            return new ApiAccessReply()
            {
                Status = message.StatusCode.ToString(),
                Error = responseContent
            };
        }

        private async Task<ApiNotificationSubscriptionReply> HandleSubscriptionResponseAsync(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
            {
                return await message.Content.ReadFromJsonAsync<ApiNotificationSubscriptionReply>();
            }

            var responseContent = await message.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<ApiNotificationSubscriptionReply>(responseContent, this.jsonOptions);

            if (!string.IsNullOrEmpty(response.Status))
            {
                return response;
            }

            return new ApiNotificationSubscriptionReply()
            {
                Status = message.StatusCode.ToString(),
                Error = responseContent
            };
        }

        private async Task<ApiAccessRequestsListReply> HandleAccessRequestsListAsync(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
            {
                return await message.Content.ReadFromJsonAsync<ApiAccessRequestsListReply>();
            }

            var responseContent = await message.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<ApiAccessRequestsListReply>(responseContent, this.jsonOptions);

            if (!string.IsNullOrEmpty(response.Status))
            {
                return response;
            }

            return new ApiAccessRequestsListReply()
            {
                Status = message.StatusCode.ToString(),
                Error = responseContent
            };
        }

        private async Task<ApiAccessRequestReply> HandleAccessRequestResponseAsync(HttpResponseMessage message)
        {
            if (message.IsSuccessStatusCode)
            {
                return await message.Content.ReadFromJsonAsync<ApiAccessRequestReply>();
            }

            var responseContent = await message.Content.ReadAsStringAsync();
            var response = JsonSerializer.Deserialize<ApiAccessRequestReply>(responseContent, this.jsonOptions);

            if (!string.IsNullOrEmpty(response.Status))
            {
                return response;
            }

            return new ApiAccessRequestReply()
            {
                Status = message.StatusCode.ToString(),
                Error = responseContent
            };
        }
    }
}
