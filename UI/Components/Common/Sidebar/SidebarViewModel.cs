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
        private IMacroService? _macroService;
        private IAutolootService? _autolootService;
        private IAutopressService? _autopressService;

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

        public SidebarViewModel()
        {
            Console.WriteLine("SidebarViewModel created");
        }

        public SidebarViewModel(
            IMacroService macroService,
            IAutolootService autolootService,
            IAutopressService autopressService,
            Router<ViewModelBase> router
        )
        {
            _router = router;
            _macroService = macroService;
            _autolootService = autolootService;
            _autopressService = autopressService;
            macroService.StatusChanged += OnMacroStatusChanged;
            autolootService.StatusChanged += OnAutolootStatusChanged;
            autopressService.StatusChanged += OnAutopressStatusChanged;
        }

        private void OnMacroStatusChanged(object? sender, ServiceStatus e) => MacroStatus = e;
        private void OnAutolootStatusChanged(object? sender, ServiceStatus e) => AutolootStatus = e;
        private void OnAutopressStatusChanged(object? sender, ServiceStatus e) => AutopressStatus = e;

        public void Dispose()
        {
            if (_macroService is not null)
                _macroService.StatusChanged -= OnMacroStatusChanged;
            if (_autolootService is not null)
                _autolootService.StatusChanged -= OnAutolootStatusChanged;
            if (_autopressService is not null)
                _autopressService.StatusChanged -= OnAutopressStatusChanged;
        }
    }
}
