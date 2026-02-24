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
    public class MacroService : IMacroService, IDisposable
    {
        private readonly IHooksManager _keyHooksManager;
        private readonly JidoConfig _config;
        private KeyCode _toggleKey;

        public KeyCode ToggleKey => _toggleKey;

        private ServiceStatus _status = ServiceStatus.STOPPED;

        public ServiceStatus Status
        {
            get => _status;
            private set
            {
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<ServiceStatus> StatusChanged;

        public MacroService(IHooksManager keyHooksManager, JidoConfig config)
        {
            _keyHooksManager = keyHooksManager;
            _config = config;
            _toggleKey = _config.ToggleKey;
            _keyHooksManager.RegisterKey(_toggleKey, (_, _) => Toggle());
        }

        public void Toggle()
        {
            Status = Status == ServiceStatus.STOPPED ? ServiceStatus.IDLE : ServiceStatus.STOPPED;
            StatusChanged?.Invoke(this, Status);
        }

        public Task<KeyCode> ChangeToggleKey()
        {
            return _keyHooksManager
                .ListenNextKey()
                .ContinueWith(task =>
                {
                    _keyHooksManager.UnregisterKey(_toggleKey);
                    _toggleKey = task.Result;
                    _config.ToggleKey = task.Result;
                    _config.Persist();
                    _keyHooksManager.RegisterKey(_toggleKey, (_, _) => Toggle());
                    return _toggleKey;
                });
        }

        public void Dispose()
        {
            _keyHooksManager.Dispose();
        }
    }

    public interface IMacroService : IServiceWithStatus, IToggleableService
    { }
}
