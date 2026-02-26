using System;
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
        private double _maxClicksPerSecond;

        [ObservableProperty]
        private double _captureRatio;

        [ObservableProperty]
        private string _avgCycleMs = "—";

        public AutolootPageViewModel() { }

        public AutolootPageViewModel(IAutolootService autolootService)
            : base(autolootService)
        {
            _autolootService = autolootService;
            MaxClicksPerSecond = autolootService.MaxClicksPerSecond;
            CaptureRatio = autolootService.CaptureRatio;
            autolootService.AverageCycleMsUpdated += OnAverageCycleMsUpdated;
        }

        private void OnAverageCycleMsUpdated(object? sender, double ms) =>
            AvgCycleMs = $"{ms:F1} ms";

        [RelayCommand]
        private void SaveAutolootConfig()
        {
            _autolootService?.UpdateConfig(MaxClicksPerSecond, CaptureRatio);
        }

        public void Dispose()
        {
            if (_autolootService is not null)
                _autolootService.AverageCycleMsUpdated -= OnAverageCycleMsUpdated;
        }
    }
}
