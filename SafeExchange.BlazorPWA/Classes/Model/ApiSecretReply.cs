﻿/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Model
{
    using System;

    public class ApiSecretReply
    {
        public string Status { get; set; }

        public SecretData Result { get; set; }

        public string Error { get; set; }
    }
}
