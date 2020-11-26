/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA
{
    using System;

    public class StateContainer
    {
        public string CurrentPageHeader { get; private set; }

        public event Action OnChange;

        public void SetCurrentPageHeader(string pageHeader)
        {
            CurrentPageHeader = pageHeader;
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
