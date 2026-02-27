using CommunityToolkit.Mvvm.ComponentModel;
using SharpHook.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jido.UI.ViewModels
{
    public partial class HighLevelCommandViewModel : ObservableObject
    {
        [ObservableProperty]
        protected int _intervalInMs;
    }

    public partial class CompositeHighLevelCommandViewModel : HighLevelCommandViewModel
    {
        public ObservableCollection<LowLevelCommandViewModel> Commands { get; set; } = new ObservableCollection<LowLevelCommandViewModel>();

        public CompositeHighLevelCommandViewModel(List<LowLevelCommandViewModel> commands, int intervalInMs)
        {
            Commands = new ObservableCollection<LowLevelCommandViewModel>(commands);
            IntervalInMs = intervalInMs;
        }

        public void AddLowLevelCommand(string type)
        {
            if (type == "press")
            {
                Commands.Add(new PressCommandViewModel());
            }
            else if (type == "wait")
            {
                Commands.Add(new WaitCommandViewModel());
            }
        }

        public void RemoveLowLevelCommand(LowLevelCommandViewModel command)
        {
            Commands.Remove(command);
        }
    }

    public partial class BasicHighLevelCommandViewModel : HighLevelCommandViewModel
    {
        [ObservableProperty]
        private PressCommandViewModel _command = new PressCommandViewModel();

        public BasicHighLevelCommandViewModel(PressCommandViewModel command, int intervalInMs)
        {
            Command = command;
            IntervalInMs = intervalInMs;
        }
    }

    public partial class LowLevelCommandViewModel : ObservableObject
    {
    }

    public partial class PressCommandViewModel : LowLevelCommandViewModel
    {
        [ObservableProperty]
        private KeyCode _keyToPress;

        [ObservableProperty]
        private int _pressDurationInMs = 100;
    }

    public partial class WaitCommandViewModel : LowLevelCommandViewModel
    {
        [ObservableProperty]
        private int _waitTimeInMs;
    }

    public partial class ConstantCommandViewModel : ObservableObject
    {
        [ObservableProperty]
        private KeyCode _keyToPress;
    }
}
