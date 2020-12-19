/// <summary>
/// ...
/// </summary>
/// 
namespace SafeExchange.Client.Web.Components
{
    using System;

    public class UserAuthData
    {
        public string id_token { get; set; }

        public string access_token { get; set; }

        public string refresh_token { get; set; }

        public string token_type { get; set; }

        public string scope { get; set; }

        public int expires_at { get; set; }
    }
}
