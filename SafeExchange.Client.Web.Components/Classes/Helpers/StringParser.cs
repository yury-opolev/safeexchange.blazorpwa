/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components
{
    using System;
    using System.Collections.Generic;

    public static class StringParser
    {
        public static bool TryGetGuidList(string guidsCsv, out List<Guid> result)
        {
            result = new List<Guid>();
            if (string.IsNullOrEmpty(guidsCsv))
            {
                return true;
            }

            var chunks = guidsCsv.Split(",");
            foreach (var chunk in chunks)
            {
                if (!Guid.TryParse(chunk, out var guid))
                {
                    return false;
                }

                result.Add(guid);
            }

            return true;
        }
    }
}
