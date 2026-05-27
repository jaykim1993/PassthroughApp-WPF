using System.Windows;
using PassthroughApp.ViewModels;
using PassthroughApp.Views;

namespace PassthroughApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new MainWindow { DataContext = new MainWindowViewModel() }.Show();
        }
    }
}
