using System.Text.Json.Serialization;

namespace Nexus.Models;

public class ProfanityCheckResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    
    [JsonPropertyName("original_text")]
    public string OriginalText { get; set; } = string.Empty;
    
    [JsonPropertyName("masked_text")]
    public string MaskedText { get; set; } = string.Empty;
    
    [JsonPropertyName("forbidden_words")]
    public List<string> ForbiddenWords { get; set; } = new();
    
    [JsonIgnore]
    public bool HasForbiddenWords => Status == "forbidden" && ForbiddenWords.Count > 0;
}
