using System.Collections.Generic;
using System.Threading.Tasks;
using Nexus.Models;
using Nexus.Services;

namespace Nexus.Services.Http;

public class PluginApiService : HttpService
{
    public PluginApiService(ConfigService configService, ToastService? toastService = null)
        : base(configService, toastService)
    {
    }

    public async Task<ApiResponse<List<RemotePluginInfo>>?> GetRemotePluginListAsync()
    {
        return await GetAsync<List<RemotePluginInfo>>("desktop/plugins", new RequestOptions
        {
            RequireAuth = true,
            OperationName = "获取远程插件列表"
        });
    }

    public async Task<ApiResponse<List<MarketPluginItem>>?> GetMarketPluginListAsync()
    {
        return await GetAsync<List<MarketPluginItem>>("desktop/plugins/market", new RequestOptions
        {
            RequireAuth = true,
            OperationName = "获取市场插件列表"
        });
    }

    public async Task<ApiResponse<object>?> InstallPluginAsync(string pluginId)
    {
        return await PostAsync<object>($"desktop/plugins/{pluginId}/install", null, new RequestOptions
        {
            RequireAuth = true,
            OperationName = $"安装插件 {pluginId}",
            ShowSuccessToast = true,
            ShowErrorToast = true
        });
    }
}
