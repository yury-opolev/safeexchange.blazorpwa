/// <summary>
/// ...
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class SecretDataInput
    {
        [Required]
        [StringLength(25000, ErrorMessage = "Value too long (25k size limit).")]
        public string Value { get; set; }

        public string ContentType { get; set; }

        public DestroySettings DestroySettings { get; set; }
    }
}
