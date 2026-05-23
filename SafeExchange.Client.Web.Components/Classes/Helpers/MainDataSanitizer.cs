/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using Ganss.Xss;

// Client-side sanitization is applied on WRITE (CreateData/EditData) as a
// defense-in-depth layer alongside the backend's own upload-time sanitize
// (SafeExchangeSecretStream.UploadMainContentAsync). It is intentionally NOT
// used on the read/view path: ViewData renders the already-sanitized stored
// content directly, because re-parsing multi-MB inline-image HTML on the
// single WASM UI thread on every view was prohibitively slow.
public static class MainDataSanitizer
{
    private static readonly HtmlSanitizer sanitizer = CreateSanitizer();

    public static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return sanitizer.Sanitize(input);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var instance = new HtmlSanitizer();
        instance.AllowedSchemes.Add("data");
        instance.AllowedAttributes.Add("class");
        instance.AllowedAttributes.Add("data-value");
        instance.AllowedAttributes.Add("data-bs-toggle");
        instance.AllowedAttributes.Add("data-bs-placement");
        instance.AllowedAttributes.Add("title");
        return instance;
    }
}
