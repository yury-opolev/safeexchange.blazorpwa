/// <summary>
/// SecureStringExtensions
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using System.Net;
    using System.Security;

    public static class SecureStringExtensions
    {
        public static SecureString ToSecureString(this string input)
        {
            return new NetworkCredential(string.Empty, input).SecurePassword;
        }

        public static string ToUnsecureString(this SecureString input)
        {
            return new NetworkCredential(string.Empty, input).Password;
        }
    }
}
