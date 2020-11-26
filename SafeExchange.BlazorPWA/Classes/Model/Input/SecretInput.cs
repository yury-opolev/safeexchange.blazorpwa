/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Model
{
    using System;
    using System.Collections.Generic;

    public class SecretInput
    {
        public string Name { get; set; }

        public SecretDataInput Data { get; set; }

        public List<AccessDataInput> AccessList { get; set; }
    }
}
