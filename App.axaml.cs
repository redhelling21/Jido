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
using Jido.UI.Routing;
using Jido.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Jido
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            IServiceProvider services = ConfigureServices();

            // Eagerly instantiate all feature services so their key hooks are registered at startup
            // rather than lazily on first page visit.
            services.GetRequiredService<IMacroService>();
            services.GetRequiredService<IAutolootService>();
            services.GetRequiredService<IAutopressService>();
            services.GetRequiredService<IInventoryManagementService>();

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
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            // Config
            services.AddSingleton<JidoConfig>(s => new JidoConfig("settings.json"));
            services.AddSingleton<Router<ViewModelBase>>(s => new Router<ViewModelBase>(t =>
                (ViewModelBase)s.GetRequiredService(t)
            ));
            services.AddSingleton<IHooksManager, HooksManager>();
            services.AddSingleton<IServiceHub, ServiceHub>();
            services.AddSingleton<IMacroService, MacroService>();
            services.AddSingleton<IAutolootService, AutolootService>();
            services.AddSingleton<IAutopressService, AutopressService>();
            services.AddSingleton<IInventoryManagementService, InventoryManagementService>();

            // Component ViewModels
            services.AddSingleton<MainWindowViewModel>();
            services.AddSingleton<SidebarViewModel>();
            services.AddTransient<HomePageViewModel>();
            services.AddTransient<AutolootPageViewModel>();
            services.AddTransient<AutopressPageViewModel>();
            services.AddTransient<InventoryManagementPageViewModel>();

            // Utilities
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
            return services.BuildServiceProvider();
        }
    }
}
