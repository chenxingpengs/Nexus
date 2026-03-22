namespace Nexus.Services.Http;

public class RequestOptions
{
    public bool RequireAuth { get; set; } = true;
    
    public int TimeoutSeconds { get; set; } = 30;
    
    public int MaxRetries { get; set; } = 0;
    
    public int RetryDelayMs { get; set; } = 1000;
    
    public bool ShowErrorToast { get; set; } = true;
    
    public bool ShowSuccessToast { get; set; } = false;
    
    public string? OperationName { get; set; }
    
    public static RequestOptions Default => new();
    
    public static RequestOptions NoAuth => new() { RequireAuth = false };
    
    public static RequestOptions WithRetry(int maxRetries = 3, int retryDelayMs = 1000) => new()
    {
        MaxRetries = maxRetries,
        RetryDelayMs = retryDelayMs
    };
    
    public static RequestOptions Silent => new() { ShowErrorToast = false };
}
