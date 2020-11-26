﻿/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Model
{
    using System;
    using System.Collections.Generic;

    public class ApiSecretsListReply
    {
        public string Status { get; set; }

        public List<SecretDescriptionData> Secrets { get; set; }

        public string Error { get; set; }
    }
}
