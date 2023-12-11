using System;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using PointCloudUtility;


namespace Viewer360
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        View.MainWindow m_Window;

        private async void Application_Startup(object sender, StartupEventArgs e)
        {

            // View.MainWindow mainWindow = new View.MainWindow();
            m_Window = new View.MainWindow();
            m_Window.viewer360_View.m_Window = m_Window;
            m_Window.Show();
            if (e.Args.Length == 9) 
                await (m_Window.DataContext as ViewModel.MainViewModel).Open(e.Args[0], e.Args[1], e.Args[2], e.Args[3], e.Args[4], e.Args[5], e.Args[6], e.Args[7], e.Args[8]);

            m_Window.InitUI();
        }
    }
}
