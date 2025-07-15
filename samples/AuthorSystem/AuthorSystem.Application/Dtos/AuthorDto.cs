using System.Text.Json.Serialization;

namespace AuthorSystem.Application.Dtos;

/// <summary>
/// Data transfer object for author information
/// </summary>
public class AuthorDto
{
    /// <summary>
    /// Unique identifier of the author
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// Author's full name
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Author's email address
    /// </summary>
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Brief biography of the author
    /// </summary>
    [JsonPropertyName("biography")]
    public string Biography { get; set; } = string.Empty;

    /// <summary>
    /// Author's birth date
    /// </summary>
    [JsonPropertyName("birthDate")]
    public DateTime BirthDate { get; set; }

    /// <summary>
    /// Date when the author record was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date when the author record was last updated
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The amount of vote which the author got
    /// </summary>
    [JsonPropertyName("vote")]
    public int Vote { get; set; }
}