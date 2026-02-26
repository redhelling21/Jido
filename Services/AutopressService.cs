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
        private readonly System.Timers.Timer _suspendTimer = new() { AutoReset = false };
        private readonly ConcurrentQueue<LowLevelCommand> _queuedCommands = new();
        public List<HighLevelCommand> ScheduledCommands { get; private set; }
        public List<ConstantCommand> ConstantCommands { get; private set; }
        public int ClickDelay { get; private set; }
        public double IntervalRandomizationRatio { get; private set; }

        public AutopressService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            IServiceHub serviceHub
        )
            : base(
                keyHooksManager,
                config,
                macroService,
                config.Features.Autopress.ToggleKey,
                serviceHub,
                ServiceNames.Autopress
            )
        {
            InitFromConfig();
            _suspendTimer.Elapsed += OnSuspendTimerElapsed;
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
            if (_macroService.Status == ServiceStatus.STOPPED)
                return;

            if (Status == ServiceStatus.STOPPED)
                StartRoutine();
            else
            {
                _suspendTimer.Stop();
                if (Status == ServiceStatus.PAUSED)
                    // The routine is already stopped (StopRoutine was called by SuspendAutoPress),
                    // so we only need to cancel the pending resume and update the status.
                    Status = ServiceStatus.STOPPED;
                else
                    StopRoutine();
            }
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
                _suspendTimer.Interval = ClickDelay;
                _suspendTimer.Start();
            }
            else if (Status == ServiceStatus.PAUSED)
            {
                // User clicked again before the resume timer fired — reset the delay
                _suspendTimer.Stop();
                _suspendTimer.Interval = ClickDelay;
                _suspendTimer.Start();
            }
        }

        private void OnSuspendTimerElapsed(object? sender, EventArgs e)
        {
            // Guard against the case where Toggle() or the macro stop fired during the suspension
            // window, both of which set Status away from PAUSED.
            if (Status == ServiceStatus.PAUSED)
            {
                if (_macroService.Status != ServiceStatus.STOPPED)
                    StartRoutine();
                else
                    Status = ServiceStatus.STOPPED;
            }
        }

        private void StartRoutine()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            foreach (var cmd in ConstantCommands)
                _eventSimulator.SimulateKeyPress(cmd.KeyToPress);

            _ = Task.Run(() => KeyPressRoutine(_cancellationTokenSource.Token));

            // Each command enqueues once immediately on Start(), then continues on its own timer.
            // KeyPressRoutine consumes the shared queue and handles the actual key simulation.
            foreach (var cmd in ScheduledCommands)
                cmd.Start(_queuedCommands, IntervalRandomizationRatio);
            Status = ServiceStatus.IDLE;
        }

        protected override void StopRoutine()
        {
            // Guard against double-stop: called from Toggle, SuspendAutoPress, UpdateConfig, and
            // the base class macro-stop handler — any of which may race with each other.
            if (_cancellationTokenSource is null || _cancellationTokenSource.IsCancellationRequested)
                return;
            _cancellationTokenSource.Cancel();

            _suspendTimer.Stop();

            foreach (var cmd in ConstantCommands)
                _eventSimulator.SimulateKeyRelease(cmd.KeyToPress);

            foreach (var cmd in ScheduledCommands)
                cmd.Stop();

            // Drain stale commands so they don't fire on the next StartRoutine.
            while (_queuedCommands.TryDequeue(out _)) { }

            Status = ServiceStatus.STOPPED;
            _cancellationTokenSource.Dispose();
        }

        private async Task KeyPressRoutine(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_queuedCommands.TryDequeue(out var command))
                    {
                        if (command is WaitCommand wait)
                            await Task.Delay(wait.WaitTimeInMs, cancellationToken);
                        else if (command is PressCommand press)
                        {
                            await Task.Delay(300, cancellationToken);
                            _eventSimulator.SimulateKeyPress(press.KeyToPress);
                            // finally guarantees release even if cancellation fires mid-hold,
                            // preventing keys from getting stuck in a pressed state.
                            try
                            {
                                await Task.Delay(press.PressDurationInMs, cancellationToken);
                            }
                            finally
                            {
                                _eventSimulator.SimulateKeyRelease(press.KeyToPress);
                            }
                        }
                    }
                    else
                        await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
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
