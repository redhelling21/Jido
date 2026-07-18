using System;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper.Configuration.Conventions;
using Jido.Config;
using Jido.Utils;

namespace Jido.Services
{
    public abstract class BaseToggleableService : IServiceWithStatus, IToggleableService, IDisposable
    {
        protected readonly IHooksManager _keyHooksManager;
        protected readonly JidoConfig _config;
        protected readonly IMacroService _macroService;
        protected readonly IServiceHub _serviceHub;
        protected readonly string _serviceName;
        private KeyCombo _toggleCombo;

        // Define services that shouldn't run while this one does
        protected virtual string[] ExclusiveWith => Array.Empty<string>();

        protected CancellationTokenSource? _cts;

        // Cancel and dispose the current CTS (if any) and create a fresh one.
        // A plain read-cancel-assign would let two callers dispose the same CTS twice
        protected CancellationTokenSource ResetCts()
        {
            var fresh = new CancellationTokenSource();
            // Replace the content of the ref, the cancel the previous one
            var previous = Interlocked.Exchange(ref _cts, fresh);
            if (previous is not null)
            {
                try
                {
                    previous.Cancel();
                }
                catch (ObjectDisposedException) { }
                previous.Dispose();
            }
            return fresh;
        }

        // Cancel the in-flight routine, if any, without racing a concurrent ResetCts/Dispose.
        protected void CancelCts()
        {
            var current = Volatile.Read(ref _cts);
            if (current is null)
                return;
            try
            {
                current.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already torn down by a concurrent ResetCts or Dispose — nothing to cancel.
            }
        }

        public KeyCombo ToggleKey => _toggleCombo;

        // ensures cross-thread reads see the latest write
        private volatile int _statusValue = (int)ServiceStatus.STOPPED;

        public ServiceStatus Status
        {
            get => (ServiceStatus)_statusValue;
            protected set
            {
                _statusValue = (int)value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<ServiceStatus> StatusChanged;

        protected BaseToggleableService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            KeyCombo toggleCombo,
            IServiceHub serviceHub,
            string serviceName
        )
        {
            _keyHooksManager = keyHooksManager;
            _config = config;
            _macroService = macroService;
            _serviceHub = serviceHub;
            _serviceName = serviceName;
            _toggleCombo = toggleCombo;
            _keyHooksManager.RegisterCombo(_toggleCombo, (_, _) => Toggle());
            _macroService.StatusChanged += OnMacroStatusChanged;
            serviceHub.Register(serviceName, this);
        }

        public abstract void Toggle();

        protected bool CanStartRoutine()
        {
            if (_macroService.Status == ServiceStatus.STOPPED)
                return false;

            foreach (var name in ExclusiveWith)
            {
                if (name == _serviceName)
                    continue;
                if (_serviceHub.IsActive(name))
                    return false;
            }
            return true;
        }

        protected void ToggleOneShotRoutine(Func<CancellationToken, Task> routine)
        {
            if (Status == ServiceStatus.WORKING)
            {
                StopRoutine();
                return;
            }

            if (!CanStartRoutine())
                return;

            // Swap the CTS here rather than inside the task, so the token is in place by the time
            // Toggle returns and a rapid second press cannot race two ResetCts calls.
            var token = ResetCts().Token;
            _ = Task.Run(() => routine(token));
        }

        protected virtual void StopRoutine()
        {
            CancelCts();
            Status = ServiceStatus.STOPPED;
        }

        protected abstract void PersistToggleCombo(KeyCombo combo);

        private void OnMacroStatusChanged(object? sender, ServiceStatus macroStatus)
        {
            if (macroStatus == ServiceStatus.STOPPED && Status != ServiceStatus.STOPPED)
                StopRoutine();
        }

        public async Task<KeyCombo> ChangeToggleKey()
        {
            var combo = await _keyHooksManager.ListenNextCombo();
            if (combo == _toggleCombo)
                return _toggleCombo;

            // Fallback tothe original combo if the new one is already registered somewhere else
            if (_keyHooksManager.IsComboRegistered(combo))
                return _toggleCombo;

            _keyHooksManager.UnregisterCombo(_toggleCombo);
            _toggleCombo = combo;
            PersistToggleCombo(combo);
            _config.Persist();
            _keyHooksManager.RegisterCombo(_toggleCombo, (_, _) => Toggle());
            return _toggleCombo;
        }

        public virtual void Dispose()
        {
            _macroService.StatusChanged -= OnMacroStatusChanged;
            _keyHooksManager.UnregisterCombo(_toggleCombo);
            // Free the ref, then cancel what was inside
            var cts = Interlocked.Exchange(ref _cts, null);
            if (cts is not null)
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException) { }
                cts.Dispose();
            }
        }
    }
}
