using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Jido.UI.Components
{
    /// <summary>
    /// Backing state for a save button that confirms the write by briefly swapping its own icon
    /// and label, then reverting. Deliberately not a notification system — one instance per save
    /// button, bound directly by that button.
    /// </summary>
    public partial class SaveFeedback : ObservableObject
    {
        private const string IdleIcon = "fa fa-floppy-disk";

        private readonly string _idleText;
        private readonly DispatcherTimer _revert;

        [ObservableProperty]
        private string _icon = IdleIcon;

        [ObservableProperty]
        private string _text;

        public SaveFeedback(string idleText = "Save")
        {
            _idleText = idleText;
            _text = idleText;
            _revert = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _revert.Tick += (_, _) =>
            {
                _revert.Stop();
                Icon = IdleIcon;
                Text = _idleText;
            };
        }

        public void Flash(bool succeeded = true)
        {
            Icon = succeeded ? "fa fa-check" : "fa fa-xmark";
            Text = succeeded ? "Saved" : "Failed";

            // Restarting supersedes any pending revert, so a double-click can't clear the newer flash
            _revert.Stop();
            _revert.Start();
        }
    }
}
