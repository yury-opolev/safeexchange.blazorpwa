/// <summary>
/// ExpirationMetadata
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class ExpirationMetadata
    {
        public ExpirationMetadata() { }

        public ExpirationMetadata(ExpirationMetadata source)
        {
            this.ExpireAfterRead = source.ExpireAfterRead;
            this.ScheduleExpiration = source.ScheduleExpiration;
            this.ExpireAt = source.ExpireAt;
        }

        public ExpirationMetadata(ExpirationSettingsOutput source)
        {
            this.ExpireAfterRead = source.ExpireAfterRead;
            this.ScheduleExpiration = source.ScheduleExpiration;
            this.ExpireAt = source.ExpireAt;
        }

        public bool ExpireAfterRead { get; set; }

        public bool ScheduleExpiration { get; set; }

        public DateTime ExpireAt { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is null || obj is not ExpirationMetadata other)
            {
                return false;
            }

            return
                this.ExpireAfterRead == other.ExpireAfterRead &&
                this.ScheduleExpiration == other.ScheduleExpiration &&
                this.ExpireAt == other.ExpireAt;
        }

        public override int GetHashCode()
            => HashCode.Combine(this.ExpireAfterRead, this.ScheduleExpiration, this.ExpireAt);

        public override string? ToString()
            =>  $"ExpireAfterRead: {this.ExpireAfterRead}, ScheduleExpiration: {this.ScheduleExpiration}, ExpireAt: {this.ExpireAt}";

        public ExpirationSettingsInput ToDto() => new ()
        {
            ExpireAfterRead = this.ExpireAfterRead,
            ScheduleExpiration = this.ScheduleExpiration,
            ExpireAt = this.ExpireAt
        };
    }
}
