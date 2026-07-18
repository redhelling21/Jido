using System;
using System.Threading;
using System.Threading.Tasks;
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
        private KeyCombo _toggleCombo;

        protected CancellationTokenSource? _cts;

        // Cancel and dispose the current CTS (if any) and creates a fresh one
        protected CancellationTokenSource ResetCts()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            return _cts;
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
            _toggleCombo = toggleCombo;
            _keyHooksManager.RegisterCombo(_toggleCombo, (_, _) => Toggle());
            _macroService.StatusChanged += OnMacroStatusChanged;
            serviceHub.Register(serviceName, this);
        }

        public abstract void Toggle();

        protected abstract void StopRoutine();

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
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
