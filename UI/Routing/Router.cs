using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Jido.UI.Components.Pages.Autoloot;
using Jido.UI.Components.Pages.Autopress;
using Jido.UI.Components;
using Jido.UI.Components.Pages.Home;
using Jido.UI.Components.Pages.InventoryManagement;

namespace Jido.UI.Routing
{
    public class Router<TViewModelBase>
        where TViewModelBase : ViewModelBase
    {
        private TViewModelBase _currentViewModel = default!;
        private readonly Dictionary<string, Type> _routes;
        protected readonly Func<Type, TViewModelBase> CreateViewModel;

        public event Action<TViewModelBase>? CurrentViewModelChanged;

        public Router(Func<Type, TViewModelBase> createViewModel)
        {
            CreateViewModel = createViewModel;
            _routes = new Dictionary<string, Type>
            {
                { "Home", typeof(HomePageViewModel) },
                { "Autoloot", typeof(AutolootPageViewModel) },
                { "Autopress", typeof(AutopressPageViewModel) },
                { "InventoryManagement", typeof(InventoryManagementPageViewModel) },
            };
        }

        protected TViewModelBase CurrentViewModel
        {
            set
            {
                if (value == _currentViewModel)
                    return;
                _currentViewModel = value;
                OnCurrentViewModelChanged(value);
            }
        }

        private void OnCurrentViewModelChanged(TViewModelBase viewModel)
        {
            CurrentViewModelChanged?.Invoke(viewModel);
        }

        public virtual T GoTo<T>()
            where T : TViewModelBase
        {
            var viewModel = InstantiateViewModel<T>();
            CurrentViewModel = viewModel;
            return viewModel;
        }

        public virtual TViewModelBase GoTo(string path)
        {
            if (_routes.ContainsKey(path))
            {
                MethodInfo goToMethod = GetType()
                    .GetMethods()
                    .Where(m => m.Name == "GoTo" && m.IsGenericMethod)
                    .FirstOrDefault()
                    .MakeGenericMethod(_routes[path]);
                var model = goToMethod.Invoke(this, null);
                return (TViewModelBase)model;
            }
            else
            {
                // Handle unknown page key
                throw new ArgumentException($"No view model found for page key '{path}'.");
            }
        }

        protected T InstantiateViewModel<T>()
            where T : TViewModelBase
        {
            return (T)Convert.ChangeType(CreateViewModel(typeof(T)), typeof(T));
        }
    }
}
