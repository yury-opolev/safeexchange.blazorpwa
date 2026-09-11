/// <summary>
/// SecretNameValidatorTests
/// </summary>

namespace SafeExchange.Client.Common.Tests
{
    using NUnit.Framework;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Common.Utilities;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// The creation naming contract. These cases are mirrored verbatim by the API's
    /// SecretNameValidatorTests (safeexchange): if one side changes, the other must change
    /// with it, otherwise a name accepted by one client is rejected by the other.
    /// </summary>
    [TestFixture]
    public class SecretNameValidatorTests
    {
        [TestCase("LegacySecret", "letters")]
        [TestCase("Legacy_Secret", "underscore")]
        [TestCase("Legacy-Secret-123", "hyphen and digits")]
        [TestCase("Legacy_Secret-123", "underscore and hyphen")]
        [TestCase("_leading-underscore", "underscore may lead")]
        [TestCase("-leading-hyphen", "hyphen may lead")]
        [TestCase("12345", "digits only")]
        public void ValidNamesAreAccepted(string input, string _why)
        {
            Assert.That(SecretNameValidator.Validate(input), Is.Empty);
            Assert.That(SecretNameValidator.IsValid(input), Is.True);
        }

        [TestCase("", "empty")]
        [TestCase(null, "missing")]
        [TestCase("has space", "space")]
        [TestCase("has/slash", "slash")]
        [TestCase("has.dot", "dot")]
        [TestCase("has:colon", "colon")]
        [TestCase("emoji\U0001F600", "non-ascii")]
        [TestCase("Legacy_Secret\n", "trailing newline")]
        [TestCase("Legacy_Secret\r", "trailing carriage return")]
        [TestCase("Legacy\nSecret", "interior newline")]
        public void InvalidNamesAreRejected(string? input, string _why)
        {
            Assert.That(SecretNameValidator.Validate(input), Is.Not.Empty);
            Assert.That(SecretNameValidator.IsValid(input), Is.False);
        }

        [Test]
        public void MissingNameReportsOnlyTheRequiredRule()
        {
            // The length and pattern rules cannot be evaluated for a missing name.
            Assert.That(SecretNameValidator.Validate(null), Is.EqualTo(new[] { SecretNameValidator.RequiredMessage }));
            Assert.That(SecretNameValidator.Validate(string.Empty), Is.EqualTo(new[] { SecretNameValidator.RequiredMessage }));
        }

        [Test]
        public void ExactlyMaxLengthIsAccepted()
        {
            var name = new string('a', SecretNameValidator.MaxLength);

            Assert.That(SecretNameValidator.Validate(name), Is.Empty);
        }

        [Test]
        public void OverMaxLengthIsRejected()
        {
            var name = new string('a', SecretNameValidator.MaxLength + 1);

            Assert.That(SecretNameValidator.Validate(name), Does.Contain(SecretNameValidator.TooLongMessage));
        }

        [Test]
        public void ObjectMetadataAnnotationsAgreeWithTheValidator()
        {
            // The model annotations and the form validator must not drift apart: the edit
            // form only skips the name rule, it does not get a different one.
            foreach (var name in new[] { "Legacy_Secret", "Legacy-Secret-123", "has space", "has/slash", "Legacy_Secret\n", string.Empty })
            {
                var metadata = new ObjectMetadata { ObjectName = name };
                var results = new List<ValidationResult>();
                var annotationsValid = Validator.TryValidateObject(
                    metadata, new ValidationContext(metadata), results, validateAllProperties: true);

                Assert.That(
                    annotationsValid,
                    Is.EqualTo(SecretNameValidator.IsValid(name)),
                    $"Annotations and validator disagree on '{name}'.");
            }
        }
    }
}
