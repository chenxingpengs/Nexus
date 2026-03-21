# Edge-TTS 朗读模块实现计划 (C# / Avalonia)

## 概述
使用 `Edge_tts_sharp` 库（ClassIsland 同款方案）实现语音朗读功能，集成到 Nexus 客户端。

## 技术方案

### 依赖
- `Edge_tts_sharp`: C# 版 edge-tts 库，通过 WebSocket 调用微软 Edge TTS 服务
- `NAudio`: 音频播放库（Edge_tts_sharp 依赖）

### 核心功能
1. 文本转语音（TTS）
2. 多种中文语音选择
3. 语速、音量调节
4. 异步播放
5. 本地缓存
6. 队列管理

## 实现步骤

### 步骤 1: 安装 NuGet 包
```bash
dotnet add package Edge_tts_sharp
```

### 步骤 2: 创建 TTS 服务模块
**文件**: `Services/TTSService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Edge_tts_sharp;
using Edge_tts_sharp.Model;
using NAudio.Wave;

namespace Nexus.Services
{
    public class TTSService : IDisposable
    {
        public static readonly string CacheFolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexus", "TTSCache");

        public static readonly Dictionary<string, string> Voices = new()
        {
            { "xiaoxiao", "zh-CN-XiaoxiaoNeural" },
            { "yunyang", "zh-CN-YunyangNeural" },
            { "xiaoyi", "zh-CN-XiaoyiNeural" },
            { "yunjian", "zh-CN-YunjianNeural" },
            { "yunxi", "zh-CN-YunxiNeural" },
            { "xiaoxuan", "zh-CN-XiaoxuanNeural" }
        };

        private readonly List<eVoice> _availableVoices;
        private readonly Queue<TTSPlayInfo> _playQueue = new();
        private CancellationTokenSource? _currentCancellationToken;
        private bool _isPlaying = false;
        private bool _isDisposed = false;

        public event EventHandler? SpeakCompleted;
        public event EventHandler<Exception>? ErrorOccurred;

        public bool IsPlaying => _isPlaying;

        public TTSService()
        {
            _availableVoices = Edge_tts.GetVoice();
            Directory.CreateDirectory(CacheFolderPath);
        }

        public void Speak(string text, string voice = "xiaoxiao", int rate = 0, float volume = 1.0f)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(text))
                return;

            var voiceName = Voices.TryGetValue(voice, out var v) ? v : "zh-CN-XiaoxiaoNeural";
            var cachePath = GetCachePath(text, voiceName, rate);

            _playQueue.Enqueue(new TTSPlayInfo(text, voiceName, rate, volume, cachePath));
            _ = ProcessQueueAsync();
        }

        public void Stop()
        {
            _currentCancellationToken?.Cancel();
            _playQueue.Clear();
            _isPlaying = false;
        }

        private string GetCachePath(string text, string voice, int rate)
        {
                var data = Encoding.UTF8.GetBytes($"{text}_{voice}_{rate}");
                var hash = MD5.HashData(data);
                var hashStr = string.Concat(hash.Select(b => b.ToString("x2")));
                return Path.Combine(CacheFolderPath, voice, $"{hashStr}.mp3");
            }

        private async Task ProcessQueueAsync()
            {
                if (_isPlaying || _playQueue.Count == 0)
                    return;

                _isPlaying = true;
                _currentCancellationToken = new CancellationTokenSource();

                while (_playQueue.Count > 0 && !_currentCancellationToken.Token.IsCancellationRequested)
                {
                    var info = _playQueue.Dequeue();

                    try
                    {
                        if (!File.Exists(info.CachePath))
                        {
                            await GenerateAudioAsync(info);
                        }

                        if (File.Exists(info.CachePath) && !_currentCancellationToken.Token.IsCancellationRequested)
                        {
                            await PlayAudioAsync(info.CachePath, info.Volume);
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(this, ex);
                    }
                }

                _isPlaying = false;
                SpeakCompleted?.Invoke(this, EventArgs.Empty);
            }

        private async Task GenerateAudioAsync(TTSPlayInfo info)
            {
                var voice = _availableVoices.FirstOrDefault(v => v.ShortName == info.VoiceName);
                if (voice == null)
                {
                    voice = _availableVoices.FirstOrDefault(v => v.ShortName == "zh-CN-XiaoxiaoNeural");
                }

                var directory = Path.GetDirectoryName(info.CachePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var option = new PlayOption
                {
                    Text = info.Text,
                    Rate = info.Rate,
                    SavePath = info.CachePath
                };

                await Task.Run(() => Edge_tts.SaveAudio(option, voice));
            }

        private async Task PlayAudioAsync(string filePath, float volume)
            {
                await using var reader = new Mp3FileReader(filePath);
                using var player = new WaveOutEvent();
                player.Init(reader);
                player.Volume = volume;
                player.Play();

                while (player.PlaybackState == PlaybackState.Playing)
                {
                    if (_currentCancellationToken?.Token.IsCancellationRequested == true)
                    {
                        player.Stop();
                        break;
                    }
                    await Task.Delay(100);
                }
            }

        public static List<(string Key, string Name)> GetAvailableVoices()
            {
                return Voices.Select(v => (v.Key, v.Value)).ToList();
            }

        public void Dispose()
            {
                if (_isDisposed) return;
                _isDisposed = true;
                Stop();
            }

        private record TTSPlayInfo(string Text, string VoiceName, int Rate, float Volume, string CachePath);
    }
}
```

### 步骤 3: 创建便捷调用类
**文件**: `Services/TTS.cs`

```csharp
using System;

namespace Nexus.Services
{
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
                        _instance ??= new TTSService();
                    }
                }
                return _instance;
            }
        }

        public static void Speak(string text, string voice = "xiaoxiao", int rate = 0, float volume = 1.0f)
        {
            Instance.Speak(text, voice, rate, volume);
        }

        public static void Stop()
        {
            Instance.Stop();
        }
    }
}
```

### 步骤 4: 集成到通知系统
在 `NotificationService.cs` 中添加 TTS 支持：

## 文件结构

```
Nexus/
├── Services/
│   ├── TTSService.cs          # TTS 服务
│   ├── TTS.cs                  # 便捷调用
│   └── NotificationService.cs  # 修改：添加 TTS 调用
└── Nexus.csproj                # 添加 Edge_tts_sharp 包引用
```

## 使用示例

```csharp
// 快速朗读
TTS.Speak("欢迎使用校园考勤系统");

// 指定语音和语速
TTS.Speak("通知：请按时打卡", voice: "yunyang", rate: 20);

// 停止朗读
TTS.Stop();
```

## 预设语音

| Key | 名称 | 特点 |
|-----|------|------|
| xiaoxiao | 晓晓 | 女声，自然亲切 |
| yunyang | 云扬 | 男声，新闻播报风格 |
| xiaoyi | 晓伊 | 女声，温柔甜美 |
| yunjian | 云健 | 男声，沉稳大气 |
| yunxi | 云希 | 女声，温柔自然 |
| xiaoxuan | 晓萱 | 女声，客服风格 |

## 注意事项

1. **网络依赖**: 首次生成音频需要网络连接
2. **缓存机制**: 音频文件会缓存到本地，避免重复生成
3. **资源释放**: 使用完毕后调用 Dispose() 释放资源
4. **队列播放**: 支持多条文本排队朗读
