using System.Threading.Tasks;
using AutoMapper;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Config;
using Jido.Services;
using Jido.Utils;

namespace Jido.UI.Components.Pages.InventoryManagement
{
    public partial class InventoryManagementPageViewModel : ViewModelBase
    {
        private IInventoryManagementService _inventoryService;
        private IFillInventoryService _fillService;
        private InventoryOverlay? _inventoryOverlay;
        private InventoryOverlayData _inventoryOverlayData;

        [ObservableProperty]
        private string _inventoryConfigButtonText = "Configure";

        [ObservableProperty]
        private KeyCombo _emptyInventoryKey;

        [ObservableProperty]
        private int _inventoryClickDelayMs;

        [ObservableProperty]
        private KeyCombo _fillInventoryKey;

        [ObservableProperty]
        private string _emptyChangeKeyButtonText = "Change";

        private bool _isEmptyListening;

        [ObservableProperty]
        private string _fillChangeKeyButtonText = "Change";

        private bool _isFillListening;

        [ObservableProperty]
        private int _fillClickDelayMs;

        public InventoryManagementPageViewModel()
        { }

        public InventoryManagementPageViewModel(IInventoryManagementService inventoryService, IFillInventoryService fillService, IMapper mapper)
        {
            _inventoryService = inventoryService;
            _fillService = fillService;
            _emptyInventoryKey = _inventoryService.Config.EmptyInventoryKey;
            _inventoryClickDelayMs = _inventoryService.Config.ClickDelayMs;
            _inventoryOverlayData = new InventoryOverlayData
            {
                InventoryHeight = _inventoryService.Config.InventoryHeight,
                InventoryWidth = _inventoryService.Config.InventoryWidth,
                InventorySlots = _inventoryService.Config.InventorySlots,
                InventoryPosition = _inventoryService.Config.InventoryPosition,
            };

            _fillInventoryKey = _fillService.Config.ToggleKey;
            _fillClickDelayMs = _fillService.Config.ClickDelayMs;
        }

        #region commands

        [RelayCommand]
        private void ConfigureInventoryLayout()
        {
            if (_inventoryOverlay == null)
            {
                _inventoryOverlay = new InventoryOverlay(_inventoryOverlayData);
                _inventoryOverlay.Closing += OnOverlayClosing;
                _inventoryOverlay.Closed += OnOverlayClosed;
                _inventoryOverlay.Show();
                InventoryConfigButtonText = "Save";
            }
            else
            {
                // Closing triggers OnOverlayClosing which reads back the data
                _inventoryOverlay.Close();
            }
        }

        private void OnOverlayClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_inventoryOverlay != null)
                _inventoryOverlayData = _inventoryOverlay.GetInventoryConfig();
        }

        private void OnOverlayClosed(object? sender, System.EventArgs e)
        {
            _inventoryOverlay = null;
            InventoryConfigButtonText = "Configure";
        }

        [RelayCommand]
        private void SaveConfig()
        {
            var config = new InventoryManagementConfig
            {
                EmptyInventoryKey = EmptyInventoryKey,
                ClickDelayMs = InventoryClickDelayMs,
                InventoryHeight = _inventoryOverlayData.InventoryHeight,
                InventoryWidth = _inventoryOverlayData.InventoryWidth,
                InventorySlots = _inventoryOverlayData.InventorySlots,
                InventoryPosition = _inventoryOverlayData.InventoryPosition,
            };
            _inventoryService.UpdateConfig(config);
            _inventoryService.CaptureAndSaveEmptyReference();
        }

        [RelayCommand(CanExecute = nameof(CanChangeEmptyKey))]
        private async Task ChangeEmptyKey()
        {
            _isEmptyListening = true;
            ChangeEmptyKeyCommand.NotifyCanExecuteChanged();
            EmptyChangeKeyButtonText = "Listening...";
            EmptyInventoryKey = await _inventoryService.ChangeToggleKey();
            EmptyChangeKeyButtonText = "Change";
            _isEmptyListening = false;
            ChangeEmptyKeyCommand.NotifyCanExecuteChanged();
        }

        private bool CanChangeEmptyKey() => !_isEmptyListening;

        [RelayCommand(CanExecute = nameof(CanChangeFillKey))]
        private async Task ChangeFillKey()
        {
            _isFillListening = true;
            ChangeFillKeyCommand.NotifyCanExecuteChanged();
            FillChangeKeyButtonText = "Listening...";
            FillInventoryKey = await _fillService.ChangeToggleKey();
            FillChangeKeyButtonText = "Change";
            _isFillListening = false;
            ChangeFillKeyCommand.NotifyCanExecuteChanged();
        }

        private bool CanChangeFillKey() => !_isFillListening;

        [RelayCommand]
        private void SaveFillConfig()
        {
            _fillService.UpdateConfig(FillClickDelayMs);
        }

        #endregion commands
    }
}
