﻿/// <summary>
/// ClientToken
/// </summary>

namespace SafeExchange.Client.Powershell5
{
    using System;
    using System.Collections.Generic;
    using System.Security;

    public class ClientToken
    {
        public IEnumerable<string> Scopes { get; }

        public SecureString Token { get; }

        public DateTimeOffset ExpiresOn { get; }

        public ClientToken(IEnumerable<string> scopes, SecureString token, DateTimeOffset expiresOn)
        {

        }
    }
}