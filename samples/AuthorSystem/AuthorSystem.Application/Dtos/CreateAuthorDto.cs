using System.ComponentModel.DataAnnotations;

using JetBrains.Annotations;

namespace AuthorSystem.Application.Dtos;

public class CreateAuthorDto
{
    [NotNull]
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    [NotNull]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string Biography { get; set; } = string.Empty;
    
    [Required]
    public DateTime BirthDate { get; set; }
}