// SearchDialogItemHolder

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class SearchDialogItemHolder<TItem> where TItem: class
    {
        public SearchDialogItemHolder(TItem item)
            : this(item, false, false)
        {
        }

        public SearchDialogItemHolder(TItem item, bool isPinned)
            : this(item, isPinned, false)
        {
        }

        public SearchDialogItemHolder(TItem item, bool isPinned, bool isInProgress)
        {
            this.Item = item ?? throw new ArgumentNullException(nameof(item));
            this.IsPinned = isPinned;
            this.IsInProgress = isInProgress;
        }

        public TItem Item { get; set; }

        public bool IsPinned { get; set; }

        public bool IsInProgress { get; set; }
    }
}
