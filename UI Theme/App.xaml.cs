using System.Windows;

namespace ProjectTactics
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize services
            // var dataService = new Services.DataService();
            // await dataService.InitializeAsync();
        }
    }
}
