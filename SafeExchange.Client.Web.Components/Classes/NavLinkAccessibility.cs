/// <summary>
/// ..
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Rendering;
    using Microsoft.AspNetCore.Components.Routing;
    using System.Collections.Generic;

    public class NavLinkAccessibility : NavLink
    {
        /// <summary>
        /// Gets or sets the attributes applied to the NavLink when the
        /// current route matches the NavLink href.
        /// </summary>
        [Parameter]
        public IReadOnlyDictionary<string, object>? ActiveAttributes { get; set; }

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "a");

            builder.AddMultipleAttributes(1, AdditionalAttributes);
            builder.AddAttribute(2, "class", CssClass);

            if (CssClass?.Contains("active") == true && ((ActiveAttributes?.Count ?? 0) > 0))
            {
                builder.AddMultipleAttributes(3, ActiveAttributes);
                builder.AddContent(4, ChildContent);
            }
            else
            {
                builder.AddContent(3, ChildContent);
            }

            builder.CloseElement();
        }
    }
}
