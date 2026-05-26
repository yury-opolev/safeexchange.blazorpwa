/// <summary>
/// ApiClient.Admin — partial of ApiClient with the admin-side paginated
/// list endpoints. Lives in its own file so the admin surface can evolve
/// without growing the main ApiClient.cs.
/// </summary>

namespace SafeExchange.Client.Common
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading.Tasks;

    public partial class ApiClient
    {
        public Task<BaseResponseObject<PaginatedResult<UserOverview>>> ListUsersAsync(string? q, int page = 0, int pageSize = 25)
            => GetAsync<PaginatedResult<UserOverview>>($"{ApiVersion}/admin/users{BuildPagingQuery(q, page, pageSize)}");

        public Task<BaseResponseObject<UserOverview>> SetUserEnabledAsync(string upn, bool enabled)
            => PatchAsync<UserOverview>($"{ApiVersion}/admin/users/{Uri.EscapeDataString(upn)}/enabled",
                new EnabledToggleRequest { Enabled = enabled });

        public Task<BaseResponseObject<PaginatedResult<ApplicationAdminOverview>>> ListApplicationsAsync(string? q, int page = 0, int pageSize = 25)
            => GetAsync<PaginatedResult<ApplicationAdminOverview>>($"{ApiVersion}/admin/applications{BuildPagingQuery(q, page, pageSize)}");

        public Task<BaseResponseObject<string>> SetApplicationEnabledAsync(string displayName, bool enabled)
            => PatchAsync<string>($"{ApiVersion}/admin/applications/{Uri.EscapeDataString(displayName)}/enabled",
                new EnabledToggleRequest { Enabled = enabled });

        public Task<BaseResponseObject<PaginatedResult<SecretAuditAnchorOverview>>> SearchAuditAsync(string? secretName, int page = 0, int pageSize = 25)
            => GetAsync<PaginatedResult<SecretAuditAnchorOverview>>($"{ApiVersion}/admin/audit{BuildAuditQuery(secretName, page, pageSize)}");

        private static string BuildPagingQuery(string? q, int page, int pageSize)
        {
            var qPart = string.IsNullOrEmpty(q) ? string.Empty : $"&q={Uri.EscapeDataString(q)}";
            return $"?page={page}&pageSize={pageSize}{qPart}";
        }

        private static string BuildAuditQuery(string? name, int page, int pageSize)
        {
            var nPart = string.IsNullOrEmpty(name) ? string.Empty : $"&secretName={Uri.EscapeDataString(name)}";
            return $"?page={page}&pageSize={pageSize}{nPart}";
        }

        private async Task<BaseResponseObject<T>> GetAsync<T>(string relative) where T : class
        {
            using var http = new HttpRequestMessage(HttpMethod.Get, new Uri(this.client.BaseAddress!, relative));
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<T>(response);
        }

        private async Task<BaseResponseObject<T>> PatchAsync<T>(string relative, object body) where T : class
        {
            using var http = new HttpRequestMessage(HttpMethod.Patch, new Uri(this.client.BaseAddress!, relative))
            {
                Content = JsonContent.Create(body, options: this.jsonOptions),
            };
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<T>(response);
        }
    }
}
