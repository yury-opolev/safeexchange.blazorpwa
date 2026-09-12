/// <summary>
/// CompoundModelValidation
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components.Forms;
    using SafeExchange.Client.Common.Model;
    using SafeExchange.Client.Common.Utilities;
    using System;

    /// <summary>
    /// The form rules for a <see cref="CompoundModel"/>, kept apart from the component that
    /// hosts them so they can be exercised against a real <see cref="EditContext"/> directly.
    /// Every message this produces must have a rendered ValidationMessage on the page: a
    /// message with nowhere to render still keeps <see cref="EditContext.Validate"/> false,
    /// and the form then refuses to submit without showing the user anything.
    /// </summary>
    public sealed class CompoundModelValidation
    {
        public const int MaxContentLength = 10 * 1024 * 1024;

        public const string ContentRequiredMessage = "Content is required.";

        public const string ContentTooLargeMessage = "Content is too large (10 Mb limit).";

        public const string NegativeIdleDaysMessage = "Days cannot be negative.";

        private readonly EditContext editContext;

        private readonly ValidationMessageStore messageStore;

        public CompoundModelValidation(EditContext editContext)
        {
            this.editContext = editContext ?? throw new ArgumentNullException(nameof(editContext));
            this.messageStore = new ValidationMessageStore(editContext);
        }

        /// <summary>
        /// Whether the name is subject to the creation naming contract. The edit form opts out:
        /// the name is immutable there, so revalidating it would block unrelated changes.
        /// </summary>
        public bool ValidateObjectName { get; set; } = true;

        /// <summary>
        /// Decides, per access list row, whether the row is still the user's to correct.
        /// Rows the form renders read only and rows already marked for deletion are not,
        /// so their subject is left alone. Null validates every row.
        /// </summary>
        public Func<SubjectPermissions, int, bool> ShouldValidatePermission { get; set; }

        /// <summary>
        /// Full pass, for submit. Re-derives every message from scratch.
        /// </summary>
        public void ValidateAll() => this.Run(force: true);

        /// <summary>
        /// Touched fields only, for feedback while the user types.
        /// </summary>
        public void ValidateModified() => this.Run(force: false);

        private void Run(bool force)
        {
            if (this.editContext.Model is not CompoundModel model || model.Metadata is null)
            {
                return;
            }

            if (force)
            {
                // A full pass re-derives every message, so it starts from an empty store.
                // This is what drops a message whose field is gone: removing an access list
                // row leaves its FieldIdentifier behind in the store, and such an orphan
                // keeps Validate() false forever while having nowhere on the page to render.
                this.messageStore.Clear();
            }

            if (!this.ValidateObjectName)
            {
                this.messageStore.Clear(() => model.Metadata.ObjectName);
            }
            else if (force || this.editContext.IsModified(() => model.Metadata.ObjectName))
            {
                this.ValidateName(model);
            }

            this.ValidatePermissions(model, force);

            if (force || this.editContext.IsModified(() => model.MainData))
            {
                this.ValidateContent(model);
            }

            if (model.Metadata.ExpirationMetadata is not null
                && (force || this.editContext.IsModified(() => model.Metadata.ExpirationMetadata.DaysToExpire)))
            {
                this.ValidateExpirationIdleDays(model);
            }
        }

        private void ValidateName(CompoundModel model)
        {
            this.messageStore.Clear(() => model.Metadata.ObjectName);

            foreach (var error in SecretNameValidator.Validate(model.Metadata.ObjectName))
            {
                this.messageStore.Add(() => model.Metadata.ObjectName, error);
            }
        }

        private void ValidatePermissions(CompoundModel model, bool force)
        {
            var accessList = model.Permissions;
            if (accessList is null)
            {
                return;
            }

            for (var index = 0; index < accessList.Count; index++)
            {
                var accessItem = accessList[index];
                if (accessItem is null)
                {
                    continue;
                }

                if (this.ShouldValidatePermission is not null && !this.ShouldValidatePermission(accessItem, index))
                {
                    continue;
                }

                if (!force && !this.editContext.IsModified(() => accessItem.SubjectId))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(accessItem.SubjectId))
                {
                    continue;
                }

                this.ValidatePermissionsItem(accessItem);
            }
        }

        private void ValidatePermissionsItem(SubjectPermissions accessItem)
        {
            this.messageStore.Clear(() => accessItem.SubjectName);
            this.messageStore.Clear(() => accessItem.SubjectId);

            if (accessItem.SubjectType.Equals(SubjectType.Application)
                || accessItem.SubjectType.Equals(SubjectType.Group))
            {
                return;
            }

            if (!SubjectNameValidator.IsValid(accessItem.SubjectName))
            {
                this.messageStore.Add(() => accessItem.SubjectName, SubjectNameValidator.InvalidMessage);
            }
        }

        private void ValidateContent(CompoundModel model)
        {
            this.messageStore.Clear(() => model.MainData);

            var mainData = model.MainData;
            if (string.IsNullOrEmpty(mainData))
            {
                // A missing value has no length to measure, so this returns rather than
                // falling through to the size rule and dereferencing null.
                this.messageStore.Add(() => model.MainData, ContentRequiredMessage);
                return;
            }

            if (mainData.Length > MaxContentLength)
            {
                this.messageStore.Add(() => model.MainData, ContentTooLargeMessage);
            }
        }

        private void ValidateExpirationIdleDays(CompoundModel model)
        {
            this.messageStore.Clear(() => model.Metadata.ExpirationMetadata.DaysToExpire);

            if (model.Metadata.ExpirationMetadata.DaysToExpire < 0)
            {
                this.messageStore.Add(() => model.Metadata.ExpirationMetadata.DaysToExpire, NegativeIdleDaysMessage);
            }
        }
    }
}
