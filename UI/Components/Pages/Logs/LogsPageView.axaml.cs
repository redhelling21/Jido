using System;
using System.Collections.Specialized;
using Avalonia.Controls;

namespace Jido.UI.Components.Pages.Logs;

public partial class LogsPageView : UserControl
{
    private LogsPageViewModel? _viewModel;

    public LogsPageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.DisplayedEntries.CollectionChanged -= OnEntriesChanged;

        _viewModel = DataContext as LogsPageViewModel;

        if (_viewModel is not null)
            _viewModel.DisplayedEntries.CollectionChanged += OnEntriesChanged;
    }

    // Roughly one row: enough slack that "at the bottom" survives the entry that just arrived
    private const double StickToBottomTolerancePx = 40;

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _viewModel is null)
            return;

        var list = this.FindControl<ListBox>("LogList");
        if (list is null)
            return;

        // Only follow the tail when the user is already at it
        var scroll = list.Scroll;
        if (scroll is not null
            && scroll.Offset.Y < scroll.Extent.Height - scroll.Viewport.Height - StickToBottomTolerancePx)
            return;

        var count = _viewModel.DisplayedEntries.Count;
        if (count > 0)
            list.ScrollIntoView(count - 1);
    }
}
