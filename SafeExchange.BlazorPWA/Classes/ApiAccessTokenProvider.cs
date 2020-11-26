/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
    using Microsoft.Extensions.Options;
    using Microsoft.JSInterop;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class ApiAccessTokenProvider<TProviderOptions> :
        IAccessTokenProvider
        where TProviderOptions : new()
    {
        /// <summary>
        /// Gets the <see cref="IJSRuntime"/> to use for performing JavaScript interop operations.
        /// </summary>
        protected IJSRuntime JsRuntime { get; }

        /// <summary>
        /// Gets the <see cref="NavigationManager"/> used to compute absolute urls.
        /// </summary>
        protected NavigationManager Navigation { get; }

        /// <summary>
        /// Gets the options for the underlying JavaScript library handling the authentication operations.
        /// </summary>
        protected RemoteAuthenticationOptions<TProviderOptions> Options { get; }

        private CachedAuthSettings authSettings;

        public ApiAccessTokenProvider(IJSRuntime jsRuntime, IOptions<RemoteAuthenticationOptions<TProviderOptions>> options, NavigationManager navigation)
        {
            JsRuntime = jsRuntime;
            Navigation = navigation;
            Options = options.Value;
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken()
        {
            return await this.RequestAccessToken(null);
        }

        public async ValueTask<AccessTokenResult> RequestAccessToken(AccessTokenRequestOptions options)
        {
            var userAuthData = await TryGetUserAuthDataAsync();
            if ((userAuthData == null) || !this.TryCreateAccessToken(userAuthData, out var accessToken))
            {
                var redirectUrlString = this.GetRedirectUrl(null).ToString();
                return new AccessTokenResult(AccessTokenResultStatus.RequiresRedirect, null, redirectUrlString);
            }

            return new AccessTokenResult(AccessTokenResultStatus.Success, accessToken, string.Empty);
        }

        private Uri GetRedirectUrl(string customReturnUrl)
        {
            var returnUrl = customReturnUrl != null ? Navigation.ToAbsoluteUri(customReturnUrl).ToString() : null;
            var encodedReturnUrl = Uri.EscapeDataString(returnUrl ?? Navigation.Uri);
            var redirectUrl = Navigation.ToAbsoluteUri($"{Options.AuthenticationPaths.LogInPath}?returnUrl={encodedReturnUrl}");
            return redirectUrl;
        }

        private async Task EnsureCachedAuthSettingsAsync()
        {
            if (this.authSettings != null)
            {
                return;
            }

            var settingsKey = "Microsoft.AspNetCore.Components.WebAssembly.Authentication.CachedAuthSettings";
            var authSettingsRaw = await JsRuntime.InvokeAsync<string>("sessionStorage.getItem", settingsKey);
            this.authSettings = JsonSerializer.Deserialize<CachedAuthSettings>(authSettingsRaw);
        }

        private async Task<UserAuthData> TryGetUserAuthDataAsync()
        {
            await this.EnsureCachedAuthSettingsAsync();

            try
            {
                var userRaw = await JsRuntime.InvokeAsync<string>("sessionStorage.getItem", this.authSettings?.OIDCUserKey);
                return JsonSerializer.Deserialize<UserAuthData>(userRaw);
            }
            catch
            {
                return null;
            }
        }

        private bool TryCreateAccessToken(UserAuthData userAuthData, out AccessToken accessToken)
        {
            accessToken = null;
            if (!TryParseClaimsFromJwt(userAuthData.id_token, out var claimsDictionary))
            {
                return false;
            }

            try
            {
                var expiresLong = ((JsonElement)claimsDictionary["exp"]).GetInt64();
                accessToken = new AccessToken()
                {
                    Value = userAuthData.id_token,
                    Expires = DateTimeOffset.FromUnixTimeSeconds(expiresLong)
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryParseClaimsFromJwt(string jwt, out IDictionary<string, object> claims)
        {
            claims = null;
            try
            {
                var payload = jwt.Split('.')[1];
                var jsonBytes = ParseBase64WithoutPadding(payload);
                claims = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] ParseBase64WithoutPadding(string base64)
        {
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            return Convert.FromBase64String(base64);
        }
    }
}
