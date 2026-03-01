using System;
using Jido.Utils;

namespace Jido.Services
{
    public interface IServiceHub
    {
        void Register(string name, IServiceWithStatus service);

        ServiceStatus GetStatus(string name);

        bool IsActive(string name);

        event EventHandler<ServiceStatusChangedEventArgs>? AnyStatusChanged;
    }

    public record ServiceStatusChangedEventArgs(string ServiceName, ServiceStatus Status);

    public static class ServiceNames
    {
        public const string Macro = "Macro";
        public const string Autoloot = "Autoloot";
        public const string Autopress = "Autopress";
        public const string InventoryManagement = "InventoryManagement";
        public const string FillInventory = "FillInventory";
    }
}
