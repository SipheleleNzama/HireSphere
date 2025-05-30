using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;


namespace HireSphere.Models.ViewModels
{
    public class UserViewModel
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public IList<string>? Roles { get; set; }
    }

    public class EditUserViewModel
    {
        public string? Id { get; set; }

        [Required]
        [EmailAddress]
        public string? Email { get; set; }

        [Required]
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Required]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        public List<RoleSelectionViewModel>? Roles { get; set; }
    }

    public class RoleSelectionViewModel
    {
        public string? RoleId { get; set; }
        public string? RoleName { get; set; }
        public bool IsSelected { get; set; }
    }
}
