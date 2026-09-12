/// <summary>
/// SubjectNameValidator
/// </summary>

namespace SafeExchange.Client.Common.Utilities
{
    using System.Text.RegularExpressions;

    /// <summary>
    /// Shape rule for a user subject on the access list, an email-like identifier.
    /// Applied only to rows the user can still act on: a subject the API already accepted
    /// must not be re-judged by this rule, otherwise a row that cannot be corrected on the
    /// form blocks every unrelated change to the secret.
    /// </summary>
    public static class SubjectNameValidator
    {
        public const int MaxLength = 320;

        /// <summary>
        /// The local part carries what a user principal name can carry, '+' included.
        /// The last label is not length capped - '.consulting' and '.technology' are real
        /// top level domains, and the previous {2,4} cap rejected every one of them.
        /// Ends with '\z', not '$': '$' also matches immediately before a trailing newline,
        /// which would accept "user@example.com\n".
        /// </summary>
        public const string Pattern = @"^[\w.+-]+@([\w-]+\.)+[\w-]{2,}\z";

        public const string InvalidMessage = "Email-like identifier required.";

        private static readonly Regex SubjectRegex = new(Pattern, RegexOptions.CultureInvariant);

        public static bool IsValid(string? subjectName)
            => !string.IsNullOrEmpty(subjectName)
                && subjectName.Length <= MaxLength
                && SubjectRegex.IsMatch(subjectName);
    }
}
