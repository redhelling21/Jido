using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using SharpHook.Native;

namespace Jido.Services
{
    public abstract class BaseToggleableService : IServiceWithStatus, IToggleableService, IDisposable
    {
        protected readonly IHooksManager _keyHooksManager;
        protected readonly JidoConfig _config;
        protected readonly IMacroService _macroService;
        private KeyCode _toggleKey;

        public KeyCode ToggleKey => _toggleKey;

        private ServiceStatus _status = ServiceStatus.STOPPED;

        public ServiceStatus Status
        {
            get => _status;
            protected set
            {
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<ServiceStatus> StatusChanged;

        protected BaseToggleableService(
            IHooksManager keyHooksManager,
            JidoConfig config,
            IMacroService macroService,
            KeyCode toggleKey
        )
        {
            _keyHooksManager = keyHooksManager;
            _config = config;
            _macroService = macroService;
            _toggleKey = toggleKey;
            _keyHooksManager.RegisterKey(_toggleKey, (_, _) => Toggle());
            _macroService.StatusChanged += OnMacroStatusChanged;
        }

        public abstract void Toggle();

        protected abstract void StopRoutine();

        protected abstract void PersistToggleKey(KeyCode key);

        private void OnMacroStatusChanged(object? sender, ServiceStatus macroStatus)
        {
            if (macroStatus == ServiceStatus.STOPPED && Status != ServiceStatus.STOPPED)
                StopRoutine();
        }

        public Task<KeyCode> ChangeToggleKey()
        {
            return _keyHooksManager
                .ListenNextKey()
                .ContinueWith(task =>
                {
                    _keyHooksManager.UnregisterKey(_toggleKey);
                    _toggleKey = task.Result;
                    PersistToggleKey(task.Result);
                    _config.Persist();
                    _keyHooksManager.RegisterKey(_toggleKey, (_, _) => Toggle());
                    return _toggleKey;
                });
        }

        public virtual void Dispose()
        {
            _macroService.StatusChanged -= OnMacroStatusChanged;
            _keyHooksManager.Dispose();
        }
    }
}
