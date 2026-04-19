using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nexus.Models.Widget
{
    public class ShortcutCardModel : WidgetCard
    {
        private ObservableCollection<UsbDriveItem> _usbDrives = new();
        private bool _hasDocumentCamera;
        private string _documentCameraName = "";
        private DocumentCameraType _documentCameraType;

        public ShortcutCardModel()
        {
            Type = CardType.Shortcut;
            Title = "快捷栏";
        }

        public ObservableCollection<UsbDriveItem> UsbDrives
        {
            get => _usbDrives;
            set => SetProperty(ref _usbDrives, value);
        }

        public bool HasUsbDrives => UsbDrives.Count > 0;

        public bool HasDocumentCamera
        {
            get => _hasDocumentCamera;
            set => SetProperty(ref _hasDocumentCamera, value);
        }

        public string DocumentCameraName
        {
            get => _documentCameraName;
            set => SetProperty(ref _documentCameraName, value);
        }

        public DocumentCameraType DocumentCameraType
        {
            get => _documentCameraType;
            set => SetProperty(ref _documentCameraType, value);
        }

        public void UpdateUsbDrives(ObservableCollection<UsbDriveItem> drives)
        {
            UsbDrives = drives;
            OnPropertyChanged(nameof(HasUsbDrives));
        }
    }

    public class UsbDriveItem : ObservableObject
    {
        private string _name = "";
        private string _driveLetter = "";
        private string _totalSize = "";
        private string _availableSpace = "";

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string DriveLetter
        {
            get => _driveLetter;
            set => SetProperty(ref _driveLetter, value);
        }

        public string TotalSize
        {
            get => _totalSize;
            set => SetProperty(ref _totalSize, value);
        }

        public string AvailableSpace
        {
            get => _availableSpace;
            set => SetProperty(ref _availableSpace, value);
        }

        public string DisplayName => $"{DriveLetter} {Name}";

        public string SizeInfo => $"{AvailableSpace} 可用 / {TotalSize}";
    }

    public enum DocumentCameraType
    {
        None,
        Seewo,
        Honghe
    }

    public enum ShortcutType
    {
        Usb,
        DocumentCamera
    }
}
