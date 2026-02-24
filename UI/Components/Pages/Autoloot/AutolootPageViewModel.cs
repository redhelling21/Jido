using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Models;
using Jido.Services;
using Jido.UI.Components;
using Jido.Utils;
using SharpHook.Native;

namespace Jido.UI.Components.Pages.Autoloot
{
    public partial class AutolootPageViewModel : ViewModelBase
    {
        private readonly IAutolootService? _autolootService;

        [ObservableProperty]
        private string changeKeyButtonText;

        [ObservableProperty]
        private KeyCode toggleKey;

        public AutolootPageViewModel()
        {
            ChangeKeyButtonText = "Change";
        }

        public AutolootPageViewModel(IAutolootService autolootService)
        {
            _autolootService = autolootService;
            _autolootService.StatusChanged += OnAutolootStatusChange;
            ToggleKey = _autolootService.ToggleKey;
            ChangeKeyButtonText = "Change";
        }

        private void OnAutolootStatusChange(object? sender, ServiceStatus status)
        { }

        [RelayCommand]
        private void ChangeKey()
        {
            if (_autolootService is not null)
            {
                ChangeKeyButtonText = "Listening...";
                var task = _autolootService.ChangeToggleKey();
                task.ContinueWith(
                    (key) =>
                    {
                        ToggleKey = key.Result;
                        ChangeKeyButtonText = "Change";
                    }
                );
            }
        }
    }
}
