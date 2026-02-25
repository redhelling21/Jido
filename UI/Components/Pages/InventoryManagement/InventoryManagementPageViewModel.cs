using AutoMapper;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Config;
using Jido.Services;
using SharpHook.Native;

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
        {
        }

        public InventoryManagementPageViewModel(IInventoryManagementService autopressService, IMapper mapper)
        {
            _inventoryService = autopressService;
            _emptyInventoryKey = _inventoryService.Config.EmptyInventoryKey;
            _inventoryOverlayData = new InventoryOverlayData
            {
                InventoryHeight = _inventoryService.Config.InventoryHeight,
                InventoryWidth = _inventoryService.Config.InventoryWidth,
                InventorySlots = _inventoryService.Config.InventorySlots,
                InventoryPosition = _inventoryService.Config.InventoryPosition
            };
        }

        #region commands

        [RelayCommand]
        private void ConfigureInventoryLayout()
        {
            if (_inventoryOverlay == null)
            {
                _inventoryOverlay = new InventoryOverlay(_inventoryOverlayData);
                _inventoryOverlay.Show();
                InventoryConfigButtonText = "Save";
            }
            else
            {
                _inventoryOverlayData = _inventoryOverlay.GetInventoryConfig();
                _inventoryOverlay.Close();
                _inventoryOverlay = null;
                InventoryConfigButtonText = "Configure";
            }
        }

        [RelayCommand]
        private void SaveConfig()
        {
            var config = new InventoryManagementConfig();
            config.EmptyInventoryKey = _emptyInventoryKey;
            config.InventoryHeight = _inventoryOverlayData.InventoryHeight;
            config.InventoryWidth = _inventoryOverlayData.InventoryWidth;
            config.InventorySlots = _inventoryOverlayData.InventorySlots;
            config.InventoryPosition = _inventoryOverlayData.InventoryPosition;
            _inventoryService.UpdateConfig(config);
        }

        #endregion commands
    }
}
