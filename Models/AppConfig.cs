using System;
using Nexus.Models.Widget;

namespace Nexus.Models
{
    public class AppConfig
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = "classroom_terminal";
        public string? AppVersion { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? TokenExpiresAt { get; set; }
        public BindInfo? BindInfo { get; set; }
        public string ServerUrl { get; set; } = "http://localhost:5000";
        public WidgetConfig? WidgetConfig { get; set; }

        public bool IsBound => !string.IsNullOrEmpty(AccessToken) && BindInfo != null;
    }

    public class BindInfo
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public DateTime BindTime { get; set; }
    }
}
