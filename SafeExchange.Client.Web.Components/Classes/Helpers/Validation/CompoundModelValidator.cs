/// <summary>
/// CompoundModelValidator
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Common.Utilities;
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text.RegularExpressions;

    public class CompoundModelValidator : ComponentBase
    {
        private ValidationMessageStore messageStore;

        [CascadingParameter]
        private EditContext CurrentEditContext { get; set; }

        /// <summary>
        /// Whether the name is subject to the creation naming contract. The edit form opts out:
        /// the name is immutable there, so revalidating it would block unrelated changes.
        /// </summary>
        [Parameter]
        public bool ValidateObjectName { get; set; } = true;

        protected override void OnInitialized()
        {
            if (CurrentEditContext is null)
            {
                throw new InvalidOperationException(
                    $"{nameof(CompoundModelValidator)} requires a cascading " +
                    $"parameter of type {nameof(EditContext)}. " +
                    $"For example, you can use {nameof(CompoundModelValidator)} " +
                    $"inside an {nameof(EditForm)}.");
            }

            messageStore = new(CurrentEditContext);

            CurrentEditContext.OnValidationRequested += (s, e) =>
            {
                this.Validate();
            };

            CurrentEditContext.OnFieldChanged += (s, e) =>
            {
                this.ValidateField(e.FieldIdentifier);
            };
        }

        private void Validate()
        {
            this.ValidateInternal(force: true);
            this.CurrentEditContext.NotifyValidationStateChanged();
        }

        private void ValidateField(FieldIdentifier fieldIdentifier)
        {
            this.ValidateInternal();
            this.CurrentEditContext.NotifyValidationStateChanged();
        }

        private void ValidateInternal(bool force = false)
        {
            var model = this.CurrentEditContext.Model as CompoundModel;
            if (!this.ValidateObjectName)
            {
                this.messageStore.Clear(() => model.Metadata.ObjectName);
            }
            else if (force || CurrentEditContext.IsModified(() => model.Metadata.ObjectName))
            {
                this.ValidateName(model);
            }

            this.ValidatePermissions(model, force);

            if (force || CurrentEditContext.IsModified(() => model.MainData))
            {
                this.ValidateContent(model);
            }

            if (force || CurrentEditContext.IsModified(() => model.Metadata.ExpirationMetadata.DaysToExpire))
            {
                this.ValidateExpirationIdleDays(model);
            }
        }

        private bool ValidateName(CompoundModel model)
        {
            this.messageStore.Clear(() => model.Metadata.ObjectName);

            var errors = SecretNameValidator.Validate(model.Metadata.ObjectName);
            foreach (var error in errors)
            {
                this.AddErrorMessage(() => model.Metadata.ObjectName, error);
            }

            return errors.Count == 0;
        }

        private void ValidatePermissions(CompoundModel model, bool force = false)
        {
            var accessList = model.Permissions ?? Array.Empty<SubjectPermissions>().ToList();
            foreach (var accessItem in model.Permissions)
            {
                if (!CurrentEditContext.IsModified(() => accessItem.SubjectId) && !force)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(accessItem.SubjectId))
                {
                    continue;
                }

                ValidatePermissionsItem(accessItem);
            }
        }

        private void ValidatePermissionsItem(SubjectPermissions accessItem)
        {
            this.messageStore.Clear(() => accessItem.SubjectName);
            this.messageStore.Clear(() => accessItem.SubjectId);

            if (accessItem.SubjectType.Equals(SubjectType.Application))
            {
                return;
            }

            if (accessItem.SubjectType.Equals(SubjectType.Group))
            {
                return;
            }

            var regex = new Regex(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$");
            if (accessItem.SubjectName.Length > 320 || !regex.IsMatch(accessItem.SubjectName))
            {
                this.AddErrorMessage(() => accessItem.SubjectName, "Email-like identifier required.");
            }
        }

        private bool ValidateContent(CompoundModel model)
        {
            this.messageStore.Clear(() => model.MainData);

            var isValid = true;
            if (string.IsNullOrEmpty(model.MainData))
            {
                this.AddErrorMessage(() => model.MainData, "Content is required.");
                isValid = false;
            }

            if (model.MainData.Length > 10 * 1024 * 1024)
            {
                this.AddErrorMessage(() => model.MainData, "Content is too large (10 Mb limit).");
                isValid = false;
            }

            return isValid;
        }

        private bool ValidateExpirationIdleDays(CompoundModel model)
        {
            this.messageStore.Clear(() => model.Metadata.ExpirationMetadata.DaysToExpire);

            var isValid = true;
            if (model.Metadata.ExpirationMetadata.DaysToExpire < 0)
            {
                this.AddErrorMessage(() => model.Metadata.ExpirationMetadata.DaysToExpire, "Days cannot be negative.");
                isValid = false;
            }

            return isValid;
        }

        private void AddErrorMessage(Expression<Func<object>> accessor, string message)
        {
            this.messageStore.Add(accessor, message);
        }
    }
}
