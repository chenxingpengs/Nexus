# Edge-TTS 朗读模块实现计划 (C# / Avalonia)

## 概述
使用 edge-tts 通过 HTTP API 调用微软 Edge 在线文本转语音服务，在 Nexus 客户端中实现语音朗读功能。

## 技术方案

### 依赖
- `NagerEdgeTTS`: C# 版 edge-tts 库，通过 HTTP API 调用微软服务
- `NAudio`: 音频播放库

### 核心功能
1. 文本转语音（TTS）
2. 多种中文语音选择
3. 语速、音量调节
4. 异步播放
5. 缓存管理

## 实现步骤

### 步骤 1: 安装 NuGet 包
```bash
dotnet add package NagerEdgeTTS
```

### 步骤 2: 创建 TTS 服务模块
**文件**: `Services/TTSService.cs`

```csharp
using NagerEdgeTTS;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Nexus.Services
{
    public class TTSService : IDisposable
    {
        private readonly NagerEdgeTTS.EdgeTTS _tts;
        private readonly IWavePlayer? _wavePlayer;
        private bool _isPlaying = false;
        private bool _isDisposed = false;

        // 预设中文语音
        public static readonly Dictionary<string, (string Name, string DisplayName, string Description)> Voices = new()
        {
            { "xiaoxiao", ("zh-CN-XiaoxiaoNeural", "晓晓", "女声，自然亲切") },
            { "yunyang", ("zh-CN-YunyangNeural", "云扬", "男声，新闻播报风格") },
            { "xiaoyi", ("zh-CN-XiaoyiNeural", "晓伊", "女声，温柔甜美") },
            { "yunjian", ("zh-CN-YunjianNeural", "云健", "男声，沉稳大气") },
            { "xiaoxuan", ("zh-CN-XiaoxuanNeural", "晓萱", "女声，客服风格") }
        };

        public event EventHandler<string>? SpeakCompleted;
        public event EventHandler<Exception>? ErrorOccurred;

        public bool IsPlaying => _isPlaying;

        public TTSService()
        {
            _tts = new EdgeTTS();
        }

        /// <summary>
        /// 朗读文本
        /// </summary>
        /// <param name="text">要朗读的文本</param>
        /// <param name="voice">语音名称：xiaoxiao, yunyang, xiaoyi, yunjian, xiaoxuan</param>
        /// <param name="rate">语速：-100 到 100</param>
        /// <param name="volume">音量：0 到 100</param>
        public async Task SpeakAsync(string text, string voice = "xiaoxiao", int rate = 0, int volume = 100)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TTSService));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                _isPlaying = true;

                var voiceInfo = Voices.TryGetValue(voice, out var v) ? v.Name : "zh-CN-XiaoxiaoNeural";
                var rateStr = rate >= 0 ? $"+{rate}%" : $"{rate}%";

                Debug.WriteLine($"[TTS] 开始朗读: {text}, voice={voiceInfo}, rate={rateStr}");

                // 使用 NagerEdgeTTS 生成音频
                var audioData = await _tts.SynthesizeAsync(text, voiceInfo, rateStr);

                // 使用 NAudio 播放
                using var stream = new MemoryStream(audioData);
                using var reader = new Mp3FileReader(stream);
                
                _wavePlayer = new WaveOutEvent();
                _wavePlayer.Init(reader);
                _wavePlayer.PlaybackStopped += (s, e) =>
                {
                    _isPlaying = false;
                    SpeakCompleted?.Invoke(this, voice);
                };

                _wavePlayer.Play();

                Debug.WriteLine($"[TTS] 播放完成");
            }
            catch (Exception ex)
            {
                _isPlaying = false;
                Debug.WriteLine($"[TTS] 播放失败: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        /// <summary>
        /// 停止朗读
        /// </summary>
        public void Stop()
        {
            if (_wavePlayer != null && _isPlaying)
            {
                _wavePlayer.Stop();
                _isPlaying = false;
            }
        }

        /// <summary>
        /// 获取可用语音列表
        /// </summary>
        public static List<(string Key, string Name, string Description)> GetAvailableVoices()
        {
            return Voices.Select(v => (v.Key, v.Value.Name, v.Value.Description)).ToList();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            
            _wavePlayer?.Stop();
            _wavePlayer?.Dispose();
            _isPlaying = false;
        }
    }
}
```

### 步骤 3: 创建便捷调用类
**文件**: `Services/TTSHelper.cs`

```csharp
using System;
using System.Threading.Tasks;

namespace Nexus.Services
{
    /// <summary>
    /// TTS 便捷调用类 - 静态方法快速调用
    /// </summary>
    public static class TTS
    {
        private static TTSService? _instance;
        private static readonly object _lock = new();

        public static TTSService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new TTSService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 快速朗读文本
        /// </summary>
        public static async Task SpeakAsync(string text, string voice = "xiaoxiao", int rate = 0)
        {
            await Instance.SpeakAsync(text, voice, rate);
        }

        /// <summary>
        /// 停止朗读
        /// </summary>
        public static void Stop()
        {
            Instance.Stop();
        }
    }
}
```

### 步骤 4: 集成到通知系统
在 `NotificationService.cs` 中添加 TTS 支持：

```csharp
// 收到通知时朗读
private void OnNotificationReceived(object? sender, Models.Notification notification)
{
    // ... 现有代码 ...
    
    // 朗读通知内容
    _ = TTSService.Instance.SpeakAsync(
        $"{notification.Title}：{notification.Content}",
        voice: "xiaoxiao"
    );
}
```

## 文件结构

```
Nexus/
├── Services/
│   ├── TTSService.cs      # TTS 服务
│   ├── TTSHelper.cs        # 便捷调用
│   └── NotificationService.cs  # 修改：添加 TTS 调用
└── Nexus.csproj            # 添加 NagerEdgeTTS, NAudio 包引用
```

## 使用示例

```csharp
// 方式1: 使用 TTSService
var tts = new TTSService();
await tts.SpeakAsync("欢迎使用校园考勤系统", voice: "xiaoxiao", rate: 20);

// 方式2: 使用静态便捷方法
await TTS.SpeakAsync("通知：请按时打卡", voice: "yunyang");

// 方式3: 停止朗读
TTS.Stop();
```

## 预设语音

| Key | 名称 | 特点 |
|-----|------|------|
| xiaoxiao | 晓晓 | 女声，自然亲切 |
| yunyang | 云扬 | 男声，新闻播报风格 |
| xiaoyi | 晓伊 | 女声，温柔甜美 |
| yunjian | 云健 | 男声，沉稳大气 |
| xiaoxuan | 晓萱 | 女声，客服风格 |

## 注意事项

1. **网络依赖**: edge-tts 需要网络连接
2. **异步处理**: 所有方法都是异步的
3. **资源释放**: 使用完毕后调用 Dispose() 释放资源
4. **错误处理**: 通过事件订阅错误
