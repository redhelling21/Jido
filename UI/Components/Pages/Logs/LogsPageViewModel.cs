using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Utils.Logging;
using Microsoft.Extensions.Logging;

namespace Jido.UI.Components.Pages.Logs
{
    public class LogEntryViewModel
    {
        public string Text { get; }
        public IBrush Foreground { get; }
        public LogLevel Level { get; }

        public LogEntryViewModel(LogEntry entry)
        {
            Level = entry.Level;
            var category = entry.Category.Split('.')[^1];
            Text = $"[{entry.Timestamp:HH:mm:ss.fff}] [{LevelLabel(entry.Level)}] {category}: {entry.Message}";
            // Color the text of the log depending on its level
            Foreground = new SolidColorBrush(
                entry.Level switch
                {
                    LogLevel.Debug => Color.Parse("#777777"),
                    LogLevel.Information => Color.Parse("#222222"),
                    LogLevel.Warning => Color.Parse("#CC6600"),
                    LogLevel.Error or LogLevel.Critical => Color.Parse("#CC2222"),
                    _ => Color.Parse("#222222")
                }
            );
        }

        private static string LevelLabel(LogLevel level) =>
            level switch
            {
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRT",
                _ => "???"
            };
    }

    public partial class LogsPageViewModel : ViewModelBase, IDisposable
    {
        private readonly InMemoryLoggerProvider? _provider;
        private readonly List<LogEntryViewModel> _allEntries = new();

        [ObservableProperty]
        private bool _showDebug = false;

        [ObservableProperty]
        private bool _showInfo = true;

        [ObservableProperty]
        private bool _showWarning = true;

        [ObservableProperty]
        private bool _showError = true;

        public ObservableCollection<LogEntryViewModel> DisplayedEntries { get; } = new();

        public LogsPageViewModel() { }

        public LogsPageViewModel(InMemoryLoggerProvider provider)
        {
            _provider = provider;
            // Replay entries that arrived before this ViewModel was created
            foreach (var entry in provider.GetSnapshot())
            {
                var vm = new LogEntryViewModel(entry);
                _allEntries.Add(vm);
                if (IsVisible(vm))
                    DisplayedEntries.Add(vm);
            }
            provider.EntryAdded += OnEntryAdded;
        }

        private void OnEntryAdded(LogEntry entry)
        {
            // LogEntryViewModel allocates a SolidColorBrush, which must happen on the UI thread.
            Dispatcher.UIThread.Post(() =>
            {
                var vm = new LogEntryViewModel(entry);
                _allEntries.Add(vm);
                if (IsVisible(vm))
                    DisplayedEntries.Add(vm);

                // Keep this copy bounded like the provider's buffer
                if (_allEntries.Count > InMemoryLoggerProvider.MaxEntries)
                {
                    var evicted = _allEntries[0];
                    _allEntries.RemoveAt(0);
                    DisplayedEntries.Remove(evicted);
                }
            });
        }

        private bool IsVisible(LogEntryViewModel entry) =>
            entry.Level switch
            {
                LogLevel.Debug => ShowDebug,
                LogLevel.Information => ShowInfo,
                LogLevel.Warning => ShowWarning,
                LogLevel.Error or LogLevel.Critical => ShowError,
                _ => true
            };

        partial void OnShowDebugChanged(bool value) => RebuildFilter();

        partial void OnShowInfoChanged(bool value) => RebuildFilter();

        partial void OnShowWarningChanged(bool value) => RebuildFilter();

        partial void OnShowErrorChanged(bool value) => RebuildFilter();

        private void RebuildFilter()
        {
            DisplayedEntries.Clear();
            foreach (var entry in _allEntries)
                if (IsVisible(entry))
                    DisplayedEntries.Add(entry);
        }

        [RelayCommand]
        private void Clear()
        {
            _allEntries.Clear();
            DisplayedEntries.Clear();
        }

        public void Dispose()
        {
            if (_provider is not null)
                _provider.EntryAdded -= OnEntryAdded;
        }
    }
}
