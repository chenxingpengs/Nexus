using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System.Threading.Tasks;

namespace Nexus.Views
{
    public class PasswordDialog : Window
    {
        private TextBox? _passwordTextBox;
        private TextBlock? _errorText;
        private Button? _confirmButton;
        private Button? _cancelButton;
        private string? _password;
        private bool _result;

        public PasswordDialog()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            Title = "密码验证";
            Width = 350;
            Height = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;
            Background = new SolidColorBrush(Color.Parse("#F5F5F5"));

            var mainPanel = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 15
            };

            var titleBlock = new TextBlock
            {
                Text = "请输入密码以退出程序",
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(Color.Parse("#333333")),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            mainPanel.Children.Add(titleBlock);

            _passwordTextBox = new TextBox
            {
                Watermark = "请输入密码",
                PasswordChar = '●',
                Width = 280,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            _passwordTextBox.KeyDown += OnPasswordKeyDown;
            mainPanel.Children.Add(_passwordTextBox);

            _errorText = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.Parse("#F44336")),
                FontSize = 12,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                IsVisible = false
            };
            mainPanel.Children.Add(_errorText);

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            _confirmButton = new Button
            {
                Content = "确认",
                Width = 100,
                Height = 32,
                Background = new SolidColorBrush(Color.Parse("#1976D2")),
                Foreground = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };
            _confirmButton.Click += OnConfirmClick;
            buttonPanel.Children.Add(_confirmButton);

            _cancelButton = new Button
            {
                Content = "取消",
                Width = 100,
                Height = 32,
                Background = new SolidColorBrush(Color.Parse("#E0E0E0")),
                Foreground = new SolidColorBrush(Color.Parse("#333333")),
                CornerRadius = new CornerRadius(4)
            };
            _cancelButton.Click += OnCancelClick;
            buttonPanel.Children.Add(_cancelButton);

            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;

            Opened += OnOpened;
        }

        private void OnOpened(object? sender, System.EventArgs e)
        {
            _passwordTextBox?.Focus();
        }

        private void OnPasswordKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnConfirmClick(sender, null);
            }
            else if (e.Key == Key.Escape)
            {
                OnCancelClick(sender, null);
            }
        }

        private void OnConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
        {
            _password = _passwordTextBox?.Text;
            _result = true;
            Close();
        }

        private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs? e)
        {
            _result = false;
            Close();
        }

        public void ShowError(string message)
        {
            if (_errorText != null)
            {
                _errorText.Text = message;
                _errorText.IsVisible = true;
            }
        }

        public static async Task<(bool Success, string? Password)> ShowDialogAsync(Window owner, string title = "密码验证")
        {
            var dialog = new PasswordDialog
            {
                Title = title
            };
            
            await dialog.ShowDialog(owner);
            
            return (dialog._result, dialog._password);
        }
    }
}
