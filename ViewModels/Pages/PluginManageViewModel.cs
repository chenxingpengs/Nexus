using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexus.Models;
using Nexus.Plugins.Contracts.Models;
using Nexus.Plugins.Services;
using Nexus.Services;
using Nexus.Services.Http;

namespace Nexus.ViewModels.Pages;

public partial class PluginManageViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<PluginMetadata> _plugins = new();

    [ObservableProperty]
    private PluginMetadata? _selectedPlugin;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _wsHandlerCount;

    [ObservableProperty]
    private int _viewCount;

    [ObservableProperty]
    private int _menuItemCount;

    [ObservableProperty]
    private ObservableCollection<RemotePluginInfo> _remotePlugins = new();

    [ObservableProperty]
    private bool _isRemoteLoading;

    [ObservableProperty]
    private string _remoteStatusMessage = "";

    [ObservableProperty]
    private bool _isInstalling;

    public bool IsNotInstalling => !IsInstalling;

    public ICommand UninstallPluginAsyncCommand { get; }
    public ICommand InstallOrUpdatePluginAsyncCommand { get; }
    public ICommand OpenGitHubRepoCommand { get; }

    private readonly PluginApiService? _pluginApiService;

    public PluginManageViewModel(ConfigService? configService = null)
    {
        if (configService != null)
        {
            _pluginApiService = new PluginApiService(configService);
        }

        UninstallPluginAsyncCommand = new AsyncRelayCommand<PluginMetadata?>(UninstallPluginAsync);
        InstallOrUpdatePluginAsyncCommand = new AsyncRelayCommand<RemotePluginInfo?>(InstallOrUpdatePluginAsync);
        OpenGitHubRepoCommand = new RelayCommand<string?>(OpenGitHubRepo);

        LoadPlugins();
        LoadRemotePlugins();
    }

    private void LoadPlugins()
    {
        IsLoading = true;

        try
        {
            if (App.PluginServiceInstance != null)
            {
                var allPlugins = App.PluginServiceInstance.GetAllPluginMetadata().ToList();
                Plugins = new ObservableCollection<PluginMetadata>(allPlugins);

                WsHandlerCount = App.WSBridgeInstance?.HandlerCount ?? 0;
                ViewCount = App.PluginUIServiceInstance?.RegisteredViews.Count ?? 0;
                MenuItemCount = App.PluginUIServiceInstance?.RegisteredMenuItems.Count ?? 0;

                StatusMessage = $"已加载 {Plugins.Count} 个插件";
            }
            else
            {
                StatusMessage = "插件系统未初始化";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async void LoadRemotePlugins()
    {
        if (_pluginApiService == null)
        {
            RemoteStatusMessage = "无法连接到服务器";
            return;
        }

        IsRemoteLoading = true;
        RemoteStatusMessage = "正在获取远程插件列表...";

        try
        {
            var response = await _pluginApiService.GetRemotePluginListAsync();

            if (response != null && response.IsSuccess && response.Data != null)
            {
                RemotePlugins = new ObservableCollection<RemotePluginInfo>(response.Data);
                RemoteStatusMessage = $"共 {response.Data.Count} 个远程插件";
            }
            else
            {
                RemoteStatusMessage = response?.Msg ?? "获取远程插件列表失败";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginManage] 获取远程插件失败: {ex.Message}");
            RemoteStatusMessage = $"获取失败: {ex.Message}";
        }
        finally
        {
            IsRemoteLoading = false;
        }
    }

    public void Refresh()
    {
        LoadPlugins();
        LoadRemotePlugins();
    }

    [RelayCommand]
    private async Task RefreshRemoteAsync()
    {
        await Task.Run(() => LoadRemotePlugins());
    }

    private async Task UninstallPluginAsync(PluginMetadata? plugin)
    {
        if (plugin == null || App.PluginServiceInstance == null) return;

        try
        {
            var success = await App.PluginServiceInstance.UninstallPluginAsync(plugin.Id);
            if (success)
            {
                Plugins.Remove(plugin);
                StatusMessage = $"已卸载插件: {plugin.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"卸载失败: {ex.Message}";
        }
    }

    private async Task InstallOrUpdatePluginAsync(RemotePluginInfo? plugin)
    {
        if (plugin == null || _pluginApiService == null) return;

        if (string.IsNullOrEmpty(plugin.DownloadUrl))
        {
            RemoteStatusMessage = $"插件 {plugin.Name} 未提供自动安装来源，请联系管理员";
            return;
        }

        IsInstalling = true;
        RemoteStatusMessage = $"正在{(plugin.Installed ? "更新" : "安装")} {plugin.Name}...";

        try
        {
            var response = await _pluginApiService.InstallPluginAsync(plugin.Id);

            if (response != null && response.IsSuccess)
            {
                RemoteStatusMessage = $"{plugin.Name} {(plugin.Installed ? "更新" : "安装")}成功";
                LoadRemotePlugins();
                LoadPlugins();
            }
            else
            {
                RemoteStatusMessage = response?.Msg ?? $"{plugin.Name} 安装失败";
            }
        }
        catch (Exception ex)
        {
            RemoteStatusMessage = $"操作失败: {ex.Message}";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private static void OpenGitHubRepo(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginManage] 打开链接失败: {ex.Message}");
            }
        }
    }
}
