using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AutoMapper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jido.Config;
using Jido.Models;
using Jido.Services;
using Jido.UI.ViewModels;
using Jido.Utils;
using SharpHook.Native;
using static Jido.UI.ViewModels.CompositeHighLevelCommandViewModel;

namespace Jido.UI.Components.Pages.Autopress
{
    public partial class AutopressPageViewModel : ToggleablePageViewModel, IDisposable
    {
        private readonly IAutopressService? _autopressService;
        private readonly IMapper _mapper;

        [ObservableProperty]
        private int clickDelay;

        [ObservableProperty]
        private double intervalsRandomizationRatio;

        public ObservableCollection<HighLevelCommandViewModel> ScheduledCommands { get; } =
            new ObservableCollection<HighLevelCommandViewModel>();

        public ObservableCollection<ConstantCommandViewModel> ConstantCommands { get; } =
            new ObservableCollection<ConstantCommandViewModel>();

        public AutopressPageViewModel()
        {
            // Placeholder for design purpose
            ScheduledCommands = new ObservableCollection<HighLevelCommandViewModel>(
                new ObservableCollection<HighLevelCommandViewModel>()
                {
                    new BasicHighLevelCommandViewModel(
                        new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcE },
                        1000
                    ),
                    new BasicHighLevelCommandViewModel(
                        new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcR },
                        2000
                    ),
                    new BasicHighLevelCommandViewModel(
                        new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcR },
                        3000
                    ),
                    new CompositeHighLevelCommandViewModel(
                        new List<LowLevelCommandViewModel>()
                        {
                            new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcW },
                            new WaitCommandViewModel() { WaitTimeInMs = 500 },
                            new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcO },
                        },
                        1500
                    ),
                    new CompositeHighLevelCommandViewModel(
                        new List<LowLevelCommandViewModel>()
                        {
                            new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcT },
                            new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcB }
                        },
                        2500
                    )
                }
            );
            ConstantCommands = new ObservableCollection<ConstantCommandViewModel>(
                new List<ConstantCommandViewModel>()
                {
                    new() { KeyToPress = SharpHook.Native.KeyCode.VcY },
                    new() { KeyToPress = SharpHook.Native.KeyCode.VcH },
                }
            );
        }

        public AutopressPageViewModel(IAutopressService autopressService, IMapper mapper)
            : base(autopressService)
        {
            _autopressService = autopressService;
            _autopressService.StatusChanged += OnAutopressStatusChange;
            _mapper = mapper;
            ClickDelay = _autopressService.ClickDelay;
            ScheduledCommands = new ObservableCollection<HighLevelCommandViewModel>(
                _mapper.Map<List<HighLevelCommandViewModel>>(_autopressService.ScheduledCommands)
            );
            ConstantCommands = new ObservableCollection<ConstantCommandViewModel>(
                _mapper.Map<List<ConstantCommandViewModel>>(_autopressService.ConstantCommands)
            );
        }

        private void OnAutopressStatusChange(object? sender, ServiceStatus status)
        { }

        public void Dispose()
        {
            _autopressService!.StatusChanged -= OnAutopressStatusChange;
        }

        #region commands

        [RelayCommand]
        private void AddConstantCommand()
        {
            var command = new ConstantCommandViewModel();
            ConstantCommands.Add(command);
        }

        [RelayCommand]
        private void RemoveConstantCommand(ConstantCommandViewModel command)
        {
            ConstantCommands.Remove(command);
        }

        [RelayCommand]
        private void AddBasicHighLevelCommand()
        {
            var command = new BasicHighLevelCommandViewModel(
                new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcUndefined },
                1000
            );
            ScheduledCommands.Add(command);
        }

        [RelayCommand]
        private void AddCompositeHighLevelCommand()
        {
            var command = new CompositeHighLevelCommandViewModel(
                new List<LowLevelCommandViewModel>()
                {
                    new PressCommandViewModel() { KeyToPress = SharpHook.Native.KeyCode.VcUndefined },
                },
                1000
            );
            ScheduledCommands.Add(command);
        }

        [RelayCommand]
        private void AddLowLevelCommandToComposite(CompositeHighLevelCommandViewModel command)
        { }

        [RelayCommand]
        private void RemoveLowLevelCommandFromComposite(LowLevelCommandViewModel command)
        {
            var parent = ScheduledCommands
                .Where(c =>
                {
                    return c is CompositeHighLevelCommandViewModel
                        && ((CompositeHighLevelCommandViewModel)c).Commands.Contains(command);
                })
                .FirstOrDefault();
            if (parent != null)
            {
                ((CompositeHighLevelCommandViewModel)parent).Commands.Remove(command);
            }
        }

        [RelayCommand]
        private void RemoveHighLevelCommand(HighLevelCommandViewModel command)
        {
            ScheduledCommands.Remove(command);
        }

        [RelayCommand]
        private void MoveUpHighLevelCommand(HighLevelCommandViewModel command)
        {
            var index = ScheduledCommands.IndexOf(command);
            if (index > 0)
            {
                ScheduledCommands.Move(index, index - 1);
            }
        }

        [RelayCommand]
        private void MoveDownHighLevelCommand(HighLevelCommandViewModel command)
        {
            var index = ScheduledCommands.IndexOf(command);
            if (index < ScheduledCommands.Count - 1)
            {
                ScheduledCommands.Move(index, index + 1);
            }
        }

        [RelayCommand]
        private void SaveAutoPressConfig()
        {
            if (_autopressService is not null)
            {
                var config = new AutopressConfig();
                config.ConstantCommands = _mapper.Map<List<ConstantCommand>>(ConstantCommands.ToList());
                config.ScheduledCommands = _mapper.Map<List<HighLevelCommand>>(ScheduledCommands.ToList());
                config.ClickDelay = ClickDelay;
                config.IntervalRandomizationRatio = IntervalsRandomizationRatio / 100;
                _autopressService.UpdateConfig(config);
            }
        }

        #endregion commands
    }
}
