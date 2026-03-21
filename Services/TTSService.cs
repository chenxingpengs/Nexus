using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Edge_tts_sharp;
using Edge_tts_sharp.Model;

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
        private readonly object _queueLock = new();
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
            Edge_tts.Debug = true;
            Debug.WriteLine($"[TTS] 初始化完成，可用语音数量: {_availableVoices.Count}");
        }

        public void Speak(string text, string voice = "xiaoxiao", int rate = 0, float volume = 1.0f)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(text))
                return;

            var voiceName = Voices.TryGetValue(voice, out var v) ? v : "zh-CN-XiaoxiaoNeural";

            Debug.WriteLine($"[TTS] 入队朗读: {text.Substring(0, Math.Min(50, text.Length))}..., voice={voiceName}, rate={rate}");
            
            lock (_queueLock)
            {
                _playQueue.Enqueue(new TTSPlayInfo(text, voiceName, rate, volume));
            }
            
            _ = ProcessQueueAsync();
        }

        public void Stop()
        {
            Debug.WriteLine("[TTS] 停止朗读");
            _currentCancellationToken?.Cancel();
            lock (_queueLock)
            {
                _playQueue.Clear();
            }
            _isPlaying = false;
        }

        private async Task ProcessQueueAsync()
        {
            if (_isPlaying)
                return;

            lock (_queueLock)
            {
                if (_playQueue.Count == 0)
                    return;
            }

            _isPlaying = true;
            _currentCancellationToken = new CancellationTokenSource();

            while (true)
            {
                TTSPlayInfo? info = null;
                lock (_queueLock)
                {
                    if (_playQueue.Count == 0)
                        break;
                    info = _playQueue.Dequeue();
                }

                if (info == null || _currentCancellationToken.Token.IsCancellationRequested)
                    break;

                try
                {
                    var startTime = DateTime.Now;
                    Debug.WriteLine($"[TTS] 开始播放: {info.Text.Substring(0, Math.Min(30, info.Text.Length))}...");
                    
                    await PlayTextAsync(info);
                    
                    var playTime = (DateTime.Now - startTime).TotalMilliseconds;
                    Debug.WriteLine($"[TTS] 播放完成，耗时: {playTime:F0}ms");
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine($"[TTS] 播放被取消");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TTS] 播放失败: {ex.Message}");
                    ErrorOccurred?.Invoke(this, ex);
                }
            }

            _isPlaying = false;
            Debug.WriteLine("[TTS] 队列处理完成");
            SpeakCompleted?.Invoke(this, EventArgs.Empty);
        }

        private async Task PlayTextAsync(TTSPlayInfo info)
        {
            var voice = _availableVoices.FirstOrDefault(v => v.ShortName == info.VoiceName);
            if (voice == null)
            {
                voice = _availableVoices.FirstOrDefault(v => v.ShortName == "zh-CN-XiaoxiaoNeural");
                Debug.WriteLine($"[TTS] 未找到语音 {info.VoiceName}，使用默认语音");
            }

            if (voice == null)
            {
                Debug.WriteLine($"[TTS] 错误：未找到任何可用语音");
                return;
            }

            try
            {
                var option = new PlayOption
                {
                    Text = info.Text,
                    Rate = info.Rate,
                    Volume = info.Volume
                };

                Debug.WriteLine($"[TTS] 直接播放, voice={voice.ShortName}, rate={info.Rate}, volume={info.Volume}");
                
                _currentCancellationToken?.Token.ThrowIfCancellationRequested();
                
                await Task.Run(() =>
                {
                    Edge_tts.PlayText(option, voice);
                }, _currentCancellationToken.Token);
                
                Debug.WriteLine($"[TTS] 播放完成");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[TTS] 播放被取消");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TTS] PlayTextAsync 异常: {ex.Message}\n{ex.StackTrace}");
                throw;
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

        private record TTSPlayInfo(string Text, string VoiceName, int Rate, float Volume);
    }
}
