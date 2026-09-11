/// <summary>
/// SecretNameValidator
/// </summary>

namespace SafeExchange.Client.Common.Utilities
{
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Naming contract for new secrets, mirrored by SafeExchange.Core.Utilities.SecretNameValidator.
    /// Applied on creation only - a secret name is immutable, so existing names are never revalidated.
    /// </summary>
    public static class SecretNameValidator
    {
        public const int MaxLength = 100;

        public const string Pattern = @"^[0-9a-zA-Z_-]+\z";

        public const string RequiredMessage = "Name is required.";

        public const string TooLongMessage = "Name is too long (100 character limit).";

        public const string PatternMessage = "Only letters, numbers, hyphens and underscores are allowed.";

        private static readonly Regex NameRegex = new(Pattern, RegexOptions.CultureInvariant);

        /// <summary>
        /// Every rule the name violates, empty when valid. A missing name short-circuits:
        /// the length and pattern rules cannot be evaluated for it.
        /// </summary>
        public static IReadOnlyList<string> Validate(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return new[] { RequiredMessage };
            }

            var errors = new List<string>();
            if (name.Length > MaxLength)
            {
                errors.Add(TooLongMessage);
            }

            if (!NameRegex.IsMatch(name))
            {
                errors.Add(PatternMessage);
            }

            return errors;
        }

        public static bool IsValid(string? name) => Validate(name).Count == 0;
    }
}
