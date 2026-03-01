using System;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Utils;
using Microsoft.Extensions.Logging;

namespace Jido.Services
{
    public class MacroService : IMacroService, IDisposable
    {
        private readonly IHooksManager _keyHooksManager;
        private readonly JidoConfig _config;
        private readonly ILogger<MacroService> _logger;
        private KeyCombo _toggleCombo;

        public KeyCombo ToggleKey => _toggleCombo;

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

        public MacroService(IHooksManager keyHooksManager, JidoConfig config, IServiceHub serviceHub, ILogger<MacroService> logger)
        {
            _keyHooksManager = keyHooksManager;
            _config = config;
            _logger = logger;
            _toggleCombo = _config.ToggleKey;
            _keyHooksManager.RegisterCombo(_toggleCombo, (_, _) => Toggle());
            serviceHub.Register(ServiceNames.Macro, this);
        }

        public void Toggle()
        {
            if (Status == ServiceStatus.STOPPED)
            {
                _logger.LogInformation("Macro started");
                Status = ServiceStatus.IDLE;
            }
            else
            {
                _logger.LogInformation("Macro stopped");
                Status = ServiceStatus.STOPPED;
            }
        }

        public async Task<KeyCombo> ChangeToggleKey()
        {
            var combo = await _keyHooksManager.ListenNextCombo();
            if (combo == _toggleCombo)
                return _toggleCombo;

            _keyHooksManager.UnregisterCombo(_toggleCombo);
            _toggleCombo = combo;
            _config.ToggleKey = combo;
            _config.Persist();
            _keyHooksManager.RegisterCombo(_toggleCombo, (_, _) => Toggle());
            return _toggleCombo;
        }

        public void Dispose()
        {
            _keyHooksManager.UnregisterCombo(_toggleCombo);
            _keyHooksManager.Dispose();
        }
    }

    public interface IMacroService : IServiceWithStatus, IToggleableService
    { }
}
