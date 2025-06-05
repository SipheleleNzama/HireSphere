using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;

namespace HireSphere.Models.ViewModels
{
    public class LoginViewModel
    {
            [Required]
            [EmailAddress]
            public string? Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string? Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        
            // For return URL functionality 
            public string? ReturnUrl { get; set; }
            // For External logins 
            public IList<AuthenticationScheme>? ExternalLogins { get; set; }
    }
}