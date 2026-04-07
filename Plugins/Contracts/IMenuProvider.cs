namespace Nexus.Plugins.Contracts;

public interface IMenuProvider
{
    IEnumerable<MenuItemRegistration> GetMenuItems();
}

public record MenuItemRegistration(
    string MenuId,
    string Label,
    string? Icon = null,
    int Order = 100,
    string? ParentId = null,
    Action? Handler = null
);
