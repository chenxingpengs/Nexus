using System.Text.Json.Serialization;

namespace Nexus.Models;

public class ApiResponse<T>
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;
    
    [JsonPropertyName("data")]
    public T? Data { get; set; }
    
    [JsonIgnore]
    public bool IsSuccess => Code == 200;
}

public class EmptyApiResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }
    
    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;
    
    [JsonIgnore]
    public bool IsSuccess => Code == 200;
}
