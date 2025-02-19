/// <summary>
/// GraphGroupOutput
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class GraphGroupOutput : IEquatable<GraphGroupOutput>
    {
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string? Mail { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is not GraphGroupOutput groupObj)
            {
                return false;
            }

            return this.Equals(groupObj);
        }

        public bool Equals(GraphGroupOutput? other)
        {
            if (other == default)
            {
                return false;
            }

            return this.Id == other.Id;
        }
    }
}
