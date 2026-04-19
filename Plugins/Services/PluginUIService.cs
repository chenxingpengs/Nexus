using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Plugins.Contracts;
using Nexus.Plugins.Core;

namespace Nexus.Plugins.Services;

public class PluginUIService
{
    private readonly PluginHost _pluginHost;
    private readonly List<ViewRegistration> _registeredViews = new();
    private readonly List<MenuItemRegistration> _registeredMenuItems = new();
    private readonly List<SettingsPageRegistration> _registeredSettingsPages = new();

    public IReadOnlyList<ViewRegistration> RegisteredViews => _registeredViews.AsReadOnly();
    public IReadOnlyList<MenuItemRegistration> RegisteredMenuItems => _registeredMenuItems.AsReadOnly();
    public IReadOnlyList<SettingsPageRegistration> RegisteredSettingsPages => _registeredSettingsPages.AsReadOnly();

    public event Action<ViewRegistration>? ViewRegistered;
    public event Action<MenuItemRegistration>? MenuItemRegistered;
    public event Action<SettingsPageRegistration>? SettingsPageRegistered;

    public PluginUIService(PluginHost pluginHost)
    {
        _pluginHost = pluginHost;
    }

    public void CollectAllUIExtensions()
    {
        _registeredViews.Clear();
        _registeredMenuItems.Clear();
        _registeredSettingsPages.Clear();

        foreach (var viewProvider in _pluginHost.GetAllViewProviders())
        {
            foreach (var view in viewProvider.GetViews())
            {
                _registeredViews.Add(view);
                ViewRegistered?.Invoke(view);
            }
        }

        foreach (var menuProvider in _pluginHost.GetAllMenuProviders())
        {
            foreach (var item in menuProvider.GetMenuItems())
            {
                _registeredMenuItems.Add(item);
                MenuItemRegistered?.Invoke(item);
            }
        }

        foreach (var settingsProvider in _pluginHost.GetAllSettingsProviders())
        {
            foreach (var page in settingsProvider.GetSettingsPages())
            {
                _registeredSettingsPages.Add(page);
                SettingsPageRegistered?.Invoke(page);
            }
        }

        System.Diagnostics.Debug.WriteLine($"[PluginUI] 收集到 {_registeredViews.Count} 个视图, " +
            $"{_registeredMenuItems.Count} 个菜单项, {_registeredSettingsPages.Count} 个设置页");
    }

    public IEnumerable<ViewRegistration> GetViewsForLocation(NavigationLocation location)
    {
        return _registeredViews.Where(v => v.Location == location).OrderBy(v => v.Order);
    }

    public IEnumerable<MenuItemRegistration> GetRootMenuItems()
    {
        return _registeredMenuItems.Where(m => string.IsNullOrEmpty(m.ParentId)).OrderBy(m => m.Order);
    }

    public IEnumerable<MenuItemRegistration> GetChildMenuItems(string parentId)
    {
        return _registeredMenuItems.Where(m => m.ParentId == parentId).OrderBy(m => m.Order);
    }

    public object? CreateViewModel(ViewRegistration viewReg, IServiceProvider services)
    {
        try
        {
            return Activator.CreateInstance(viewReg.ViewModelType);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PluginUI] 创建 ViewModel 失败: {viewReg.ViewModelType.Name}, {ex.Message}");
            return null;
        }
    }

    public Control? CreateViewInstance(SettingsPageRegistration pageReg)
    {
        return pageReg.ViewInstance;
    }
}
