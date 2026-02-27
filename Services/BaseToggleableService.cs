using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using SharpHook.Data;

namespace Jido.Services
{
    public abstract class BaseToggleableService : IServiceWithStatus, IToggleableService, IDisposable
    {
        protected readonly IHooksManager _keyHooksManager;
        protected readonly JidoConfig _config;
        protected readonly IMacroService _macroService;
        protected readonly IServiceHub _serviceHub;
        private KeyCode _toggleKey;

        public KeyCode ToggleKey => _toggleKey;

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
            KeyCode toggleKey,
            IServiceHub serviceHub,
            string serviceName
        )
        {
            _keyHooksManager = keyHooksManager;
            _config = config;
            _macroService = macroService;
            _serviceHub = serviceHub;
            _toggleKey = toggleKey;
            _keyHooksManager.RegisterKey(_toggleKey, (_, _) => Toggle());
            _macroService.StatusChanged += OnMacroStatusChanged;
            serviceHub.Register(serviceName, this);
        }

        public abstract void Toggle();

        protected abstract void StopRoutine();

        protected abstract void PersistToggleKey(KeyCode key);

        private void OnMacroStatusChanged(object? sender, ServiceStatus macroStatus)
        {
            if (macroStatus == ServiceStatus.STOPPED && Status != ServiceStatus.STOPPED)
                StopRoutine();
        }

        public async Task<KeyCode> ChangeToggleKey()
        {
            var key = await _keyHooksManager.ListenNextKey();
            _keyHooksManager.UnregisterKey(_toggleKey);
            _toggleKey = key;
            PersistToggleKey(key);
            _config.Persist();
            _keyHooksManager.RegisterKey(_toggleKey, (_, _) => Toggle());
            return _toggleKey;
        }

        public virtual void Dispose()
        {
            _macroService.StatusChanged -= OnMacroStatusChanged;
            _keyHooksManager.UnregisterKey(_toggleKey);
            _keyHooksManager.Dispose();
        }
    }
}
