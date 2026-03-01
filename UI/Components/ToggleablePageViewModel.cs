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
        private bool _isListening;

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

        [RelayCommand(CanExecute = nameof(CanChangeKey))]
        private async Task ChangeKey()
        {
            _isListening = true;
            ChangeKeyCommand.NotifyCanExecuteChanged();
            ChangeKeyButtonText = "Listening...";

            ToggleKey = await _toggleableService!.ChangeToggleKey();

            ChangeKeyButtonText = "Change";
            _isListening = false;
            ChangeKeyCommand.NotifyCanExecuteChanged();
        }

        private bool CanChangeKey() => !_isListening && _toggleableService is not null;
    }
}
