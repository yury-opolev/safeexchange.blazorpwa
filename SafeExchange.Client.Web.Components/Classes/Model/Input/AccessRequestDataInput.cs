/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components.Model
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public class AccessRequestDataInput
    {
        [Required]
        [StringLength(63, ErrorMessage = "Name too long (63 character limit).")]
        [RegularExpression(@"^[0-9a-zA-Z-]+$", ErrorMessage = "Only letters, numbers and hyphens are allowed.")]
        public string SecretId { get; set; }

        public string Permission { get; set; }
    }
}
