using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Nexus.Models.Widget;
using Nexus.Services;

namespace Nexus.Views.Widget.Cards
{
    public partial class ShortcutCardView : UserControl
    {
        private ShortcutService? _shortcutService;

        public ShortcutCardView()
        {
            InitializeComponent();
            
            UsbButton.Click += UsbButton_Click;
            DocumentCameraButton.Click += DocumentCameraButton_Click;
        }

        public void SetShortcutService(ShortcutService shortcutService)
        {
            _shortcutService = shortcutService;
        }

        private void UsbButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_shortcutService == null)
                return;

            var model = DataContext as ShortcutCardModel;
            if (model == null || model.UsbDrives.Count == 0)
                return;

            var flyout = new MenuFlyout();
            
            foreach (var drive in model.UsbDrives)
            {
                var item = new MenuItem
                {
                    Header = drive.DisplayName,
                    Tag = drive.DriveLetter
                };
                ToolTip.SetTip(item, drive.SizeInfo);
                item.Click += (s, args) =>
                {
                    if (s is MenuItem menuItem && menuItem.Tag is string driveLetter)
                    {
                        _shortcutService.OpenUsbDrive(driveLetter);
                    }
                };
                flyout.Items.Add(item);
            }

            if (sender is Button button)
            {
                flyout.ShowAt(button);
            }
        }

        private void DocumentCameraButton_Click(object? sender, RoutedEventArgs e)
        {
            _shortcutService?.OpenDocumentCamera();
        }
    }
}
