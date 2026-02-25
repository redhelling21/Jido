using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Jido.UI.Components
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        public void OnClosing(object? sender, CancelEventArgs e)
        {
            if (this.DataContext is not null && this.DataContext is MainWindowViewModel viewModel)
                viewModel.OnClosing(sender, e);
        }
    }
}
