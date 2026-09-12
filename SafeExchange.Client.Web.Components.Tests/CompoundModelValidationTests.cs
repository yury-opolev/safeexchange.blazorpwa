/// <summary>
/// CompoundModelValidationTests
/// </summary>

namespace SafeExchange.Client.Web.Components.Tests
{
    using Microsoft.AspNetCore.Components.Forms;
    using NUnit.Framework;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Web.Components;

    /// <summary>
    /// These pin the failure mode the edit form kept hitting: a validation message that
    /// nothing on the page can render still holds Validate() false, so the submit button
    /// silently does nothing. Every case here is about a message that must not exist.
    /// </summary>
    [TestFixture]
    public class CompoundModelValidationTests
    {
        private static CompoundModel CreateModel(params SubjectPermissions[] permissions)
            => new CompoundModel
            {
                Metadata = new ObjectMetadata
                {
                    ObjectName = "Legacy_Secret",
                    Content = new List<ContentMetadata>(),
                    ExpirationMetadata = new ExpirationMetadata()
                },
                Permissions = new List<SubjectPermissions>(permissions),
                MainData = "content"
            };

        private static SubjectPermissions User(string subjectName)
            => new SubjectPermissions
            {
                ObjectName = string.Empty,
                SubjectType = SubjectType.User,
                SubjectName = subjectName,
                SubjectId = subjectName
            };

        /// <summary>
        /// Wires the validation the same way the component does, so Validate() behaves
        /// exactly as it does behind the form's submit.
        /// </summary>
        private static (EditContext Context, CompoundModelValidation Validation) Bind(CompoundModel model)
        {
            var editContext = new EditContext(model);
            var validation = new CompoundModelValidation(editContext);
            editContext.OnValidationRequested += (s, e) => validation.ValidateAll();
            return (editContext, validation);
        }

        [Test]
        public void RemovingAnInvalidAccessRowUnblocksTheSubmit()
        {
            // A message is keyed by the object it was written for. Dropping the row takes the
            // field off the page but used to leave the message in the store, where it blocked
            // every later submit with nothing on screen to explain why.
            var model = CreateModel();
            var (editContext, _) = Bind(model);

            var invalidRow = User("not-an-email");
            model.Permissions.Add(invalidRow);

            Assert.That(editContext.Validate(), Is.False, "an invalid access row must block the submit");

            model.Permissions.Remove(invalidRow);

            Assert.That(editContext.Validate(), Is.True, "removing the row must drop the message it left behind");
            Assert.That(editContext.GetValidationMessages(), Is.Empty);
        }

        [Test]
        public void RowsTheUserCannotEditAreNotValidated()
        {
            // Subjects the API already accepted are read only on the edit form. Judging them
            // by this rule blocks the submit on a field nobody can correct.
            var model = CreateModel(User("legacy-service-account"), User("new.user@example.com"));
            var (editContext, validation) = Bind(model);

            validation.ShouldValidatePermission = (_, index) => index >= 1;

            Assert.That(editContext.Validate(), Is.True);
            Assert.That(editContext.GetValidationMessages(), Is.Empty);
        }

        [Test]
        public void EditableRowsAreStillValidated()
        {
            var model = CreateModel(User("legacy-service-account"), User("also-not-an-email"));
            var (editContext, validation) = Bind(model);

            validation.ShouldValidatePermission = (_, index) => index >= 1;

            Assert.That(editContext.Validate(), Is.False);
        }

        [Test]
        public void SubjectWithLongTopLevelDomainIsAccepted()
        {
            var model = CreateModel(User("user@example.consulting"));
            var (editContext, _) = Bind(model);

            Assert.That(editContext.Validate(), Is.True);
        }

        [TestCase(SubjectType.Group)]
        [TestCase(SubjectType.Application)]
        public void NonUserSubjectsAreNotShapeChecked(SubjectType subjectType)
        {
            var model = CreateModel(new SubjectPermissions
            {
                ObjectName = string.Empty,
                SubjectType = subjectType,
                SubjectName = "Some Display Name",
                SubjectId = "some-id"
            });

            var (editContext, _) = Bind(model);

            Assert.That(editContext.Validate(), Is.True);
        }

        [Test]
        public void ObjectNameIsNotValidatedWhenTheFormOptsOut()
        {
            // The edit form has no name input at all, so a name message there is unrenderable
            // by construction. The name is immutable anyway.
            var model = CreateModel();
            model.Metadata.ObjectName = "a name the creation rule rejects";

            var (editContext, validation) = Bind(model);
            validation.ValidateObjectName = false;

            Assert.That(editContext.Validate(), Is.True);
            Assert.That(editContext.GetValidationMessages(), Is.Empty);
        }

        [Test]
        public void ObjectNameIsValidatedWhenTheFormOwnsIt()
        {
            var model = CreateModel();
            model.Metadata.ObjectName = "a name the creation rule rejects";

            var (editContext, _) = Bind(model);

            Assert.That(editContext.Validate(), Is.False);
        }

        [Test]
        public void UnderscoreInTheNameIsAccepted()
        {
            var model = CreateModel();
            model.Metadata.ObjectName = "Legacy_Secret";

            var (editContext, _) = Bind(model);

            Assert.That(editContext.Validate(), Is.True);
        }

        [Test]
        public void MissingContentIsReportedRatherThanThrowing()
        {
            var model = CreateModel();
            model.MainData = null!;

            var (editContext, _) = Bind(model);

            Assert.That(editContext.Validate(), Is.False);
            Assert.That(
                editContext.GetValidationMessages(),
                Does.Contain(CompoundModelValidation.ContentRequiredMessage));
        }

        [Test]
        public void NegativeIdleDaysAreReported()
        {
            var model = CreateModel();
            model.Metadata.ExpirationMetadata.DaysToExpire = -1;

            var (editContext, _) = Bind(model);

            Assert.That(editContext.Validate(), Is.False);
            Assert.That(
                editContext.GetValidationMessages(),
                Does.Contain(CompoundModelValidation.NegativeIdleDaysMessage));
        }

        [Test]
        public void CorrectingAFieldClearsItsEarlierMessage()
        {
            var model = CreateModel(User("not-an-email"));
            var (editContext, _) = Bind(model);

            Assert.That(editContext.Validate(), Is.False);

            model.Permissions[0].SubjectName = "user@example.com";
            model.Permissions[0].SubjectId = "user@example.com";

            Assert.That(editContext.Validate(), Is.True);
        }
    }
}
