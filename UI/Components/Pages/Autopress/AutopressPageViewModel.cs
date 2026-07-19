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
using SharpHook.Data;
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

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveBuildCommand))]
        private string newBuildName = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadBuildCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteBuildCommand))]
        private string? selectedBuild;

        public ObservableCollection<string> BuildNames { get; } = new();

        public SaveFeedback ConfigSaveState { get; } = new("Save all");

        public SaveFeedback BuildSaveState { get; } = new();

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
                        new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcE },
                        1000
                    ),
                    new BasicHighLevelCommandViewModel(
                        new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcR },
                        2000
                    ),
                    new BasicHighLevelCommandViewModel(
                        new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcR },
                        3000
                    ),
                    new CompositeHighLevelCommandViewModel(
                        new List<LowLevelCommandViewModel>()
                        {
                            new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcW },
                            new WaitCommandViewModel() { WaitTimeInMs = 500 },
                            new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcO },
                        },
                        1500
                    ),
                    new CompositeHighLevelCommandViewModel(
                        new List<LowLevelCommandViewModel>()
                        {
                            new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcT },
                            new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcB }
                        },
                        2500
                    )
                }
            );
            ConstantCommands = new ObservableCollection<ConstantCommandViewModel>(
                new List<ConstantCommandViewModel>()
                {
                    new() { KeyToPress = SharpHook.Data.KeyCode.VcY },
                    new() { KeyToPress = SharpHook.Data.KeyCode.VcH },
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
            IntervalsRandomizationRatio = _autopressService.IntervalRandomizationRatio * 100;
            ScheduledCommands = new ObservableCollection<HighLevelCommandViewModel>(
                _mapper.Map<List<HighLevelCommandViewModel>>(_autopressService.ScheduledCommands)
            );
            ConstantCommands = new ObservableCollection<ConstantCommandViewModel>(
                _mapper.Map<List<ConstantCommandViewModel>>(_autopressService.ConstantCommands)
            );
            foreach (var b in _autopressService.Builds)
                BuildNames.Add(b.Name);
        }

        private void OnAutopressStatusChange(object? sender, ServiceStatus status)
        { }

        partial void OnSelectedBuildChanged(string? value)
        {
            if (value is not null)
                NewBuildName = value;
        }

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
                new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcUndefined },
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
                    new PressCommandViewModel() { KeyToPress = SharpHook.Data.KeyCode.VcUndefined },
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
            if (_autopressService is null) return;
            _autopressService.UpdateConfig(BuildCurrentConfig());
            ConfigSaveState.Flash();
        }

        [RelayCommand(CanExecute = nameof(CanSaveBuild))]
        private void SaveBuild()
        {
            var name = NewBuildName.Trim();
            if (string.IsNullOrEmpty(name) || _autopressService is null) return;

            // Only reflect the build in the UI once it actually reached disk
            var saved = _autopressService.SaveBuild(name, BuildCurrentConfig());
            if (saved)
            {
                if (!BuildNames.Contains(name))
                    BuildNames.Add(name);
                SelectedBuild = name;
            }
            BuildSaveState.Flash(saved);
        }

        private bool CanSaveBuild() =>
            _autopressService is not null && !string.IsNullOrWhiteSpace(NewBuildName);

        [RelayCommand(CanExecute = nameof(HasSelectedBuild))]
        private void LoadBuild()
        {
            if (SelectedBuild is null || _autopressService is null) return;
            if (!_autopressService.LoadBuild(SelectedBuild)) return;
            ClickDelay = _autopressService.ClickDelay;
            IntervalsRandomizationRatio = _autopressService.IntervalRandomizationRatio * 100;
            ScheduledCommands.Clear();
            foreach (var cmd in _mapper.Map<List<HighLevelCommandViewModel>>(_autopressService.ScheduledCommands))
                ScheduledCommands.Add(cmd);
            ConstantCommands.Clear();
            foreach (var cmd in _mapper.Map<List<ConstantCommandViewModel>>(_autopressService.ConstantCommands))
                ConstantCommands.Add(cmd);
        }

        [RelayCommand(CanExecute = nameof(HasSelectedBuild))]
        private void DeleteBuild()
        {
            if (SelectedBuild is null || _autopressService is null) return;
            if (!_autopressService.DeleteBuild(SelectedBuild)) return;
            BuildNames.Remove(SelectedBuild);
            SelectedBuild = BuildNames.Count > 0 ? BuildNames[0] : null;
        }

        private bool HasSelectedBuild() => _autopressService is not null && SelectedBuild is not null;

        private AutopressConfig BuildCurrentConfig() => new()
        {
            ConstantCommands = _mapper.Map<List<ConstantCommand>>(ConstantCommands.ToList()),
            ScheduledCommands = _mapper.Map<List<HighLevelCommand>>(ScheduledCommands.ToList()),
            ClickDelay = ClickDelay,
            IntervalRandomizationRatio = IntervalsRandomizationRatio / 100,
        };

        #endregion commands
    }
}
