/// <summary>
/// ...
/// </summary>

namespace SafeExchange.BlazorPWA.Model
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    public class SecretInput
    {
        [Required]
        [StringLength(63, ErrorMessage = "Name too long (63 character limit).")]
        [RegularExpression(@"^[0-9a-zA-Z-]+$", ErrorMessage = "Only letters, numbers and hyphens are allowed.")]
        public string Name { get; set; }

        public SecretDataInput Data { get; set; }

        public List<AccessDataInput> AccessList { get; set; }
    }
}
