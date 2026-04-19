using CommunityToolkit.Mvvm.ComponentModel;
using Nexus.Models.Widget;

namespace Nexus.ViewModels.Widget.Cards
{
    public partial class AnnouncementCardViewModel : ObservableObject
    {
        private readonly AnnouncementCardModel _model;

        public AnnouncementCardViewModel(AnnouncementCardModel model)
        {
            _model = model;
        }

        public bool IsEmpty => _model.Items.Count == 0;
        
        public AnnouncementCardModel Model => _model;

        public void UpdateModel(AnnouncementCardModel model)
        {
            _model.Items.Clear();
            foreach (var item in model.Items)
            {
                _model.Items.Add(item);
            }
            OnPropertyChanged(nameof(IsEmpty));
        }
    }
}
