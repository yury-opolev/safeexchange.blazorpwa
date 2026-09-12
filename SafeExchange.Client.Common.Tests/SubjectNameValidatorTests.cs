/// <summary>
/// SubjectNameValidatorTests
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common.Utilities;

    /// <summary>
    /// Shape rule for a user subject on the access list. The API does not re-check this,
    /// so the rule exists purely to catch typos before the request is sent - it must never
    /// be stricter than what the service accepts, or a legitimate grantee becomes unaddable.
    /// </summary>
    [TestFixture]
    public class SubjectNameValidatorTests
    {
        [TestCase("user@example.com", "plain address")]
        [TestCase("first.last@example.com", "dotted local part")]
        [TestCase("first+label@example.com", "plus addressing")]
        [TestCase("first-last@example.com", "hyphen in local part")]
        [TestCase("user_name@example.com", "underscore in local part")]
        [TestCase("user@sub.example.com", "subdomain")]
        [TestCase("user@example.co.uk", "two label suffix")]
        [TestCase("user@example.online", "six character top level domain")]
        [TestCase("user@example.consulting", "ten character top level domain")]
        [TestCase("user@example.technology", "long top level domain")]
        [TestCase("user@my-company.com", "hyphen in domain")]
        public void ValidSubjectsAreAccepted(string input, string _why)
        {
            Assert.That(SubjectNameValidator.IsValid(input), Is.True);
        }

        [TestCase("", "empty")]
        [TestCase(null, "missing")]
        [TestCase("user", "no domain")]
        [TestCase("user@", "no domain part")]
        [TestCase("@example.com", "no local part")]
        [TestCase("user@example", "no dot in domain")]
        [TestCase("user@example.c", "single character top level domain")]
        [TestCase("user name@example.com", "space")]
        [TestCase("user@exa mple.com", "space in domain")]
        [TestCase("user@example.com\n", "trailing newline")]
        [TestCase("user@example.com\r", "trailing carriage return")]
        [TestCase("user\n@example.com", "interior newline")]
        public void InvalidSubjectsAreRejected(string? input, string _why)
        {
            Assert.That(SubjectNameValidator.IsValid(input), Is.False);
        }

        [Test]
        public void SubjectAtTheLengthLimitIsAccepted()
        {
            var domain = "@example.com";
            var local = new string('a', SubjectNameValidator.MaxLength - domain.Length);

            var subject = local + domain;

            Assert.That(subject, Has.Length.EqualTo(SubjectNameValidator.MaxLength));
            Assert.That(SubjectNameValidator.IsValid(subject), Is.True);
        }

        [Test]
        public void SubjectOverTheLengthLimitIsRejected()
        {
            var domain = "@example.com";
            var local = new string('a', SubjectNameValidator.MaxLength - domain.Length + 1);

            Assert.That(SubjectNameValidator.IsValid(local + domain), Is.False);
        }
    }
}
