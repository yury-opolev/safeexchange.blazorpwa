/// <summary>
/// McpSettingsState — view state behind the "Allow MCP (AI agents)" switch in
/// the PWA settings dialog. Loads and saves the caller's own MCP opt-in via
/// /me/settings, applies the change optimistically and reverts it when the save
/// fails, and disables the switch while an administrator block is in place.
/// Kept UI-framework free so it can be unit-tested against a stubbed ApiClient.
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    using SafeExchange.Client.Common.Model;
    using System;
    using System.Threading.Tasks;

    public sealed class McpSettingsState
    {
        public const string BlockedByAdminSubStatus = "mcp_blocked_by_admin";

        public const string BlockedNotice = "Blocked by an administrator. Contact your SafeExchange administrator.";

        private readonly ApiClient apiClient;

        public McpSettingsState(ApiClient apiClient)
        {
            this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        }

        public bool IsLoaded { get; private set; }

        public bool IsBusy { get; private set; }

        public bool McpAllowed { get; private set; }

        public bool McpBlockedByAdmin { get; private set; }

        public string? Error { get; private set; }

        /// <summary>
        /// The exception from the last load/save, if any. The component inspects it
        /// for auth exceptions (e.g. an expired token) that need a sign-in redirect.
        /// </summary>
        public Exception? LastException { get; private set; }

        public bool SwitchChecked => this.McpAllowed && !this.McpBlockedByAdmin;

        public bool SwitchDisabled => !this.IsLoaded || this.IsBusy || this.McpBlockedByAdmin;

        public bool ShowBlockedNotice => this.McpBlockedByAdmin;

        public async Task LoadAsync()
        {
            this.IsBusy = true;
            this.Error = null;
            this.LastException = null;
            try
            {
                var response = await this.apiClient.GetMySettingsAsync();
                if ("ok".Equals(response.Status) && response.Result is not null)
                {
                    this.Apply(response.Result);
                    this.IsLoaded = true;
                }
                else
                {
                    this.Error = response.Error ?? response.Status;
                }
            }
            catch (Exception ex)
            {
                this.LastException = ex;
                this.Error = $"Could not load settings: {ex.Message}";
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        public async Task SetAllowedAsync(bool allowed)
        {
            if (this.SwitchDisabled)
            {
                return;
            }

            // Optimistic: the switch re-renders with the new value straight away and
            // with the old one again on failure, so the DOM checkbox is always reset.
            var previous = this.McpAllowed;
            this.McpAllowed = allowed;
            this.IsBusy = true;
            this.Error = null;
            this.LastException = null;
            try
            {
                var response = await this.apiClient.UpdateMySettingsAsync(allowed);
                if ("ok".Equals(response.Status) && response.Result is not null)
                {
                    this.Apply(response.Result);
                }
                else if (BlockedByAdminSubStatus.Equals(response.SubStatus))
                {
                    this.McpAllowed = previous;
                    this.McpBlockedByAdmin = true;
                }
                else
                {
                    this.McpAllowed = previous;
                    this.Error = response.Error ?? response.Status;
                }
            }
            catch (Exception ex)
            {
                this.McpAllowed = previous;
                this.LastException = ex;
                this.Error = $"Could not save the setting: {ex.Message}";
            }
            finally
            {
                this.IsBusy = false;
            }
        }

        private void Apply(UserSettings settings)
        {
            this.McpAllowed = settings.McpAllowed;
            this.McpBlockedByAdmin = settings.McpBlockedByAdmin;
        }
    }
}
