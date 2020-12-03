/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Helpers
{
    using System.Security.Claims;

    public class TokenHandler
    {
        public static string GetName(ClaimsPrincipal principal)
        {
            var result = principal.FindFirst("preferred_username")?.Value;

            if (string.IsNullOrEmpty(result))
            {
                result = principal.FindFirst(ClaimTypes.Upn)?.Value;
            }
            if (string.IsNullOrEmpty(result))
            {
                result = principal.FindFirst("upn")?.Value;
            }

            return result;
        }
    }
}
