/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using System;

    public class CachedAuthSettings
    {
        public string authority { get; set; }

        public string metadataUrl { get; set; }

        public string client_id { get; set; }

        public string[] defaultScopes { get; set; }

        public string redirect_uri { get; set; }

        public string post_logout_redirect_uri { get; set; }

        public string response_type { get; set; }

        public string response_mode { get; set; }

        public string scope { get; set; }

        public string OIDCUserKey => $"oidc.user:{authority}:{client_id}";
    }
}
