using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Services;
using Jido.Utils;

namespace Jido.UI.Components
{
    public abstract partial class ToggleablePageViewModel : ViewModelBase
    {
        private readonly IToggleableService? _toggleableService;

        [ObservableProperty]
        private string changeKeyButtonText = "Change";

        [ObservableProperty]
        private KeyCombo toggleKey;

        protected ToggleablePageViewModel() { }

        protected ToggleablePageViewModel(IToggleableService toggleableService)
        {
            _toggleableService = toggleableService;
            ToggleKey = _toggleableService.ToggleKey;
        }

        [RelayCommand]
        private async Task ChangeKey()
        {
            if (_toggleableService is null)
                return;
            ChangeKeyButtonText = "Listening...";
            ToggleKey = await _toggleableService.ChangeToggleKey();
            ChangeKeyButtonText = "Change";
        }
    }
}
