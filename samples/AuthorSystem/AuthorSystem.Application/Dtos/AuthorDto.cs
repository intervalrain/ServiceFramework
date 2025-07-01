namespace AuthorSystem.Application.Dtos;

/// <summary>
/// Data transfer object for author information
/// </summary>
public class AuthorDto
{
    /// <summary>
    /// Unique identifier of the author
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Author's full name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Author's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Brief biography of the author
    /// </summary>
    public string Biography { get; set; } = string.Empty;

    /// <summary>
    /// Author's birth date
    /// </summary>
    public DateTime BirthDate { get; set; }

    /// <summary>
    /// Date when the author record was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date when the author record was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The amount of vote which the author got
    /// </summary>
    public int Vote { get; set; }
}