using Jido.Services;
using Jido.UI.Components;

namespace Jido.UI.Components.Pages.Home
{
    public partial class HomePageViewModel : ToggleablePageViewModel
    {
        public HomePageViewModel() { }

        public HomePageViewModel(IMacroService macroService)
            : base(macroService)
        { }
    }
}
