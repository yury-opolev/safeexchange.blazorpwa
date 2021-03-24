﻿/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;

    public class AccessRequestData
    {
        public string RequestId { get; set; }

        public string UserName { get; set; }

        public string SecretName { get; set; }

        public string Permissions { get; set; }

        public AccessRequestType RequestType { get; set; }

        public DateTime RequestedAt { get; set; }
    }
}