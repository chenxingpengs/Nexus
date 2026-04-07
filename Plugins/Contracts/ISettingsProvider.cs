using Avalonia.Controls;

namespace Nexus.Plugins.Contracts;

public interface ISettingsProvider
{
    IEnumerable<SettingsPageRegistration> GetSettingsPages();
}

public record SettingsPageRegistration(
    string PageId,
    string Title,
    Type ViewModelType,
    Control? ViewInstance = null,
    string? Icon = null,
    int Order = 100
);
