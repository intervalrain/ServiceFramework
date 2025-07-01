using System.ComponentModel.DataAnnotations;

using JetBrains.Annotations;

namespace AuthorSystem.Application.Dtos;

/// <summary>
/// Data transfer object for creating a new author
/// </summary>
public class CreateAuthorDto
{
    /// <summary>
    /// Author's full name
    /// </summary>
    [NotNull]
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Author's email address
    /// </summary>
    [NotNull]
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    /// <summary>
    /// Brief biography of the author
    /// </summary>
    [StringLength(1000)]
    public string Biography { get; set; } = string.Empty;
    
    /// <summary>
    /// Author's birth date
    /// </summary>
    [Required]
    public DateTime BirthDate { get; set; }
}