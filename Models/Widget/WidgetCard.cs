using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.Models.Widget
{
    public enum CardType
    {
        Weather,
        Announcement,
        Attendance,
        Shortcut,
        Custom
    }

    public class WidgetCard : ObservableObject
    {
        private string _id = string.Empty;
        private CardType _type;
        private string _title = string.Empty;
        private bool _isVisible = true;
        private int _order;
        private DateTime _lastUpdated;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public CardType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public int Order
        {
            get => _order;
            set => SetProperty(ref _order, value);
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }
    }
}
