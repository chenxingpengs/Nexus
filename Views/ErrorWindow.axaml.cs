using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Threading.Tasks;

namespace Nexus.Views
{
    public class ErrorWindow : Window
    {
        private readonly string _fullErrorText;

        public ErrorWindow(string errorMessage, string stackTrace)
        {
            Title = "Nexus - 程序错误";
            Width = 650;
            Height = 550;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            CanResize = true;
            Background = new SolidColorBrush(Color.Parse("#F5F5F5"));

            _fullErrorText = $"错误信息：{errorMessage}\n\n堆栈跟踪：\n{stackTrace}";

            var mainPanel = new Grid
            {
                RowDefinitions = RowDefinitions.Parse("Auto,*"),
                Margin = new Thickness(0)
            };

            var headerPanel = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#1976D2")),
                Padding = new Thickness(15, 10),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children =
                    {
                        CreateLogo(),
                        new TextBlock
                        {
                            Text = "珠海市红旗中学",
                            FontSize = 18,
                            FontWeight = FontWeight.Bold,
                            Foreground = Brushes.White,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
            Grid.SetRow(headerPanel, 0);
            mainPanel.Children.Add(headerPanel);

            var contentPanel = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 15
            };

            var errorIcon = new TextBlock
            {
                Text = "⚠",
                FontSize = 48,
                Foreground = new SolidColorBrush(Color.Parse("#F44336")),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            contentPanel.Children.Add(errorIcon);

            var errorTitle = new TextBlock
            {
                Text = "程序严重损坏",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#333333")),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            contentPanel.Children.Add(errorTitle);

            var errorHint = new TextBlock
            {
                Text = "请重新运行安装程序或联系管理员处理",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.Parse("#666666")),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };
            contentPanel.Children.Add(errorHint);

            var errorDetailsBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                BorderBrush = new SolidColorBrush(Color.Parse("#E0E0E0")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10),
                MaxHeight = 200
            };

            var errorDetailsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            };

            var errorDetails = new TextBlock
            {
                Text = _fullErrorText,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                Foreground = new SolidColorBrush(Color.Parse("#333333")),
                TextWrapping = TextWrapping.Wrap
            };
            errorDetailsScroll.Content = errorDetails;
            errorDetailsBorder.Child = errorDetailsScroll;
            contentPanel.Children.Add(errorDetailsBorder);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var copyButton = new Button
            {
                Content = "复制错误信息",
                Padding = new Thickness(20, 8),
                Background = new SolidColorBrush(Color.Parse("#2196F3")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            copyButton.Click += async (s, e) =>
            {
                try
                {
                    await Clipboard.SetTextAsync(_fullErrorText);
                    copyButton.Content = "已复制！";
                    await Task.Delay(1500);
                    copyButton.Content = "复制错误信息";
                }
                catch { }
            };
            buttonPanel.Children.Add(copyButton);

            var closeButton = new Button
            {
                Content = "关闭程序",
                Padding = new Thickness(20, 8),
                Background = new SolidColorBrush(Color.Parse("#F44336")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            closeButton.Click += (s, e) => Close();
            buttonPanel.Children.Add(closeButton);

            contentPanel.Children.Add(buttonPanel);

            Grid.SetRow(contentPanel, 1);
            mainPanel.Children.Add(contentPanel);

            Content = mainPanel;
        }

        private Control CreateLogo()
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://Nexus/Assets/hqzx.png"));
                if (stream != null)
                {
                    var bitmap = new Bitmap(stream);
                    return new Image
                    {
                        Source = bitmap,
                        Width = 32,
                        Height = 32,
                        Stretch = Stretch.Uniform
                    };
                }
            }
            catch { }

            return new Border
            {
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(Color.Parse("#FFFFFF")),
                CornerRadius = new CornerRadius(4)
            };
        }
    }
}
