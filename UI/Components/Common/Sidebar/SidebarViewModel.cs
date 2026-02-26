using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Services;
using Jido.UI.Routing;
using Jido.Utils;

namespace Jido.UI.Components.Common.Sidebar
{
    public partial class SidebarViewModel : ViewModelBase, IDisposable
    {
        private Router<ViewModelBase> _router = default!;
        private IServiceHub? _serviceHub;

        [ObservableProperty]
        private ServiceStatus _macroStatus = ServiceStatus.STOPPED;

        [ObservableProperty]
        private ServiceStatus _autolootStatus = ServiceStatus.STOPPED;

        [ObservableProperty]
        private ServiceStatus _autopressStatus = ServiceStatus.STOPPED;

        [ObservableProperty]
        private ServiceStatus _inventoryManagementStatus = ServiceStatus.STOPPED;

        [RelayCommand]
        public void NavigateTo(string path)
        {
            _router.GoTo(path);
        }

        public SidebarViewModel() { }

        public SidebarViewModel(IServiceHub serviceHub, Router<ViewModelBase> router)
        {
            _router = router;
            _serviceHub = serviceHub;
            serviceHub.AnyStatusChanged += OnAnyStatusChanged;
        }

        private void OnAnyStatusChanged(object? sender, ServiceStatusChangedEventArgs e)
        {
            switch (e.ServiceName)
            {
                case ServiceNames.Macro: MacroStatus = e.Status; break;
                case ServiceNames.Autoloot: AutolootStatus = e.Status; break;
                case ServiceNames.Autopress: AutopressStatus = e.Status; break;
                case ServiceNames.InventoryManagement: InventoryManagementStatus = e.Status; break;
            }
        }

        public void Dispose()
        {
            if (_serviceHub is not null)
                _serviceHub.AnyStatusChanged -= OnAnyStatusChanged;
        }
    }
}
