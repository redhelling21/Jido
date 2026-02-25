using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Jido.Config;
using Jido.Models;
using Jido.Utils;
using SharpHook;
using SharpHook.Native;

namespace Jido.Services
{
    public class AutopressService : BaseToggleableService, IAutopressService
    {
        private EventSimulator _eventSimulator = new EventSimulator();
        private CancellationTokenSource _cancellationTokenSource;
        private System.Timers.Timer _suspendTimer = new();
        private readonly ConcurrentQueue<LowLevelCommand> _queuedCommands = new();
        public List<HighLevelCommand> ScheduledCommands { get; private set; }
        public List<ConstantCommand> ConstantCommands { get; private set; }
        public int ClickDelay { get; private set; }
        public double IntervalRandomizationRatio { get; private set; }

        public AutopressService(IHooksManager keyHooksManager, JidoConfig config, IMacroService macroService)
            : base(keyHooksManager, config, macroService, config.Features.Autopress.ToggleKey)
        {
            InitFromConfig();
            _keyHooksManager.RegisterMouseClick(MouseButton.Button1, SuspendAutoPress);
        }

        private void InitFromConfig()
        {
            ScheduledCommands = _config.Features.Autopress.ScheduledCommands;
            ConstantCommands = _config.Features.Autopress.ConstantCommands;
            ClickDelay = _config.Features.Autopress.ClickDelay;
            IntervalRandomizationRatio = _config.Features.Autopress.IntervalRandomizationRatio;
        }

        public override void Toggle()
        {
            if (Status == ServiceStatus.STOPPED)
                StartRoutine();
            else
                StopRoutine();
        }

        public void UpdateConfig(AutopressConfig config)
        {
            if (Status != ServiceStatus.STOPPED)
                StopRoutine();
            config.ToggleKey = ToggleKey;
            _config.Features.Autopress = config;
            _config.Persist();
            InitFromConfig();
        }

        public void SuspendAutoPress(object? sender, EventArgs e)
        {
            if (Status is ServiceStatus.IDLE or ServiceStatus.WORKING)
            {
                StopRoutine();
                Status = ServiceStatus.PAUSED;
                _suspendTimer.Stop();
                _suspendTimer.Dispose();
                _suspendTimer = new System.Timers.Timer(ClickDelay) { AutoReset = false };
                _suspendTimer.Elapsed += (_, _) => StartRoutine();
                _suspendTimer.Start();
            }
            else if (Status == ServiceStatus.PAUSED)
            {
                _suspendTimer.Stop();
                _suspendTimer.Interval = ClickDelay;
                _suspendTimer.Start();
            }
        }

        private void StartRoutine()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            // Press all constant commands
            foreach (var cmd in ConstantCommands)
                _eventSimulator.SimulateKeyPress(cmd.KeyToPress);

            // Start key press routine
            _ = Task.Run(() => KeyPressRoutine(_cancellationTokenSource.Token));

            // Each command handles its own internal timer
            foreach (var cmd in ScheduledCommands)
                cmd.Start(_queuedCommands);
            Status = ServiceStatus.IDLE;
        }

        protected override void StopRoutine()
        {
            if (_cancellationTokenSource is null || _cancellationTokenSource.IsCancellationRequested)
                return;
            _cancellationTokenSource.Cancel();

            foreach (var cmd in ConstantCommands)
                _eventSimulator.SimulateKeyRelease(cmd.KeyToPress);

            foreach (var cmd in ScheduledCommands)
                cmd.Stop();
            Status = ServiceStatus.STOPPED;
            _cancellationTokenSource.Dispose();
        }

        private async Task KeyPressRoutine(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_queuedCommands.TryDequeue(out var command))
                {
                    // We found work
                    if (command is WaitCommand wait)
                        await Task.Delay(wait.WaitTimeInMs);
                    else if (command is PressCommand press)
                    {
                        // Artificial delay between presses
                        await Task.Delay(300);
                        _eventSimulator.SimulateKeyPress(press.KeyToPress);
                        await Task.Delay(press.PressDurationInMs);
                        _eventSimulator.SimulateKeyRelease(press.KeyToPress);
                    }
                }
                else
                    await Task.Delay(100);
            }
        }

        protected override void PersistToggleKey(KeyCode key) => _config.Features.Autopress.ToggleKey = key;

        public override void Dispose()
        {
            _keyHooksManager.UnRegisterMouseClick(MouseButton.Button1, SuspendAutoPress);
            _cancellationTokenSource?.Dispose();
            _suspendTimer?.Dispose();
            base.Dispose();
        }
    }

    public interface IAutopressService : IServiceWithStatus, IToggleableService
    {
        public List<HighLevelCommand> ScheduledCommands { get; }
        public List<ConstantCommand> ConstantCommands { get; }

        public int ClickDelay { get; }

        public double IntervalRandomizationRatio { get; }

        public void UpdateConfig(AutopressConfig config);
    }
}
