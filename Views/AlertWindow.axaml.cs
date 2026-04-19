using System;
using Avalonia.Controls;
using Avalonia.Media;
using Nexus.Models;

namespace Nexus.Views
{
    public partial class AlertWindow : UserControl
    {
        private string _notificationId = string.Empty;

        public event EventHandler<string>? AlertClosed;

        public AlertWindow()
        {
            InitializeComponent();
        }

        public void ShowAlert(Notification notification)
        {
            _notificationId = notification.Id;
            TitleText.Text = notification.Title;
            ContentText.Text = notification.Content;

            var backgroundColor = notification.BackgroundColor;
            if (TryParseColor(backgroundColor, out var color))
            {
                var border = this.FindControl<Border>("MainBorder");
                if (border != null)
                {
                    border.Background = new SolidColorBrush(color);
                }
            }
        }

        private bool TryParseColor(string colorString, out Color color)
        {
            color = Colors.DodgerBlue;

            try
            {
                if (colorString.StartsWith("#"))
                {
                    color = Color.Parse(colorString);
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private void ConfirmButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            AlertClosed?.Invoke(this, _notificationId);
        }
    }
}
