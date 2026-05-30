/// <summary>
/// ApiClient.Admin — partial of ApiClient with the admin-side paginated
/// list endpoints. Lives in its own file so the admin surface can evolve
/// without growing the main ApiClient.cs.
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

        public Task<BaseResponseObject<string>> DeleteApplicationAsync(string displayName)
            => DeleteAsync<string>($"{ApiVersion}/admin/applications/{Uri.EscapeDataString(displayName)}");

        public Task<BaseResponseObject<S2SApp>> GetApplicationDetailAsync(string displayName)
            => GetAsync<S2SApp>($"{ApiVersion}/admin/applications/{Uri.EscapeDataString(displayName)}");

        public Task<BaseResponseObject<S2SApp>> ReplaceApplicationOwnersAsync(string displayName, IReadOnlyList<S2SAppOwnerInput> owners)
            => PutAsync<S2SApp>($"{ApiVersion}/admin/applications/{Uri.EscapeDataString(displayName)}/owners",
                new { owners });

        public Task<BaseResponseObject<PaginatedResult<SecretAdminOverview>>> ListSecretsAsync(
            string? q,
            int page = 0,
            int pageSize = 25,
            string? sortBy = null,
            string? sortDir = null,
            DateTime? accessedBefore = null,
            bool neverAccessed = false)
        {
            var qs = BuildPagingQuery(q, page, pageSize);
            if (!string.IsNullOrEmpty(sortBy))
            {
                qs += $"&sortBy={Uri.EscapeDataString(sortBy)}";
            }

            if (!string.IsNullOrEmpty(sortDir))
            {
                qs += $"&sortDir={Uri.EscapeDataString(sortDir)}";
            }

            if (accessedBefore.HasValue)
            {
                qs += $"&accessedBefore={Uri.EscapeDataString(accessedBefore.Value.ToUniversalTime().ToString("O"))}";
            }

            if (neverAccessed)
            {
                qs += "&neverAccessed=true";
            }

            return GetAsync<PaginatedResult<SecretAdminOverview>>($"{ApiVersion}/admin/secret-list{qs}");
        }

        public Task<BaseResponseObject<SecretAdminDetail>> GetSecretDetailAsync(string name)
            => GetAsync<SecretAdminDetail>($"{ApiVersion}/admin/secret/{Uri.EscapeDataString(name)}");

        public Task<BaseResponseObject<List<SecretAccessItem>>> GetSecretAccessAsync(string name)
            => GetAsync<List<SecretAccessItem>>($"{ApiVersion}/admin/secret/{Uri.EscapeDataString(name)}/access");

        public Task<BaseResponseObject<UserDetail>> GetUserDetailAsync(string upn)
            => GetAsync<UserDetail>($"{ApiVersion}/admin/users/{Uri.EscapeDataString(upn)}");

        public Task<BaseResponseObject<PaginatedResult<SecretAuditAnchorOverview>>> SearchAuditAsync(string? secretName, int page = 0, int pageSize = 25)
            => GetAsync<PaginatedResult<SecretAuditAnchorOverview>>($"{ApiVersion}/admin/audit{BuildAuditQuery(secretName, page, pageSize)}");

        public Task<BaseResponseObject<AdminSecretAuditPageOutput>> GetAuditInstanceAsync(string auditInstanceId, string? continuation = null, bool raw = false, string direction = "desc", int pageSize = 100)
        {
            var qs = $"?direction={Uri.EscapeDataString(direction)}&pageSize={pageSize}";
            if (raw)
            {
                qs += "&raw=true";
            }

            if (!string.IsNullOrEmpty(continuation))
            {
                qs += $"&continuation={Uri.EscapeDataString(continuation)}";
            }

            return GetAsync<AdminSecretAuditPageOutput>($"{ApiVersion}/admin/audit/{Uri.EscapeDataString(auditInstanceId)}{qs}");
        }

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

        private async Task<BaseResponseObject<T>> DeleteAsync<T>(string relative) where T : class
        {
            using var http = new HttpRequestMessage(HttpMethod.Delete, new Uri(this.client.BaseAddress!, relative));
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<T>(response);
        }

        private async Task<BaseResponseObject<T>> PutAsync<T>(string relative, object body) where T : class
        {
            using var http = new HttpRequestMessage(HttpMethod.Put, new Uri(this.client.BaseAddress!, relative))
            {
                Content = JsonContent.Create(body, options: this.jsonOptions),
            };
            var response = await this.client.SendAsync(http);
            return await DeserializeOrErrorAsync<T>(response);
        }
    }
}
