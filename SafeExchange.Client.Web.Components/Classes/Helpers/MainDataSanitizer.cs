/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using Ganss.Xss;

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
