using System;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using PointCloudUtility;
using System.IO.Pipes;
using System.IO;
using System.Windows.Interop;
using Viewer360.View;


namespace Viewer360
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        View.MainWindow m_Window;
        static CMessageManager m_oMsgManager;
        bool m_bNewImageLoaded;


        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            ComponentDispatcher.ThreadIdle += OnApplicationIdle;

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            // View.MainWindow mainWindow = new View.MainWindow();
            m_Window = new View.MainWindow();
            m_Window.viewer360_View.m_Window = m_Window;
            (m_Window.DataContext as ViewModel.MainViewModel).m_Window = m_Window;

            m_Window.Show();
            if (e.Args.Length == 9) 
                await (m_Window.DataContext as ViewModel.MainViewModel).Open(e.Args[0], e.Args[1], e.Args[2], e.Args[3], e.Args[4], e.Args[5], e.Args[6], e.Args[7], e.Args[8]);

            // Inizializzo la messaggistica col server
            m_oMsgManager = new CMessageManager(CMessageManager.PipeType.Client);
            m_oMsgManager.CreateConnection();

            // Attendo che la il Server si connetta
            m_oMsgManager.WaitForConnection();

            // Attivo thread di ricezione; si dovrebbe chiudere da solo all'uscita della finestra.
            CMessageManager.StartListener(m_oMsgManager);

            m_Window.InitUI();

            // Calcolo il valore iniziale della matrice di rotazione originale della camera rispetto al mondo
            m_Window.viewer360_View.ComputeGlobalRotMatrix();
        }


        private void CompositionTarget_Rendering(object sender, EventArgs e)
        {
            // Codice da eseguire durante il rendering
            // Ad esempio, forza l'aggiornamento della view
            if (m_bNewImageLoaded)
            {
                m_Window.InvalidateVisual();
                m_bNewImageLoaded = false;
            }
        }


        private void OnApplicationIdle(object sender, EventArgs e)
        {
            if(m_oMsgManager!=null && m_oMsgManager.PendingMsg())
            {
                //++++++++++++++++++++++++++
                // Console.WriteLine("Da ListenForMessages stop1 Type=" + m_oMsgManager.GetMsgType());
                //++++++++++++++++++++++++++

                if (m_oMsgManager.GetMsgType()==0)  // Messaggio pending è di tipo CameraInfo
                {
                    string sCameraName = "";
                    string sX = "";
                    string sY = "";
                    string sZ = "";
                    string Rotx = "";
                    string Roty = "";
                    string Rotz = "";


                    string sMsg = m_oMsgManager.GetMsg();
                    m_oMsgManager.GetCameraInfo(sMsg, ref sCameraName, ref sX, ref sY, ref sZ, ref Rotx, ref Roty, ref Rotz);

                    (m_Window.DataContext as ViewModel.MainViewModel).LoadNewImage(sCameraName, sX, sY, sZ, Rotx, Roty, Rotz);
                    m_bNewImageLoaded = true;
                }
            }

            if(SharingHelper.m_bCameraAtHasChanged)
            {
                // Comunico la nuova direzione della camera al server
                double dX=1;
                double dY=0;

                m_Window.viewer360_View.ComputeCameraAtForServer(ref dX, ref dY);
                m_oMsgManager.SendCameraAt(dX, dY);

                SharingHelper.m_bCameraAtHasChanged = false;
            }


        }

    }
}
