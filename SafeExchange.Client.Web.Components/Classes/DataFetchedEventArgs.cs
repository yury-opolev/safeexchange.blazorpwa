/// <summary>
/// DataFetchedEventArgs
/// </summary>

namespace SafeExchange.Client.Web.Components.Classes
{
    using SafeExchange.Client.Common.Model;
    using System;

    public class DataFetchedEventArgs : EventArgs
    {
        /// <summary>
        /// Data fetch response status.
        /// </summary>
        public ResponseStatus ResponseStatus { get; set; }
    }
}
