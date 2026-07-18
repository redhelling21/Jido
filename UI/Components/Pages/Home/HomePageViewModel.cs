using CommunityToolkit.Mvvm.ComponentModel;
using Jido.Services;
using Jido.UI.Components;
using Jido.Utils;

namespace Jido.UI.Components.Pages.Home
{
    public partial class HomePageViewModel : ToggleablePageViewModel
    {
        public string DetectedResolution => $"{ScreenUtils.PrimaryWidth} × {ScreenUtils.PrimaryHeight} px";

        public HomePageViewModel() { }

        public HomePageViewModel(IMacroService macroService)
            : base(macroService)
        { }
    }
}
