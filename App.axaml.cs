using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Jido.Config;
using Jido.Services;
using Jido.UI.Components;
using Jido.UI.Components.Common.Sidebar;
using Jido.UI.Components.Pages.Autoloot;
using Jido.UI.Components.Pages.Autopress;
using Jido.UI.Components.Pages.Home;
using Jido.UI.Components.Pages.InventoryManagement;
using Jido.UI.Components.Pages.Logs;
using Jido.UI.Routing;
using Jido.Utils;
using Jido.Utils.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jido
{
    public partial class App : Application
    {
        private ServiceProvider? _services;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            ServiceProvider services = ConfigureServices();
            _services = services;

            var logger = services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Jido v2 starting");

            // Eagerly instantiate all feature services so their key hooks are registered at startup
            // rather than lazily on first page visit.
            services.GetRequiredService<IMacroService>();
            logger.LogInformation("MacroService initialized");
            services.GetRequiredService<IAutolootService>();
            logger.LogInformation("AutolootService initialized");
            services.GetRequiredService<IAutopressService>();
            logger.LogInformation("AutopressService initialized");
            services.GetRequiredService<IInventoryManagementService>();
            logger.LogInformation("InventoryManagementService initialized");
            services.GetRequiredService<IFillInventoryService>();
            logger.LogInformation("FillInventoryService initialized");
            services.GetRequiredService<IBulkUseItemService>();
            logger.LogInformation("BulkUseItemService initialized");
            logger.LogInformation("All services ready");

            var router = services.GetRequiredService<Router<ViewModelBase>>();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Line below is needed to remove Avalonia data validation. Without this line you
                // will get duplicate validations from both Avalonia and CT
                BindingPlugins.DataValidators.RemoveAt(0);
                desktop.MainWindow = new MainWindow
                {
                    DataContext = services.GetRequiredService<MainWindowViewModel>(),
                };

                // Dispose the container on exit so every singleton's Dispose actually runs: the
                // global input hook, autopress timers, OpenCV Mats and the log buffer.
                desktop.Exit += (_, _) =>
                {
                    logger.LogInformation("Jido v2 shutting down");
                    _services?.Dispose();
                    _services = null;
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            var inMemoryLoggerProvider = new InMemoryLoggerProvider();
            services.AddSingleton(inMemoryLoggerProvider);
            services.AddLogging(b => b.AddDebug().AddProvider(inMemoryLoggerProvider).SetMinimumLevel(LogLevel.Debug));
            // Config
            services.AddSingleton<JidoConfig>(s => new JidoConfig(
                "settings.json",
                s.GetRequiredService<ILogger<JidoConfig>>()
            ));
            services.AddSingleton<Router<ViewModelBase>>(s => new Router<ViewModelBase>(t =>
                (ViewModelBase)s.GetRequiredService(t)
            ));
            services.AddSingleton<IHooksManager, HooksManager>();
            services.AddSingleton<IServiceHub, ServiceHub>();
            services.AddSingleton<IMacroService, MacroService>();
            services.AddSingleton<IAutolootService, AutolootService>();
            services.AddSingleton<IAutopressService, AutopressService>();
            services.AddSingleton<IInventoryManagementService, InventoryManagementService>();
            services.AddSingleton<IFillInventoryService, FillInventoryService>();
            services.AddSingleton<IBulkUseItemService, BulkUseItemService>();

            // Component ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<SidebarViewModel>();
            services.AddTransient<HomePageViewModel>();
            services.AddTransient<AutolootPageViewModel>();
            services.AddTransient<AutopressPageViewModel>();
            services.AddTransient<InventoryManagementPageViewModel>();
            services.AddTransient<LogsPageViewModel>();

            // Utilities
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            return services.BuildServiceProvider();
        }
    }
}
