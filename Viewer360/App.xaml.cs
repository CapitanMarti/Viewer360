using System;

using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using PointCloudUtility;
using System.IO.Pipes;
using System.IO;
using System.Windows.Interop;
using Viewer360.View;
using System.Windows.Media.Media3D;
using static PointCloudUtility.CMessageManager;


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
            // Aggiunta callback
            ComponentDispatcher.ThreadIdle += OnApplicationIdle;
            CompositionTarget.Rendering += CompositionTarget_Rendering;

            // Creazione finestra
            m_Window = new View.MainWindow();
            m_Window.viewer360_View.m_Window = m_Window;
            (m_Window.DataContext as ViewModel.MainViewModel).m_Window = m_Window;
            m_Window.Show();


            // Inizializzazione CameraManager (lettura dati da Mapfile creato dal server)
            CViewerCameraManager.Init();

            // Acquisizione parametri command line e apertura file
            string sCatalogNameFull ="";
            string sPhoto360NameFull="";
            string sJasonPath = "";
            if (e.Args.Length == 9)
            {
                sCatalogNameFull = e.Args[0];
                sPhoto360NameFull= e.Args[1];
                sJasonPath= e.Args[2];

                CLabelManager.Init(System.IO.Path.GetDirectoryName(sPhoto360NameFull), sJasonPath);

                await (m_Window.DataContext as ViewModel.MainViewModel).Open(sCatalogNameFull, sPhoto360NameFull, sJasonPath);
            }

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
                CMessageManager.CMessage sMsg = m_oMsgManager.GetMsg();
                if (sMsg.m_Type == MsgType.CameraSelected)  // Messaggio pending è di tipo CameraSelected
                {
                    string sCameraName = "";

                    m_oMsgManager.GetCameraSelected(sMsg.m_sMsg, ref sCameraName);

                    (m_Window.DataContext as ViewModel.MainViewModel).LoadNewImage(sCameraName);
                    m_bNewImageLoaded = true;

                    return;
                }
            }

            if(SharingHelper.m_bPhotoHasChanged)
            {
                //++++++++++++++++++++++++++
                // Console.WriteLine("Da OnApplicationIdle stop1 Type=1");
                //++++++++++++++++++++++++++                // Comunico il nuovo indice di Photo
                int index = (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentPhotoIndex;
                double dX = 1;
                double dY = 0;

                m_Window.viewer360_View.ComputePlanarCameraAt(ref dX, ref dY);
                m_oMsgManager.SendCameraSelected1(index,dX,dY);

                SharingHelper.m_bPhotoHasChanged = false;
                return;
            }

            if (SharingHelper.m_bCameraAtHasChanged)
            {
                //++++++++++++++++++++++++++
                // Console.WriteLine("Da OnApplicationIdle stop1 Type=2");
                //++++++++++++++++++++++++++                // Comunico il nuovo indice di Photo
                // Comunico la nuova direzione della camera al server
                double dX =1;
                double dY=0;

                m_Window.viewer360_View.ComputePlanarCameraAt(ref dX, ref dY);
                m_oMsgManager.SendCameraInfo(dX, dY, m_Window.viewer360_View.Vfov, m_Window.viewer360_View.Hfov);

                SharingHelper.m_bCameraAtHasChanged = false;
                return;
            }


        }

    }
}
