/// <summary>
/// PinnedSecretsHelper
/// </summary>

namespace SafeExchange.Client.Common.Helpers
{
    using SafeExchange.Client.Common;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public static class PinnedSecretsHelper
    {
        public static async Task<PinnedSecretToggleResult> SwitchSecretPinAsync(
            ApiClient apiClient, ISet<string> pinnedSecretNames, string secretName, bool newPinValue)
        {
            if (newPinValue)
            {
                string status;
                string? error;
                try
                {
                    var response = await apiClient.PutPinnedSecretAsync(secretName);
                    status = response.Status;
                    error = response.Error;
                }
                catch (Exception ex)
                {
                    status = "exception";
                    error = $"{ex.GetType()}: {ex.Message}";
                }

                var succeeded = status == "ok";
                if (succeeded)
                {
                    pinnedSecretNames.Add(secretName);
                }

                return new PinnedSecretToggleResult { Succeeded = succeeded, Status = status, Error = error };
            }
            else
            {
                string status;
                string? error;
                try
                {
                    var response = await apiClient.DeletePinnedSecretAsync(secretName);
                    status = response.Status;
                    error = response.Error;
                }
                catch (Exception ex)
                {
                    status = "exception";
                    error = $"{ex.GetType()}: {ex.Message}";
                }

                // Unpin is idempotent server-side: "no_content" means the pin was
                // already gone, which is still success from the user's point of view.
                var succeeded = status == "ok" || status == "no_content";
                if (succeeded)
                {
                    pinnedSecretNames.Remove(secretName);
                }

                return new PinnedSecretToggleResult { Succeeded = succeeded, Status = status, Error = error };
            }
        }
    }
}
