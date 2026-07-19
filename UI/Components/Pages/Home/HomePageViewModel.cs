using System;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using Jido.Config;
using Jido.Services;
using Jido.UI.Components;
using Jido.Utils;

namespace Jido.UI.Components.Pages.Home
{
    public partial class HomePageViewModel : ToggleablePageViewModel
    {
        private readonly JidoConfig? _config;

        public string DetectedResolution => $"{ScreenUtils.PrimaryWidth} × {ScreenUtils.PrimaryHeight} px";

        public AppTheme[] Themes { get; } = Enum.GetValues<AppTheme>();

        [ObservableProperty]
        private AppTheme _selectedTheme;

        public HomePageViewModel() { }

        public HomePageViewModel(IMacroService macroService, JidoConfig config)
            : base(macroService)
        {
            _config = config;
            SelectedTheme = config.Theme;
        }

        partial void OnSelectedThemeChanged(AppTheme value)
        {
            if (_config is null || _config.Theme == value)
                return;

            Application.Current!.RequestedThemeVariant = value.ToVariant();
            _config.Theme = value;
            _config.Persist();
        }
    }
}
