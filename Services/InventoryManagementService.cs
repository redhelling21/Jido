using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jido.Config;
using Jido.Models;
using Jido.Utils;
using SharpHook;
using SharpHook.Native;

namespace Jido.Services
{
    public class InventoryManagementService : IInventoryManagementService
    {
        private IHooksManager _keyHooksManager;
        private JidoConfig _config;
        public InventoryManagementConfig Config => _config.Features.InventoryManagement;
        private ServiceStatus _status = ServiceStatus.STOPPED;

        public ServiceStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<ServiceStatus> StatusChanged;

        public InventoryManagementService(IHooksManager keyHooksManager, JidoConfig config)
        {
            _keyHooksManager = keyHooksManager;
            _config = config;
            InitFromConfig();
        }

        private void InitFromConfig()
        {
        }

        public void UpdateConfig(InventoryManagementConfig config)
        {
            _config.Features.InventoryManagement = config;
            _config.Persist();
            InitFromConfig();
        }
    }

    public interface IInventoryManagementService : IServiceWithStatus
    {
        public InventoryManagementConfig Config { get; }

        public void UpdateConfig(InventoryManagementConfig config);
    }
}
