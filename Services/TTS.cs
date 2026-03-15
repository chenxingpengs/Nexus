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

        public static bool IsPlaying => Instance.IsPlaying;

        public static event EventHandler? SpeakCompleted
        {
            add => Instance.SpeakCompleted += value;
            remove => Instance.SpeakCompleted -= value;
        }

        public static event EventHandler<Exception>? ErrorOccurred
        {
            add => Instance.ErrorOccurred += value;
            remove => Instance.ErrorOccurred -= value;
        }

        public static void Speak(string text, string voice = "xiaoxiao", int rate = 0, float volume = 1.0f)
        {
            Instance.Speak(text, voice, rate, volume);
        }

        public static void Stop()
        {
            Instance.Stop();
        }

        public static void Dispose()
        {
            _instance?.Dispose();
            _instance = null;
        }
    }
}
