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
using static Viewer360.View.CUIManager;
using System.Xml.Linq;


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


        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Aggiunta callback
            ComponentDispatcher.ThreadIdle += OnApplicationIdle;
            CompositionTarget.Rendering += CompositionTarget_Rendering;

            // Creazione finestra
            m_Window = new View.MainWindow();
            m_Window.viewer360_View.m_Window = m_Window;
            (m_Window.DataContext as ViewModel.MainViewModel).m_Window = m_Window;
            CUIManager.m_Window = m_Window;
            CUIManager.m_bDebugMode = false;


            CUIManager.Init();


            // Inizializzazione CameraManager (lettura dati posizioni/orientamento camere da Mapfile creato dal server)
            CViewerCameraManager.Init();

            // Acquisizione parametri command line e apertura file
            string sCatalogNameFull = "";
            string sPhotoFileNameFull = "";
            string sJasonPath = "";
            string sSegmentPath = "";

            if (e.Args.Length == 5)
            {
                if (e.Args[0] == "Planar")
                    m_Window.viewer360_View.SetProjection(Viewer360View.ViewerProjection.Planar);
                else
                    m_Window.viewer360_View.SetProjection(Viewer360View.ViewerProjection.Spheric);

                sCatalogNameFull = e.Args[1];
                sPhotoFileNameFull = e.Args[2];
                sJasonPath = e.Args[3];
                sSegmentPath = e.Args[4];
                CLabelManager.Init(System.IO.Path.GetDirectoryName(sPhotoFileNameFull), sJasonPath);

                SharingHelper.SetFileAndFolderNames(sPhotoFileNameFull, sJasonPath, sSegmentPath);
                SharingHelper.LoadCatalogManager(sCatalogNameFull);

                m_Window.viewer360_View.InitCamera();
                (m_Window.DataContext as ViewModel.MainViewModel).LoadImage(sPhotoFileNameFull);
            }

            // Inizializzo la messaggistica col server
            m_oMsgManager = new CMessageManager(CMessageManager.PipeType.Client);

            if (CUIManager.m_bDebugMode == false)
            {
                m_oMsgManager.CreateConnection();


                // Attendo che la il Server si connetta
                m_oMsgManager.WaitForConnection();

                // Attivo thread di ricezione; si dovrebbe chiudere da solo all'uscita della finestra.
                CMessageManager.StartListener(m_oMsgManager);
            }

            // Inizializzo UI
            m_Window.InitUI();
            CUIManager.SetViewerMode(ViewerMode.Create);
            m_Window.Show();

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

            if (m_oMsgManager != null && m_oMsgManager.PendingMsg())
            {
                CMessageManager.CMessage sMsg = m_oMsgManager.GetMsg();

                if (m_Window.IsActive == false)  // Il focus è al Server--> processo i messaggi  // ATTENZIONE AI MESSAGGI SUI MURI PER W&D
                {
                    if (sMsg.m_Type == MsgType.CameraSelected)  // Messaggio pending è di tipo CameraSelected
                    {
                        string sCameraName = "";

                        m_oMsgManager.GetCameraSelected(sMsg.m_sMsg, ref sCameraName);

                        (m_Window.DataContext as ViewModel.MainViewModel).LoadNewImage(sCameraName);
                        CUIManager.SetViewerMode(ViewerMode.Create);
                        m_bNewImageLoaded = true;

                        return;
                    }
                    else if (sMsg.m_Type == MsgType.ElementSelected)
                    {
                        string sElementName = "";

                        m_oMsgManager.GetElementSelected(sMsg.m_sMsg, ref sElementName);

                        (m_Window.DataContext as ViewModel.MainViewModel).LoadLabelForSelectedElement(sElementName);
                        CUIManager.SetViewerMode(ViewerMode.Edit);
                        return;
                    }
                    else if (sMsg.m_Type == MsgType.ElementDeletedWarning)
                    {
                        CLabelManager.ReloadLabel();
                        (m_Window.DataContext as ViewModel.MainViewModel).GetClosestLabel();

                        return;
                    }
                }
            }


            if (SharingHelper.m_bLabelHasChanged)
            {
                int index = (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentPhotoIndex;
                double dX = 1;
                double dY = 0;
                string sElementlName = (m_Window.DataContext as ViewModel.MainViewModel).m_oCurrentLabel.m_sJsonFileName;
                sElementlName = System.IO.Path.GetFileNameWithoutExtension(sElementlName);

                m_Window.viewer360_View.ComputePlanarCameraAt(ref dX, ref dY);
                m_oMsgManager.SendCameraAndLabelSelected(index, dX, dY, sElementlName);

                SharingHelper.m_bLabelHasChanged = false;
                return;
            }

            if (SharingHelper.m_bPhotoHasChanged)
            {
                //++++++++++++++++++++++++++
                // Console.WriteLine("Da OnApplicationIdle stop1 Type=1");
                //++++++++++++++++++++++++++                // Comunico il nuovo indice di Photo
                int index = (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentPhotoIndex;
                double dX = 1;
                double dY = 0;

                m_Window.viewer360_View.ComputePlanarCameraAt(ref dX, ref dY);
                m_oMsgManager.SendCameraSelected1(index, dX, dY);

                SharingHelper.m_bPhotoHasChanged = false;
                SharingHelper.m_bCameraAtHasChanged = false; // Per evitare doppio messaggio
                return;
            }

            if (SharingHelper.m_bCameraAtHasChanged)
            {
                //++++++++++++++++++++++++++
                // Console.WriteLine("Da OnApplicationIdle stop1 Type=2");
                //++++++++++++++++++++++++++                // Comunico il nuovo indice di Photo
                // Comunico la nuova direzione della camera al server
                double dX = 1;
                double dY = 0;

                m_Window.viewer360_View.ComputePlanarCameraAt(ref dX, ref dY);
                m_oMsgManager.SendCameraInfo(dX, dY, m_Window.viewer360_View.Vfov, m_Window.viewer360_View.Hfov);

                SharingHelper.m_bCameraAtHasChanged = false;
                return;
            }

            if (SharingHelper.m_bElementDeleted)
            {
                m_oMsgManager.SendElementDeletedWarning();
                SharingHelper.m_bElementDeleted = false;
                return;
            }

            if (SharingHelper.m_bLabelAdded)
            {
                m_oMsgManager.SendLabelAddedWarning();
                SharingHelper.m_bLabelAdded = false;
                return;
            }
            

            if (SharingHelper.m_bParentWallRequested)
            {
                m_oMsgManager.SendParentWallRequested();
                SharingHelper.m_bParentWallRequested = false;
                return;
            }
        }
    }
}
