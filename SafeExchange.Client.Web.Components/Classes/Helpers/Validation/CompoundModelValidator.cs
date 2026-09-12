/// <summary>
/// CompoundModelValidator
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;
    using SafeExchange.Client.Common.Model;
    using System;

    /// <summary>
    /// Wires <see cref="CompoundModelValidation"/> into an <see cref="EditForm"/>.
    /// The rules themselves live in that class, so they can be tested without rendering.
    /// </summary>
    public class CompoundModelValidator : ComponentBase
    {
        private CompoundModelValidation validation;

        [CascadingParameter]
        private EditContext CurrentEditContext { get; set; }

        /// <summary>
        /// Whether the name is subject to the creation naming contract. The edit form opts out:
        /// the name is immutable there, so revalidating it would block unrelated changes.
        /// </summary>
        [Parameter]
        public bool ValidateObjectName { get; set; } = true;

        /// <summary>
        /// Decides, per access list row, whether the row is still the user's to correct.
        /// Left unset, every row is validated, which is what the creation form wants.
        /// </summary>
        [Parameter]
        public Func<SubjectPermissions, int, bool> ShouldValidatePermission { get; set; }

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

            this.validation = new CompoundModelValidation(CurrentEditContext);
            this.ApplyParameters();

            CurrentEditContext.OnValidationRequested += (s, e) =>
            {
                this.validation.ValidateAll();
                CurrentEditContext.NotifyValidationStateChanged();
            };

            CurrentEditContext.OnFieldChanged += (s, e) =>
            {
                this.validation.ValidateModified();
                CurrentEditContext.NotifyValidationStateChanged();
            };
        }

        protected override void OnParametersSet()
        {
            this.ApplyParameters();
        }

        private void ApplyParameters()
        {
            if (this.validation is null)
            {
                return;
            }

            this.validation.ValidateObjectName = this.ValidateObjectName;
            this.validation.ShouldValidatePermission = this.ShouldValidatePermission;
        }
    }
}
