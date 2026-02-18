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

        public ObservableCollection<Color> ColorItems { get; } = new ObservableCollection<Color>();

        public AutolootPageViewModel()
        {
            ChangeKeyButtonText = "Change";
            ColorItems = new ObservableCollection<Color>(
                new List<Color>()
                {
                    new() { Name = "Red", RGB = [255, 0, 0] },
                    new() { Name = "Green", RGB = [0, 255, 0] },
                    new() { Name = "Blue", RGB = [0, 0, 255] },
                }
            );
        }

        public AutolootPageViewModel(IAutolootService autolootService)
        {
            _autolootService = autolootService;
            _autolootService.StatusChanged += OnAutolootStatusChange;
            ToggleKey = _autolootService.ToggleKey;
            foreach (var color in _autolootService.Colors)
            {
                color.PropertyChanged += OnColorChanged;
                ColorItems.Add(color);
            }
            ChangeKeyButtonText = "Change";
        }

        private void OnAutolootStatusChange(object? sender, ServiceStatus status)
        { }

        private void OnColorChanged(object? sender, EventArgs e)
        {
            if (_autolootService is not null)
            {
                _autolootService.UpdateColors(ColorItems.ToList());
            }
        }

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

        [RelayCommand]
        private void AddColor()
        {
            var color = new Color() { Name = "New", RGB = [255, 255, 255] };
            color.PropertyChanged += OnColorChanged;
            ColorItems.Add(color);
            // Trigger color update ?
        }

        [RelayCommand]
        private void DeleteColor(Color color)
        {
            color.PropertyChanged -= OnColorChanged;
            ColorItems.Remove(color);
            if (_autolootService is not null)
            {
                _autolootService.UpdateColors(ColorItems.ToList());
            }
        }
    }
}
