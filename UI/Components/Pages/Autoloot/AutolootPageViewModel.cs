using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Services;
using Jido.UI.Components;
using Jido.Utils;

namespace Jido.UI.Components.Pages.Autoloot
{
    public partial class AutolootPageViewModel : ToggleablePageViewModel, IDisposable
    {
        private readonly IAutolootService? _autolootService;

        [ObservableProperty]
        private int _cycleDelayMs;

        [ObservableProperty]
        private int _captureRatioPercent;

        [ObservableProperty]
        private string _avgCycleMs = "—";

        public SaveFeedback SaveState { get; } = new();

        public AutolootPageViewModel() { }

        public AutolootPageViewModel(IAutolootService autolootService)
            : base(autolootService)
        {
            _autolootService = autolootService;
            CycleDelayMs = autolootService.CycleDelayMs;
            CaptureRatioPercent = (int)Math.Round(autolootService.CaptureRatio * 100);
            autolootService.AverageCycleMsUpdated += OnAverageCycleMsUpdated;
        }

        // Raised from the autoloot routine thread, so pass it to the UI one
        private void OnAverageCycleMsUpdated(object? sender, double ms) =>
            Dispatcher.UIThread.Post(() => AvgCycleMs = $"{ms:F1} ms");

        [RelayCommand]
        private void SaveAutolootConfig()
        {
            if (_autolootService is null) return;
            _autolootService.UpdateConfig(CycleDelayMs, CaptureRatioPercent / 100.0);
            SaveState.Flash();
        }

        public void Dispose()
        {
            if (_autolootService is not null)
                _autolootService.AverageCycleMsUpdated -= OnAverageCycleMsUpdated;
        }
    }
}
