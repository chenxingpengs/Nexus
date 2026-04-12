using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Nexus.Models;
using Nexus.Services;

namespace Nexus.Views
{
    public partial class PageCallWindow : Window
    {
        private PageCall? _pageCall;
        private readonly SocketIOService _socketIOService;
        private bool _confirmed = false;
        private DispatcherTimer? _speakTimer;
        private int _speakCount = 0;
        private const int MaxSpeakCount = 5;
        private const int SpeakIntervalMs = 5000;

        public event EventHandler<string>? PageCallClosed;

        public PageCallWindow(SocketIOService socketIOService)
        {
            InitializeComponent();
            _socketIOService = socketIOService;
        }

        public void ShowPageCall(PageCall pageCall)
        {
            _pageCall = pageCall;
            _speakCount = 0;
            
            Dispatcher.UIThread.Post(() =>
            {
                StudentNameText.Text = pageCall.StudentName;
                ClassNameText.Text = pageCall.ClassName ?? "未知班级";
                TimeText.Text = pageCall.CreatedAt.ToString("HH:mm:ss");
                
                if (!string.IsNullOrEmpty(pageCall.Reason))
                {
                    ReasonPanel.IsVisible = true;
                    ReasonText.Text = pageCall.Reason;
                }
                else
                {
                    ReasonPanel.IsVisible = false;
                }
                
                Debug.WriteLine($"[PageCallWindow] 显示寻人通知: {pageCall.StudentName}, 班级: {pageCall.ClassName}");
            });
            
            StartRepeatedSpeak();
        }

        private void StartRepeatedSpeak()
        {
            if (_pageCall == null) return;
            
            var speakText = _pageCall.StudentName;
            if (!string.IsNullOrEmpty(_pageCall.Reason))
            {
                speakText += $" {_pageCall.Reason}";
            }
            
            TTS.Speak(speakText, voice: "xiaoxiao", rate: 0);
            _speakCount++;
            Debug.WriteLine($"[PageCallWindow] 语音播报第 {_speakCount} 次");
            
            _speakTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(SpeakIntervalMs)
            };
            
            _speakTimer.Tick += (s, e) =>
            {
                if (_confirmed || _speakCount >= MaxSpeakCount)
                {
                    StopRepeatedSpeak();
                    return;
                }
                
                TTS.Speak(speakText, voice: "xiaoxiao", rate: 0);
                _speakCount++;
                Debug.WriteLine($"[PageCallWindow] 语音播报第 {_speakCount} 次");
            };
            
            _speakTimer.Start();
        }

        private void StopRepeatedSpeak()
        {
            if (_speakTimer != null)
            {
                _speakTimer.Stop();
                _speakTimer = null;
            }
            TTS.Stop();
        }

        private async void ConfirmButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_pageCall == null || _confirmed)
            {
                return;
            }

            try
            {
                StopRepeatedSpeak();
                
                ConfirmButton.IsEnabled = false;
                ConfirmButton.Content = "确认中...";

                if (_socketIOService.IsConnected)
                {
                    await _socketIOService.SendAsync("page_call_confirm", new
                    {
                        page_call_id = _pageCall.Id
                    });
                    
                    Debug.WriteLine($"[PageCallWindow] 发送确认: {_pageCall.Id}");
                    
                    _confirmed = true;
                    
                    Dispatcher.UIThread.Post(() =>
                    {
                        ConfirmButton.Content = "已确认";
                        ConfirmButton.Background = new SolidColorBrush(Color.Parse("#67C23A"));
                    });
                    
                    await Task.Delay(1000);
                }
                else
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        ConfirmButton.Content = "连接断开";
                        ConfirmButton.Background = new SolidColorBrush(Color.Parse("#F56C6C"));
                        ConfirmButton.IsEnabled = true;
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PageCallWindow] 确认失败: {ex.Message}");
                Dispatcher.UIThread.Post(() =>
                {
                    ConfirmButton.Content = "确认失败，请重试";
                    ConfirmButton.IsEnabled = true;
                });
            }

            if (_confirmed)
            {
                PageCallClosed?.Invoke(this, _pageCall.Id);
                base.Close();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!_confirmed)
            {
                e.Cancel = true;
                Debug.WriteLine($"[PageCallWindow] 阻止关闭窗口，未确认");
            }
            else
            {
                StopRepeatedSpeak();
                base.OnClosing(e);
            }
        }
    }
}
