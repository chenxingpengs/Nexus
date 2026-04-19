using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Nexus.Models.Chat;
using Nexus.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;

namespace Nexus.Views.Pages
{
    public partial class ChatPage : UserControl
    {
        private ScrollViewer? _messagesScrollViewer;
        private DispatcherTimer? _scrollTimer;
        private bool _hasScrolledToDivider = false;
        private Window? _parentWindow;
        private bool _autoScrollEnabled = true;

        public ChatPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public ChatPage(ChatPageViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _messagesScrollViewer = this.FindControl<ScrollViewer>("MessagesScrollViewer");
            if (_messagesScrollViewer != null)
            {
                _messagesScrollViewer.ScrollChanged += OnMessagesScrollChanged;
            }

            _parentWindow = this.FindAncestorOfType<Window>();
            if (_parentWindow != null)
            {
                _parentWindow.Activated += OnWindowActivated;
                _parentWindow.Deactivated += OnWindowDeactivated;
                _parentWindow.GetObservable(Window.WindowStateProperty).Subscribe(OnWindowStateChanged);
            }

            if (DataContext is ChatPageViewModel viewModel)
            {
                viewModel.UpdateWindowState(true, false);
                viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_messagesScrollViewer != null)
            {
                _messagesScrollViewer.ScrollChanged -= OnMessagesScrollChanged;
            }

            if (_parentWindow != null)
            {
                _parentWindow.Activated -= OnWindowActivated;
                _parentWindow.Deactivated -= OnWindowDeactivated;
            }

            if (DataContext is ChatPageViewModel viewModel)
            {
                viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
            }

            _scrollTimer?.Stop();
            _scrollTimer = null;
        }

        private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && _autoScrollEnabled)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _messagesScrollViewer?.ScrollToEnd();
                }, DispatcherPriority.Render);
            }
        }

        private void OnConversationClick(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is Conversation conversation)
            {
                if (DataContext is ChatPageViewModel viewModel)
                {
                    _hasScrolledToDivider = false;
                    viewModel.SelectedConversation = conversation;
                    
                    Dispatcher.UIThread.Post(() =>
                    {
                        _messagesScrollViewer?.ScrollToEnd();
                    }, DispatcherPriority.Render);
                }
            }
        }

        private void OnMessagesScrollChanged(object? sender, ScrollChangedEventArgs e)
        {
            if (_hasScrolledToDivider) return;

            if (DataContext is ChatPageViewModel viewModel && viewModel.HasNewMessageDivider)
            {
                _scrollTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _scrollTimer.Tick -= OnScrollTimerTick;
                _scrollTimer.Tick += OnScrollTimerTick;
                _scrollTimer.Start();
            }
        }

        private void OnScrollTimerTick(object? sender, EventArgs e)
        {
            _scrollTimer?.Stop();

            if (IsDividerVisible())
            {
                _hasScrolledToDivider = true;
                if (DataContext is ChatPageViewModel viewModel)
                {
                    viewModel.MarkMessagesAsRead();
                }
            }
        }

        private bool IsDividerVisible()
        {
            if (_messagesScrollViewer == null) return false;

            var scrollBounds = _messagesScrollViewer.Bounds;
            var itemsControl = _messagesScrollViewer.Content as ItemsControl;
            if (itemsControl == null) return false;

            var items = itemsControl.Items;
            foreach (var item in items)
            {
                if (item is ChatMessage message && message.IsNewMessageDivider)
                {
                    var container = itemsControl.ContainerFromItem(item) as ContentControl;
                    if (container != null)
                    {
                        var containerBounds = container.Bounds;
                        var containerTop = containerBounds.Top;
                        var containerBottom = containerBounds.Bottom;
                        var scrollTop = _messagesScrollViewer.Offset.Y;
                        var scrollBottom = scrollTop + scrollBounds.Height;

                        if (containerBottom >= scrollTop && containerTop <= scrollBottom)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private void OnWindowActivated(object? sender, EventArgs e)
        {
            if (DataContext is ChatPageViewModel viewModel)
            {
                viewModel.UpdateWindowState(true, false);
            }
        }

        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            if (DataContext is ChatPageViewModel viewModel)
            {
                viewModel.UpdateWindowState(false, false);
            }
        }

        private void OnWindowStateChanged(WindowState state)
        {
            if (DataContext is ChatPageViewModel viewModel)
            {
                viewModel.UpdateWindowState(true, state == WindowState.Minimized);
            }
        }

        private void OnMentionMemberClick(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is Participant participant)
            {
                if (DataContext is ChatPageViewModel viewModel)
                {
                    viewModel.SelectMentionMemberCommand.Execute(participant);
                }
            }
        }
    }

    public class FirstCharConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str && !string.IsNullOrEmpty(str))
            {
                return str[0].ToString();
            }
            return "?";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TimeConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is DateTime time)
            {
                var now = DateTime.Now;
                var diff = now - time;

                if (diff.TotalMinutes < 1) return "刚刚";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}分钟前";
                if (diff.TotalDays < 1) return time.ToString("HH:mm");
                if (diff.TotalDays < 7) return time.ToString("ddd HH:mm");
                return time.ToString("MM/dd");
            }
            return "";
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CountToBoolConverter : Avalonia.Data.Converters.IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
