using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Plugins.Contracts;
using Nexus.Plugins.Contracts.Models;
using Nexus.Plugins.Core;

namespace ExampleAttendancePlugin;

public class Plugin : PluginBase
{
    private AlertSettings _settings = new();
    private List<AttendanceRecord> _recentRecords = new();

    public override string Id => "com.example.attendance-alert";
    public override string Name => "考勤提醒增强";
    public override string Version => "1.0.0";
    public override string Description => "监听考勤事件并提供桌面端增强提醒";
    public override string Author => "Nexus Plugin Demo";
    public override string[] Dependencies => Array.Empty<string>();

    public override void Initialize(IPluginContext context, IServiceCollection services)
    {
        base.Initialize(context, services);

        var settingsPath = Path.Combine(ConfigFolder, "settings.json");
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                _settings = JsonSerializer.Deserialize<AlertSettings>(json) ?? new AlertSettings();
            }
            catch { }
        }

        context.RegisterWebSocketHandler(this);

        System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 插件初始化完成，配置目录: {ConfigFolder}");
    }

    public override void OnStartup(IServiceProvider serviceProvider)
    {
        System.Diagnostics.Debug.WriteLine("[AttendanceAlert] 插件已启动");
    }

    public override void OnShutdown()
    {
        SaveSettings();
        System.Diagnostics.Debug.WriteLine("[AttendanceAlert] 插件已关闭");
    }

    public override string[] SubscribedEvents => new[]
    {
        "attendance_update",
        "notification:push",
        "bind_notification"
    };

    public async Task HandleMessageAsync(string eventName, JsonElement data, WebSocketContext context)
    {
        switch (eventName)
        {
            case "attendance_update":
                await HandleAttendanceUpdate(data, context);
                break;
            case "notification:push":
                await HandleNotification(data, context);
                break;
            case "bind_notification":
                await HandleBindNotification(data, context);
                break;
        }
    }

    private async Task HandleAttendanceUpdate(JsonElement data, WebSocketContext ctx)
    {
        try
        {
            var record = new AttendanceRecord
            {
                StudentName = data.GetProperty("student_name").GetString() ?? "未知",
                Status = data.GetProperty("status").GetString() ?? "未知",
                Time = DateTime.Now,
                RawData = data.GetRawText()
            };

            _recentRecords.Add(record);
            if (_recentRecords.Count > 100) _recentRecords.RemoveAt(0);

            var message = $"📋 考勤更新: {record.StudentName} - {record.Status}";
            System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] {message}");

            if (_settings.EnableToast)
            {
                ctx.PublishLocal("plugin:toast", new { Title = "考勤更新", Message = message });
            }

            if (_settings.EnableSound && !string.IsNullOrEmpty(_settings.SoundType))
            {
                ctx.PublishLocal("plugin:sound", new { Type = _settings.SoundType });
            }

            if (_settings.AutoReplyOnAbsent &&
                record.Status.Contains("缺勤", StringComparison.OrdinalIgnoreCase))
            {
                await ctx.EmitAsync("attendance_ack", new
                {
                    student_name = record.StudentName,
                    plugin_id = Id,
                    ack_time = DateTime.UtcNow.ToString("O")
                });

                System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 已自动回复确认: {record.StudentName}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 处理 attendance_update 失败: {ex.Message}");
        }
    }

    private async Task HandleNotification(JsonElement data, WebSocketContext ctx)
    {
        try
        {
            var title = data.GetProperty("title").GetString() ?? "通知";
            var content = data.GetProperty("content").GetString() ?? "";

            System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 收到通知: {title} - {content}");

            if (_settings.ForwardNotifications)
            {
                ctx.PublishLocal("plugin:notification-forwarded", new
                {
                    OriginalTitle = title,
                    OriginalContent = content,
                    SourcePlugin = Id,
                    ForwardedAt = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 处理 notification 失败: {ex.Message}");
        }
    }

    private async Task HandleBindNotification(JsonElement data, WebSocketContext ctx)
    {
        try
        {
            var type = data.GetProperty("type").GetString() ?? "";
            System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 绑定通知: type={type}");

            if (type == "screenshot_request" && _settings.AllowScreenshot)
            {
                await ctx.EmitAsync("screenshot_response", new
                {
                    plugin_id = Id,
                    status = "not_implemented",
                    message = "截图功能待实现"
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 处理 bind_notification 失败: {ex.Message}");
        }
    }

    public override Task OnConnectionStateChangedAsync(ConnectionState state, string? errorMessage = null)
    {
        System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] WS连接状态变更: {state}" +
            (errorMessage != null ? $", 错误: {errorMessage}" : ""));

        if (state == ConnectionState.Connected)
        {
            _recentRecords.Clear();
        }

        return Task.CompletedTask;
    }

    public List<AttendanceRecord> GetRecentRecords() => _recentRecords.ToList();

    public AlertSettings GetSettings() => _settings;

    public void UpdateSettings(AlertSettings settings)
    {
        _settings = settings;
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(ConfigFolder);
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(ConfigFolder, "settings.json"), json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AttendanceAlert] 保存配置失败: {ex.Message}");
        }
    }
}

public class AlertSettings
{
    public bool EnableToast { get; set; } = true;
    public bool EnableSound { get; set; } = true;
    public string SoundType { get; set; } = "default";
    public bool AutoReplyOnAbsent { get; set; } = false;
    public bool ForwardNotifications { get; set; } = true;
    public bool AllowScreenshot { get; set; } = false;
}

public class AttendanceRecord
{
    public string StudentName { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime Time { get; set; }
    public string RawData { get; set; } = "";
}
