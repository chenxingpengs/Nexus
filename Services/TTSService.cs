using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            Debug.WriteLine($"[TTS] 初始化完成，可用语音数量: {_availableVoices.Count}");
        }

        public void Speak(string text, string voice = "xiaoxiao", int rate = 0, float volume = 1.0f)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(text))
                return;

            var voiceName = Voices.TryGetValue(voice, out var v) ? v : "zh-CN-XiaoxiaoNeural";
            var cachePath = GetCachePath(text, voiceName, rate);

            Debug.WriteLine($"[TTS] 入队朗读: {text}, voice={voiceName}, rate={rate}");
            _playQueue.Enqueue(new TTSPlayInfo(text, voiceName, rate, volume, cachePath));
            _ = ProcessQueueAsync();
        }

        public void Stop()
        {
            Debug.WriteLine("[TTS] 停止朗读");
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
                        Debug.WriteLine($"[TTS] 生成音频: {info.CachePath}");
                        await GenerateAudioAsync(info);
                    }

                    if (File.Exists(info.CachePath) && !_currentCancellationToken.Token.IsCancellationRequested)
                    {
                        Debug.WriteLine($"[TTS] 播放音频: {info.CachePath}");
                        await PlayAudioAsync(info.CachePath, info.Volume);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TTS] 播放失败: {ex.Message}");
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
            var tcs = new TaskCompletionSource<bool>();
            
            try
            {
                var reader = new Mp3FileReader(filePath);
                var player = new WaveOutEvent();
                
                player.Init(reader);
                player.Volume = volume;
                
                player.PlaybackStopped += (s, e) =>
                {
                    Debug.WriteLine($"[TTS] 播放结束");
                    player.Dispose();
                    reader.Dispose();
                    tcs.TrySetResult(true);
                };
                
                player.Play();
                Debug.WriteLine($"[TTS] 开始播放: {filePath}");
                
                using var registration = _currentCancellationToken?.Token.Register(() =>
                {
                    player.Stop();
                    Debug.WriteLine("[TTS] 播放被取消");
                });
                
                await tcs.Task;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TTS] PlayAudioAsync 异常: {ex.Message}\n{ex.StackTrace}");
                tcs.TrySetException(ex);
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

        private record TTSPlayInfo(string Text, string VoiceName, int Rate, float Volume, string CachePath);
    }
}
