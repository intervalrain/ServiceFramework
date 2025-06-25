using JetBrains.Annotations;
using System.ComponentModel.DataAnnotations;

namespace BookStore.Application.Dtos;

public class CreateBookDto
{
    [NotNull]
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;
    
    [NotNull]
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Author { get; set; } = string.Empty;
    
    [NotNull]
    [Required]
    [RegularExpression(@"^(?:\d{10}|\d{13})$", ErrorMessage = "ISBN must be 10 or 13 digits")]
    public string ISBN { get; set; } = string.Empty;
    
    [Range(0.01, 999999.99)]
    public decimal Price { get; set; }
    
    [Range(0, int.MaxValue)]
    public int Stock { get; set; }
}