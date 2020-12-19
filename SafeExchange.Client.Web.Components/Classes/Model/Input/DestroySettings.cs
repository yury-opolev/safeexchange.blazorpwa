/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class DestroySettings
    {
        public bool DestroyAfterRead { get; set; }

        public bool ScheduleDestroy { get; set; }

        public DateTime DestroyAt { get; set; }
    }
}
