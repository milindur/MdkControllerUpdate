using System.Threading;
using System.Windows;
using System.Windows.Markup;
using GalaSoft.MvvmLight.Messaging;
using MdkControllerUpdate.Messages;
using MdkControllerUpdate.ViewModel;
using Microsoft.Win32;

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

            Messenger.Default.Register<FileOpenDialogMessage>(this, m =>
            {
                var ofd = new OpenFileDialog
                {
                    CheckFileExists = true,
                    DefaultExt = ".bin",
                    Filter = "Firmware (.bin)|*.bin"
                };
                if ((bool) ofd.ShowDialog(Owner))
                {
                    m.FileSelected(ofd.FileName);
                }
                else
                {
                    m.Canceled();
                }
            });
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
