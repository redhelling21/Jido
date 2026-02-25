using System;
using Avalonia;
using Projektanker.Icons.Avalonia;
using Projektanker.Icons.Avalonia.FontAwesome;
using Projektanker.Icons.Avalonia.MaterialDesign;

namespace Jido
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, (l) =>
            {
                l.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            });
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            IconProvider.Current.Register<FontAwesomeIconProvider>().Register<MaterialDesignIconProvider>();

            return AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().LogToTrace();
        }
    }
}
