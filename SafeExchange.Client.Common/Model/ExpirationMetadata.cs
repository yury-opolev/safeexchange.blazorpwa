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
            var expireAt = source.ExpireAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(source.ExpireAt, DateTimeKind.Utc)
                : source.ExpireAt;

            this.ScheduleExpiration = source.ScheduleExpiration;
            this.ExpireAt = expireAt.ToLocalTime();
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

        public ExpirationSettingsInput ToDto()
        {
            var expireAt = this.ExpireAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(this.ExpireAt, DateTimeKind.Local)
                : this.ExpireAt;

            return new ExpirationSettingsInput()
            {
                ScheduleExpiration = this.ScheduleExpiration,
                ExpireAt = expireAt.ToUniversalTime(),
                ExpireOnIdleTime = this.ExpireOnIdleTime,
                IdleTimeToExpire = this.IdleTimeToExpire
            };
        }
    }
}
