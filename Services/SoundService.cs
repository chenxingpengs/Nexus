using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Avalonia.Threading;

namespace Nexus.Services
{
    public class SoundService : IDisposable
    {
        private IWavePlayer? _wavePlayer;
        private AudioFileReader? _audioFileReader;
        private bool _isPlaying;
        private bool _disposed;
        private readonly object _lock = new();
        private string? _currentSoundFile;
        private bool _shouldLoop;

        public event EventHandler? PlaybackCompleted;

        public bool IsPlaying => _isPlaying;

        public void PlaySound(string soundFileName, bool loop = false, float volume = 1.0f)
        {
            if (_disposed) return;

            var soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Sounds", soundFileName);
            
            if (!File.Exists(soundPath))
            {
                Debug.WriteLine($"[SoundService] 声音文件不存在: {soundPath}");
                Debug.WriteLine($"[SoundService] 检查路径: {AppDomain.CurrentDomain.BaseDirectory}");
                return;
            }

            Debug.WriteLine($"[SoundService] 准备播放声音: {soundPath}");

            try
            {
                StopPlayback();

                _audioFileReader = new AudioFileReader(soundPath);
                _wavePlayer = new WaveOutEvent();
                _wavePlayer.Init(_audioFileReader);
                _wavePlayer.Volume = Math.Clamp(volume, 0f, 1f);

                _isPlaying = true;
                _currentSoundFile = soundFileName;
                _shouldLoop = loop;

                _wavePlayer.PlaybackStopped += OnPlaybackStopped;

                _wavePlayer.Play();
                Debug.WriteLine($"[SoundService] 开始播放声音: {soundFileName}, 循环: {loop}, 音量: {volume}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SoundService] 播放声音失败: {ex.Message}");
                Debug.WriteLine($"[SoundService] 异常堆栈: {ex.StackTrace}");
                _isPlaying = false;
            }
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            Debug.WriteLine($"[SoundService] 播放停止事件触发, 循环: {_shouldLoop}, 正在播放: {_isPlaying}");
            
            if (_shouldLoop && _isPlaying && !_disposed)
            {
                try
                {
                    if (_audioFileReader != null)
                    {
                        _audioFileReader.Position = 0;
                        _wavePlayer?.Play();
                        Debug.WriteLine($"[SoundService] 循环播放: {_currentSoundFile}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SoundService] 循环播放失败: {ex.Message}");
                }
            }
            else
            {
                _isPlaying = false;
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            }
        }

        public void StopPlayback()
        {
            lock (_lock)
            {
                try
                {
                    _isPlaying = false;

                    if (_wavePlayer != null)
                    {
                        _wavePlayer.Stop();
                        _wavePlayer.Dispose();
                        _wavePlayer = null;
                    }

                    if (_audioFileReader != null)
                    {
                        _audioFileReader.Dispose();
                        _audioFileReader = null;
                    }

                    Debug.WriteLine("[SoundService] 停止播放声音");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SoundService] 停止播放失败: {ex.Message}");
                }
            }
        }

        public void SetVolume(float volume)
        {
            if (_wavePlayer != null)
            {
                _wavePlayer.Volume = Math.Clamp(volume, 0f, 1f);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopPlayback();
            _disposed = true;

            GC.SuppressFinalize(this);
        }
    }
}
