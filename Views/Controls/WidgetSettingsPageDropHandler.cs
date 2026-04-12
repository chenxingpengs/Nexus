using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Nexus.ViewModels.Pages;

namespace Nexus.Views.Controls
{
    public class WidgetSettingsPageDropHandler(WidgetSettingsViewModel viewModel) : DropHandlerBase, IDragHandler
    {
        public WidgetSettingsViewModel ViewModel { get; } = viewModel;

        public override void Enter(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
        {
            e.DragEffects = sourceContext is CardConfigViewModel ? DragDropEffects.Move : DragDropEffects.None;
        }

        public override void Drop(object? sender, DragEventArgs e, object? sourceContext, object? targetContext)
        {
            if (targetContext is not ObservableCollection<CardConfigViewModel> items ||
                sender is not ListBox listBox)
                return;

            if (sourceContext is not CardConfigViewModel draggedItem)
                return;

            var targetIndex = GetTargetIndex(listBox, e, items);
            if (targetIndex < 0) return;

            var oldIndex = items.IndexOf(draggedItem);
            if (oldIndex < 0 || oldIndex == targetIndex) return;

            ViewModel.MoveCard(oldIndex, targetIndex);
        }

        public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
        {
            return sourceContext is CardConfigViewModel &&
                   targetContext is ObservableCollection<CardConfigViewModel>;
        }

        public void BeforeDragDrop(object? sender, PointerEventArgs e, object? context)
        {
        }

        public void AfterDragDrop(object? sender, PointerEventArgs e, object? context)
        {
        }

        private static int GetTargetIndex(ListBox listBox, DragEventArgs e, IList<CardConfigViewModel> items)
        {
            var pos = e.GetPosition(listBox);
            
            if (listBox.GetVisualAt(pos) is Control targetControl &&
                targetControl.FindAncestorOfType<ListBoxItem>() is {} listBoxItem &&
                listBoxItem.DataContext is CardConfigViewModel targetItem)
            {
                var rPos = e.GetPosition(listBoxItem);
                var index = items.IndexOf(targetItem);
                if (index >= 0)
                    return rPos.Y <= listBoxItem.Bounds.Height / 2 ? index : index + 1;
            }

            return items.Count > 0 ? items.Count : -1;
        }
    }
}
