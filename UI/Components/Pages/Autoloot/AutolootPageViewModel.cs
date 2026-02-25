using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Services;
using Jido.UI.Components;
using Jido.Utils;

namespace Jido.UI.Components.Pages.Autoloot
{
    public partial class AutolootPageViewModel : ToggleablePageViewModel, IDisposable
    {
        private readonly IAutolootService? _autolootService;

        public AutolootPageViewModel() { }

        public AutolootPageViewModel(IAutolootService autolootService)
            : base(autolootService)
        {
            _autolootService = autolootService;
            _autolootService.StatusChanged += OnAutolootStatusChange;
        }

        private void OnAutolootStatusChange(object? sender, ServiceStatus status)
        { }

        public void Dispose()
        {
            _autolootService!.StatusChanged -= OnAutolootStatusChange;
        }
    }
}
