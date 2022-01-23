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
            this.ScheduleExpiration = source.ScheduleExpiration;
            this.ExpireAt = source.ExpireAt;
            this.ExpireOnIdleTime = source.ExpireOnIdleTime;
            this.IdleTimeToExpire = source.IdleTimeToExpire;
        }

        public ExpirationMetadata(ExpirationSettingsOutput source)
        {
            this.ScheduleExpiration = source.ScheduleExpiration;
            this.ExpireAt = source.ExpireAt;
            this.ExpireOnIdleTime = source.ExpireOnIdleTime;
            this.IdleTimeToExpire = source.IdleTimeToExpire;
        }

        public bool ScheduleExpiration { get; set; }

        public DateTime ExpireAt { get; set; }

        public bool ExpireOnIdleTime { get; set; }

        public TimeSpan IdleTimeToExpire { get; set; }

        public int DaysToExpire
        {
            get => this.IdleTimeToExpire.Days;
            set => this.IdleTimeToExpire = new TimeSpan(value, this.IdleTimeToExpire.Hours, this.IdleTimeToExpire.Minutes, this.IdleTimeToExpire.Seconds);
        }

        public DateTime TimeToExpire
        {
            get => DateTime.MinValue + new TimeSpan(this.IdleTimeToExpire.Hours, this.IdleTimeToExpire.Minutes, this.IdleTimeToExpire.Seconds);
            
            set
            {
                var time = value - DateTime.MinValue;
                this.IdleTimeToExpire = new TimeSpan(this.IdleTimeToExpire.Days, time.Hours, time.Minutes, time.Seconds);
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is null || !(obj is ExpirationMetadata other))
            {
                return false;
            }

            return
                this.ScheduleExpiration == other.ScheduleExpiration &&
                this.ExpireAt == other.ExpireAt &&
                this.ExpireOnIdleTime == other.ExpireOnIdleTime &&
                this.IdleTimeToExpire == other.IdleTimeToExpire;
        }

        public override int GetHashCode()
            => HashCode.Combine(this.ScheduleExpiration, this.ExpireAt, this.ExpireOnIdleTime, this.IdleTimeToExpire);

        public override string? ToString()
            =>  $"ScheduleExpiration: {this.ScheduleExpiration}, ExpireAt: {this.ExpireAt}, ExpireOnIdleTime: {this.ExpireOnIdleTime}, IdleTimeToExpire: {this.IdleTimeToExpire}";

        public ExpirationSettingsInput ToDto() => new ExpirationSettingsInput()
        {
            ScheduleExpiration = this.ScheduleExpiration,
            ExpireAt = this.ExpireAt,
            ExpireOnIdleTime = this.ExpireOnIdleTime,
            IdleTimeToExpire = this.IdleTimeToExpire
        };
    }
}
