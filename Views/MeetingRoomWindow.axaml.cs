using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Nexus.ViewModels;
using System;

namespace Nexus.Views
{
    public partial class MeetingRoomWindow : Window
    {
        private MeetingRoomViewModel? _viewModel;

        public MeetingRoomWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = DataContext as MeetingRoomViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                
                if (_viewModel.VideoFrame != null)
                {
                    VideoImage.Source = _viewModel.VideoFrame;
                }
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MeetingRoomViewModel.VideoFrame))
            {
                if (_viewModel?.VideoFrame != null)
                {
                    VideoImage.Source = _viewModel.VideoFrame;
                }
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
            base.OnClosing(e);
        }
    }
}
