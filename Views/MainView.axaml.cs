using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Nexus.Views
{
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
        }

        public void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }
    }
}
