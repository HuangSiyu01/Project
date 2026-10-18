using System.ComponentModel.DataAnnotations;

namespace Project.Dtos
{
    /// <summary>
    /// Identifies the attraction rows selected in the customer's saved cart.
    /// Prices and quantities are always re-read and validated by the server.
    /// </summary>
    public class CreateCheckoutSessionRequest
    {
        [Required]
        [MinLength(1, ErrorMessage = "Select at least one attraction to checkout.")]
        public List<int> SelectedAttractionIds { get; set; } = new();
    }
}
