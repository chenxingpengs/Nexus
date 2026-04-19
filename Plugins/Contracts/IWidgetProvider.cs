namespace Nexus.Plugins.Contracts;

public interface IWidgetProvider
{
    IEnumerable<WidgetRegistration> GetWidgets();
}

public record WidgetRegistration(
    string WidgetId,
    string Title,
    Type WidgetType,
    int PositionX = 0,
    int PositionY = 0,
    int Width = 300,
    int Height = 200,
    int ZIndex = 0,
    bool IsVisible = true
);
