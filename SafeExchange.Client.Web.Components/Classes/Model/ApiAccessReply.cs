﻿/// <summary>
/// ...
/// </summary>
namespace SafeExchange.Client.Web.Components.Model
{
    using System;
    using System.Collections.Generic;

    public class ApiAccessReply
    {
        public string Status { get; set; }

        public List<PermissionsData> AccessList { get; set; }

        public string Error { get; set; }
    }
}
