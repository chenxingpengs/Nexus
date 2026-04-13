using Avalonia.Controls;
using Nexus.ViewModels;
using System.ComponentModel;

namespace Nexus.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                if (viewModel.IsShowScheduleIncomplete && !viewModel.CanClose)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}
