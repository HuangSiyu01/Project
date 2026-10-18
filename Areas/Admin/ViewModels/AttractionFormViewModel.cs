using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Project.Areas.Admin.ViewModels
{
    public class AttractionFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter the attraction name.")]
        [StringLength(120)]
        [Display(Name = "Attraction Name")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the city or location.")]
        [StringLength(100)]
        [Display(Name = "City / Location")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter an attraction description.")]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "10000.00", ErrorMessage = "Ticket price must be between NZ$0.01 and NZ$10,000.00.")]
        [Display(Name = "Ticket Price (NZD)")]
        public decimal TicketPrice { get; set; }

        [Display(Name = "Available for purchase")]
        public bool IsAvailable { get; set; } = true;

        [Display(Name = "Main Image")]
        public IFormFile? MainImage { get; set; }

        [Display(Name = "Additional Images")]
        public List<IFormFile> AdditionalImages { get; set; } = new();

        public List<int> RemoveImageIds { get; set; } = new();

        public List<ExistingAttractionImageViewModel> ExistingImages { get; set; } = new();
        public string? CurrentMainImageUrl { get; set; }
    }

    public class ExistingAttractionImageViewModel
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}

