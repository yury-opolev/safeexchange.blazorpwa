/// <summary>
/// BaseResponseObject
/// </summary>

namespace SafeExchange.Client.Common.Model
{
    using System;

    public class BaseResponseObject<T> where T : class
    {
        public string Status { get; set; } = string.Empty;

        public T? Result { get; set; }

        public string? Error { get; set; }

        public ResponseStatus ToResponseStatus()
            => new ResponseStatus()
            {
                Status = this.Status,
                Error = this.Error
            };
    }
}

