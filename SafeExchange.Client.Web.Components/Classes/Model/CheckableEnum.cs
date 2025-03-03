/// CheckableEnum

namespace SafeExchange.Client.Web.Components.Classes.Model
{
    using System;

    public class CheckableEnum<T> where T : struct, Enum
    {
        public bool IsSelected { get; set; }

        public T Value { get; set; }
    }
}
