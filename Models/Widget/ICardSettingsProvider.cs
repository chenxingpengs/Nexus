using Avalonia.Controls;
using Nexus.Models.Widget;

namespace Nexus.Models.Widget
{
    public interface ICardSettingsProvider
    {
        CardType CardType { get; }
        string SettingsTitle { get; }
        Control CreateSettingsPanel();
    }
}
