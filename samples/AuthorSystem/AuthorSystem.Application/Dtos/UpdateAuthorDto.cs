using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using JetBrains.Annotations;

namespace AuthorSystem.Application.Dtos;

/// <summary>
/// Data transfer object for updating an existing author
/// </summary>
public class UpdateAuthorDto
{
    [Required]
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Author's full name
    /// </summary>
    [NotNull]
    [Required]
    [StringLength(100, MinimumLength = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Author's email address
    /// </summary>
    [NotNull]
    [Required]
    [EmailAddress]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Brief biography of the author
    /// </summary>
    [StringLength(1000)]
    [JsonPropertyName("biography")]
    public string Biography { get; set; } = string.Empty;

    /// <summary>
    /// Author's birth date
    /// </summary>
    [Required]
    [JsonPropertyName("birthDate")]
    public DateTime BirthDate { get; set; }
}