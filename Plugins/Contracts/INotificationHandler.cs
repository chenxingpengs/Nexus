namespace Nexus.Plugins.Contracts;

public interface INotificationHandler
{
    string[] HandledNotificationTypes { get; }
    Task HandleNotificationAsync(NotificationInfo notification);
}

public record NotificationInfo(
    string Type,
    string Title,
    string Content,
    string? ActionUrl = null,
    Dictionary<string, string>? Metadata = null
);
