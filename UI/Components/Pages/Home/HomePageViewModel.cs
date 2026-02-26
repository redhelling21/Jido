using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Config;
using Jido.Services;
using Jido.UI.Components;

namespace Jido.UI.Components.Pages.Home
{
    public partial class HomePageViewModel : ToggleablePageViewModel
    {
        private readonly JidoConfig? _config;

        [ObservableProperty]
        private int _screenWidth;

        [ObservableProperty]
        private int _screenHeight;

        public HomePageViewModel() { }

        public HomePageViewModel(IMacroService macroService, JidoConfig config)
            : base(macroService)
        {
            _config = config;
            ScreenWidth = config.Screen.Width;
            ScreenHeight = config.Screen.Height;
        }

        [RelayCommand]
        private void SaveScreenConfig()
        {
            if (_config is null)
                return;
            _config.Screen.Width = ScreenWidth;
            _config.Screen.Height = ScreenHeight;
            _config.Persist();
        }
    }
}
