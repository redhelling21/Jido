using AutoMapper;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Config;
using Jido.Services;
using SharpHook.Data;

namespace Jido.UI.Components.Pages.InventoryManagement
{
    public partial class InventoryManagementPageViewModel : ViewModelBase
    {
        private IInventoryManagementService _inventoryService;
        private InventoryOverlay? _inventoryOverlay;
        private InventoryOverlayData _inventoryOverlayData;

        [ObservableProperty]
        private string _inventoryConfigButtonText = "Configure";

        [ObservableProperty]
        private KeyCode _emptyInventoryKey;

        public InventoryManagementPageViewModel()
        { }

        public InventoryManagementPageViewModel(IInventoryManagementService inventoryService, IMapper mapper)
        {
            _inventoryService = inventoryService;
            _emptyInventoryKey = _inventoryService.Config.EmptyInventoryKey;
            _inventoryOverlayData = new InventoryOverlayData
            {
                InventoryHeight = _inventoryService.Config.InventoryHeight,
                InventoryWidth = _inventoryService.Config.InventoryWidth,
                InventorySlots = _inventoryService.Config.InventorySlots,
                InventoryPosition = _inventoryService.Config.InventoryPosition,
            };
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
            // Get the updated config
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
                InventoryHeight = _inventoryOverlayData.InventoryHeight,
                InventoryWidth = _inventoryOverlayData.InventoryWidth,
                InventorySlots = _inventoryOverlayData.InventorySlots,
                InventoryPosition = _inventoryOverlayData.InventoryPosition,
            };
            _inventoryService.UpdateConfig(config);
            // Screenshot the current (hopefully empty) inventory for comparison when running the routine
            _inventoryService.CaptureAndSaveEmptyReference();
        }

        #endregion commands
    }
}
