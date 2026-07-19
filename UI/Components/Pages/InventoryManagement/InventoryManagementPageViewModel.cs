using System;
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
        private IBulkUseItemService _bulkService;
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

        [ObservableProperty]
        private KeyCombo _bulkUseItemKey;

        [ObservableProperty]
        private string _bulkChangeKeyButtonText = "Change";

        private bool _isBulkListening;

        [ObservableProperty]
        private int _bulkClickDelayMs;

        public SaveFeedback SaveState { get; } = new("Save all");

        public InventoryManagementPageViewModel()
        { }

        public InventoryManagementPageViewModel(
            IInventoryManagementService inventoryService,
            IFillInventoryService fillService,
            IBulkUseItemService bulkService,
            IMapper mapper
        )
        {
            _inventoryService = inventoryService;
            _fillService = fillService;
            _bulkService = bulkService;
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

            _bulkUseItemKey = _bulkService.Config.ToggleKey;
            _bulkClickDelayMs = _bulkService.Config.ClickDelayMs;
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
        private void SaveAll()
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
            _fillService.UpdateConfig(FillClickDelayMs);
            _bulkService.UpdateConfig(BulkClickDelayMs);
            SaveState.Flash();
        }

        [RelayCommand(CanExecute = nameof(CanChangeEmptyKey))]
        private async Task ChangeEmptyKey()
        {
            EmptyInventoryKey = await ListenForKey(
                _inventoryService,
                v => _isEmptyListening = v,
                ChangeEmptyKeyCommand,
                v => EmptyChangeKeyButtonText = v
            );
        }

        private bool CanChangeEmptyKey() => !_isEmptyListening;

        [RelayCommand(CanExecute = nameof(CanChangeFillKey))]
        private async Task ChangeFillKey()
        {
            FillInventoryKey = await ListenForKey(
                _fillService,
                v => _isFillListening = v,
                ChangeFillKeyCommand,
                v => FillChangeKeyButtonText = v
            );
        }

        private bool CanChangeFillKey() => !_isFillListening;

        [RelayCommand(CanExecute = nameof(CanChangeBulkKey))]
        private async Task ChangeBulkKey()
        {
            BulkUseItemKey = await ListenForKey(
                _bulkService,
                v => _isBulkListening = v,
                ChangeBulkKeyCommand,
                v => BulkChangeKeyButtonText = v
            );
        }

        private bool CanChangeBulkKey() => !_isBulkListening;

        private async Task<KeyCombo> ListenForKey(
            IToggleableService service,
            Action<bool> setListening,
            IRelayCommand command,
            Action<string> setButtonText
        )
        {
            setListening(true);
            command.NotifyCanExecuteChanged();
            setButtonText("Listening...");
            var combo = await service.ChangeToggleKey();
            setButtonText("Change");
            setListening(false);
            command.NotifyCanExecuteChanged();
            return combo;
        }

        #endregion commands
    }
}
