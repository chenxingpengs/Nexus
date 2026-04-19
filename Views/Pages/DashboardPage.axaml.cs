using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Nexus.Services;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Pages
{
    public partial class DashboardPage : UserControl
    {
        private DashboardViewModel? _viewModel;

        public DashboardPage() : this(null)
        {
        }

        public DashboardPage(SocketIOService? socketIOService)
        {
            InitializeComponent();

            _viewModel = new DashboardViewModel(socketIOService);
            _viewModel.ShowNotification += OnShowNotification;
            DataContext = _viewModel;
        }

        private async void OnShowNotification(object? sender, string message)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                var parentWindow = topLevel as Window;
                if (parentWindow == null)
                {
                    parentWindow = this.FindAncestorOfType<Window>();
                }

                var dialog = new Window
                {
                    Title = "系统通知",
                    Content = new TextBlock
                    {
                        Text = message,
                        Margin = new Thickness(20),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        MinWidth = 200
                    },
                    SizeToContent = SizeToContent.WidthAndHeight,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                if (parentWindow != null)
                {
                    await dialog.ShowDialog(parentWindow);
                }
                else
                {
                    dialog.Show();
                }
            }
        }

        protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            _viewModel?.Cleanup();
        }
    }
}
