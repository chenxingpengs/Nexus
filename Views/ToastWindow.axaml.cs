using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Nexus.Services;

namespace Nexus.Views;

public partial class ToastWindow : Window
{
    public ToastWindow()
    {
        InitializeComponent();
    }

    public void Show(ToastItem toast)
    {
        SetupPosition();
        SetupContent(toast);
        Show();
    }

    private void SetupPosition()
    {
        var screen = Screens.Primary;
        if (screen != null)
        {
            var bounds = screen.Bounds;
            var padding = 20;
            
            Position = new PixelPoint(
                (int)(bounds.X + bounds.Width - Width - padding),
                (int)(bounds.Y + bounds.Height - Height - padding - 60)
            );
        }
    }

    private void SetupContent(ToastItem toast)
    {
        TitleText.Text = toast.Title;
        MessageText.Text = toast.Message;

        var (icon, backgroundColor) = toast.Type switch
        {
            ToastType.Success => ("✓", Color.Parse("#4CAF50")),
            ToastType.Warning => ("⚠", Color.Parse("#FF9800")),
            ToastType.Error => ("✕", Color.Parse("#F44336")),
            ToastType.Info => ("ℹ", Color.Parse("#2196F3")),
            _ => ("ℹ", Color.Parse("#2196F3"))
        };

        IconText.Text = icon;
        ToastContainer.Background = new SolidColorBrush(backgroundColor);
    }
}
