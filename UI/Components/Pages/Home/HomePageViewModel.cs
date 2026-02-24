using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Services;
using Jido.UI.Components;
using Jido.Utils;
using SharpHook.Native;

namespace Jido.UI.Components.Pages.Home
{
    public partial class HomePageViewModel : ViewModelBase
    {
        private readonly IMacroService? _macroService;

        [ObservableProperty]
        private KeyCode toggleKey;

        [ObservableProperty]
        private string changeKeyButtonText;

        public HomePageViewModel()
        {
            ChangeKeyButtonText = "Change";
        }

        public HomePageViewModel(IMacroService macroService)
        {
            _macroService = macroService;
            ToggleKey = _macroService.ToggleKey;
            ChangeKeyButtonText = "Change";
        }

        [RelayCommand]
        private void ChangeKey()
        {
            if (_macroService is null)
                return;

            ChangeKeyButtonText = "Listening...";
            _macroService
                .ChangeToggleKey()
                .ContinueWith(task =>
                {
                    ToggleKey = task.Result;
                    ChangeKeyButtonText = "Change";
                });
        }
    }
}
