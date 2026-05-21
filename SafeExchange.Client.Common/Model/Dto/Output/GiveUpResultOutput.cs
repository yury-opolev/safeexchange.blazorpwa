/// <summary>
/// GiveUpResultOutput — body of DELETE /v2/access-giveup/{secretId}. Reports what
/// actually happened: whether the caller had a row to release and whether the
/// secret got scheduled for grace-period purge as a result.
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class GiveUpResultOutput
    {
        public bool HadDirectRow { get; set; }

        public bool WasOrphaned { get; set; }

        public DateTime? ExpireAt { get; set; }
    }
}
