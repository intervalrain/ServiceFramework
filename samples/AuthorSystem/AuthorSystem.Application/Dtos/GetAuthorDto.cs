using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using JetBrains.Annotations;

using Newtonsoft.Json;

namespace AuthorSystem.Application.Dtos;

public class GetAuthorDto
{
    /// <summary>
    /// Unique identifier of the author
    /// </summary>
    [NotNull]
    [Required]
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
}
