using System.Threading;
using System.Windows;
using System.Windows.Markup;
using MdkControllerUpdate.ViewModel;

namespace MdkControllerUpdate.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainView
    {
        public MainView()
        {
            InitializeComponent();

            Language = XmlLanguage.GetLanguage(Thread.CurrentThread.CurrentCulture.Name);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var mainViewModel = DataContext as MainViewModel;
            if (mainViewModel == null) return;

            mainViewModel.RefreshPortsCommand.Execute(null);
            mainViewModel.RefreshReleasesCommand.Execute(null);
        }
    }
}
