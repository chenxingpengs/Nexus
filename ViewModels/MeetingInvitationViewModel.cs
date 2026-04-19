using CommunityToolkit.Mvvm.Input;
using Nexus.Models.Meeting;
using System.Windows.Input;

namespace Nexus.ViewModels
{
    public class MeetingInvitationViewModel : ViewModelBase
    {
        private readonly MeetingInvitation _invitation;

        public string Title => _invitation.Title;
        public string? HostName => _invitation.HostName;
        public DateTime? InvitedAt => _invitation.InvitedAt;
        public string MeetingId => _invitation.MeetingId;
        public int BroadcastPort => _invitation.BroadcastPort;
        public string? HostIp => _invitation.HostIp;

        public ICommand AcceptCommand { get; }
        public ICommand RejectCommand { get; }

        public event Action? Accepted;
        public event Action? Rejected;

        public MeetingInvitationViewModel(MeetingInvitation invitation)
        {
            _invitation = invitation;
            AcceptCommand = new RelayCommand(OnAccept);
            RejectCommand = new RelayCommand(OnReject);
        }

        private void OnAccept()
        {
            Accepted?.Invoke();
        }

        private void OnReject()
        {
            Rejected?.Invoke();
        }
    }
}
