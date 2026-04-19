using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nexus.Models.Widget
{
    public class AnnouncementCardModel : WidgetCard
    {
        private ObservableCollection<AnnouncementItem> _items = new();

        public AnnouncementCardModel()
        {
            Type = CardType.Announcement;
            Title = "公告";
        }

        public ObservableCollection<AnnouncementItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public bool IsEmpty => _items.Count == 0;
    }

    public class AnnouncementItem : ObservableObject
    {
        private string _id = string.Empty;
        private string _title = string.Empty;
        private string _content = string.Empty;
        private DateTime _publishTime;
        private string _priority = "medium";
        private bool _isRead;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public DateTime PublishTime
        {
            get => _publishTime;
            set => SetProperty(ref _publishTime, value);
        }

        public string Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public bool IsRead
        {
            get => _isRead;
            set => SetProperty(ref _isRead, value);
        }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - PublishTime;
                if (diff.TotalMinutes < 1) return "刚刚";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}分钟前";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}小时前";
                return $"{(int)diff.TotalDays}天前";
            }
        }
    }
}
