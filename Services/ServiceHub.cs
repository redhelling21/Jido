using System;
using System.Collections.Concurrent;
using Jido.Utils;

namespace Jido.Services
{
    public class ServiceHub : IServiceHub
    {
        private readonly ConcurrentDictionary<string, IServiceWithStatus> _services = new();

        public event EventHandler<ServiceStatusChangedEventArgs>? AnyStatusChanged;

        public void Register(string name, IServiceWithStatus service)
        {
            _services[name] = service;
            service.StatusChanged += (_, status) =>
                AnyStatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(name, status));
        }

        public ServiceStatus GetStatus(string name) =>
            _services.TryGetValue(name, out var svc) ? svc.Status : ServiceStatus.STOPPED;

        public bool IsActive(string name) => GetStatus(name) != ServiceStatus.STOPPED;
    }
}
