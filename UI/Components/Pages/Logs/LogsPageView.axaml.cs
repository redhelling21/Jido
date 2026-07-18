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

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || _viewModel is null)
            return;

        var count = _viewModel.DisplayedEntries.Count;
        if (count > 0)
            this.FindControl<ListBox>("LogList")?.ScrollIntoView(count - 1);
    }
}
