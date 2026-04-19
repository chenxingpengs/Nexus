using Avalonia.Controls;
using Nexus.Models.Meeting;
using Nexus.ViewModels;

namespace Nexus.Views
{
    public partial class MeetingInvitationWindow : Window
    {
        private readonly MeetingInvitationViewModel _viewModel;

        public MeetingInvitationWindow()
        {
            InitializeComponent();
        }

        public MeetingInvitationWindow(MeetingInvitation invitation) : this()
        {
            _viewModel = new MeetingInvitationViewModel(invitation);
            _viewModel.Accepted += OnAccepted;
            _viewModel.Rejected += OnRejected;
            DataContext = _viewModel;
        }

        private void OnAccepted()
        {
            Close(true);
        }

        private void OnRejected()
        {
            Close(false);
        }
    }
}
