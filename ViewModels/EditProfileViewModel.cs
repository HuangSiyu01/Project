using System.ComponentModel.DataAnnotations;

namespace Project.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Display name is required.")]
        [MaxLength(100)]
        [Display(Name = "Display Name")]
        public string DisplayName { get; set; }

        [MaxLength(500, ErrorMessage = "Bio cannot exceed 500 characters.")]
        [Display(Name = "Bio")]
        public string? Bio { get; set; }

        [Display(Name = "Avatar")]
        public IFormFile? Avatar { get; set; }
    }
}