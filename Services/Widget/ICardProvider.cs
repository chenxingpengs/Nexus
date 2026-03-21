using System.Threading.Tasks;
using Nexus.Models.Widget;

namespace Nexus.Services.Widget
{
    public interface ICardProvider<T> where T : WidgetCard
    {
        CardType CardType { get; }
        Task<T> GetInitialDataAsync();
        Task<T> RefreshDataAsync();
        void HandleUpdate(object payload);
    }
}
