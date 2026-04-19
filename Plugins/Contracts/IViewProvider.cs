namespace Nexus.Plugins.Contracts;

public interface IViewProvider
{
    IEnumerable<ViewRegistration> GetViews();
}

public record ViewRegistration(
    string ViewId,
    Type ViewModelType,
    Type? ViewType = null,
    NavigationLocation Location = NavigationLocation.MainContent,
    int Order = 100,
    string? Icon = null,
    string? Title = null
);

public enum NavigationLocation
{
    MainContent,
    Sidebar,
    SettingsPage,
    TrayMenu,
    WidgetArea,
    CustomRegion
}
