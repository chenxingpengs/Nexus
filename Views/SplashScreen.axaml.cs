using Avalonia.Controls;
using Nexus.ViewModels;

namespace Nexus.Views
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            
            Closing += OnWindowClosing;
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is SplashScreenViewModel viewModel)
            {
                if (!viewModel.CanClose)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
