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
using System.Globalization;
using System.Collections.Generic;


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

            // Inizializzazione CProjectManagerData
            CProjectManagerData.InitFromMapFile();

            // Inizializzazione CameraManager (lettura dati posizioni/orientamento camere da Mapfile creato dal server)
            CViewerCameraManager.Init();
            CProjectPlane.Init(m_Window.viewer360_View);

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
            SharingHelper.m_oMsgManager = m_oMsgManager;

            if (CUIManager.m_bDebugMode == false)
            {
                m_oMsgManager.CreateConnection();


                // Attendo che la il Server si connetta
                m_oMsgManager.WaitForConnection();

                // Attivo thread di ricezione; si dovrebbe chiudere da solo all'uscita della finestra.
                CMessageManager.StartListener(m_oMsgManager);
            }

            // Inizializzo UI
            CUIManager.StartUpUI();
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

                if (sMsg.m_Type == MsgType.CastPlaneWall)  // Messaggio pending è di tipo CastPlaneWall
                {
                    double dPosX=0;
                    double dPosY = 0;
                    double dNX = 0;
                    double dNY = 0;
                    int iSide = -1;
                    string sWallName = "";
                    m_oMsgManager.GetCastPlaneWall(sMsg.m_sMsg, ref dPosX, ref dPosY, ref dNX, ref dNY, ref iSide, ref sWallName);



                    if (CUIManager.GetMode() == ViewerMode.Create)  // Modalità Create
                    {
                        //++++++++++++++++++++++++
                        Console.WriteLine(sWallName);
                        Console.WriteLine("vPos="+ dPosX.ToString(CultureInfo.InvariantCulture)+"  "+ dPosY.ToString(CultureInfo.InvariantCulture));
                        Console.WriteLine("vN="+ dNX.ToString(CultureInfo.InvariantCulture)+ "  "+dNX.ToString(CultureInfo.InvariantCulture));
                        Console.WriteLine("iSide="+ iSide.ToString(CultureInfo.InvariantCulture));
                        Console.WriteLine("");
                        //++++++++++++++++++++++++

                        if (dNX <= 1 && CUIManager.GetMode() == ViewerMode.Create)  // Piano valido
                            CProjectPlane.SetPlane(dPosX, dPosY, dNX, dNY, sWallName);
                        else  // Nessun piano identificato
                            CProjectPlane.RemovePlane();
                    }

                    CUIManager.UpdateUI();
                }

                if (m_Window.IsActive == false)  // Il focus è al Server--> processo i messaggi  // ATTENZIONE AI MESSAGGI SUI MURI PER W&D
                {
                    if (sMsg.m_Type == MsgType.CameraSelected)  // Messaggio pending è di tipo CameraSelected
                    {
                        string sCameraName = "";
                        double dAtX=0, dAtY=0, dAtZ=0;
                        m_oMsgManager.GetCameraSelected(sMsg.m_sMsg, ref sCameraName, ref dAtX, ref dAtY, ref dAtZ);

                        (m_Window.DataContext as ViewModel.MainViewModel).LoadNewImage(sCameraName);
                        CUIManager.SetViewerMode(ViewerMode.Create);
                        m_bNewImageLoaded = true;

                        return;
                    }
                    else if (sMsg.m_Type == MsgType.ElementSelected)
                    {
                        m_oMsgManager.DisableSending();
                        string sElementName = "";

                        m_oMsgManager.GetElementSelected(sMsg.m_sMsg, ref sElementName);

                        (m_Window.DataContext as ViewModel.MainViewModel).LoadLabelForSelectedElement(sElementName);
                        CUIManager.SetViewerMode(ViewerMode.Edit);
                        m_oMsgManager.EnableSending();
                        return;
                    }
                    else if (sMsg.m_Type == MsgType.ElementDeletedWarning)
                    {
                        CLabelManager.ReloadLabel();
                        (m_Window.DataContext as ViewModel.MainViewModel).GetClosestLabel();

                        return;
                    }
                    else if (sMsg.m_Type == MsgType.CloudClickRequest)
                    {
                        m_Window.viewer360_View.SaveImageAndJsonForCloudClick();
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
                m_oMsgManager.SendCameraAndLabelSelected(index, dX, dY, m_Window.viewer360_View.Vfov, m_Window.viewer360_View.Hfov, sElementlName);

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
                m_oMsgManager.SendCameraSelected1(index, dX, dY);  // Nota: nel caso planare i valori inviati di dX e dY non saranno utilizzati

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
                int iPhotoIndex=-1;
                int iLabelIndex=-1;
                CLabelManager.GetSelectionIndex(ref iPhotoIndex, ref iLabelIndex);

                iPhotoIndex = (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentPhotoIndex;
                iLabelIndex = (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentLabelIndex;

                string sSelElementName = "";

                if(iPhotoIndex>=0 && iLabelIndex>=0)
                    sSelElementName=CLabelManager.GetLabelName(iPhotoIndex, iLabelIndex);

                m_oMsgManager.SendElementDeletedWarning(sSelElementName);
                SharingHelper.m_bElementDeleted = false;
                return;
            }

            if (SharingHelper.m_bLabelAdded)
            {
                m_oMsgManager.SendLabelAddedWarning();
                SharingHelper.m_bLabelAdded = false;
                return;
            }
            

            if (SharingHelper.m_iSendCategoryToServer>=0)
            {
                m_oMsgManager.SendCategoryToServer(SharingHelper.m_iSendCategoryToServer,(int)CUIManager.GetMode());
                SharingHelper.m_iSendCategoryToServer = -1;
                return;
            }

            if(SharingHelper.m_oMsgInfo1!=null)
            {
                CSingleFileLabel oLabel = SharingHelper.m_oMsgInfo1.m_sLabel;
                string sLabelFileName = oLabel.m_sJsonFileName;
                string sLabelName = oLabel.m_aLabelInfo[0].sLabelName;
                string sParentEl = oLabel.m_aLabelInfo[0].sParentElementName;
//                string sCameraName= oLabel.m_aLabelInfo[0].m_sC
                int iCatalogID= oLabel.m_aLabelInfo[0].iObjCatalogID;
                int iCategory = oLabel.m_aLabelInfo[0].iCategory;

                double[] aCameraPos = new double[3];
                if (m_Window.viewer360_View.MyCam != null)
                {
                    aCameraPos[0] = m_Window.viewer360_View.MyCam.Position.X;
                    aCameraPos[1] = m_Window.viewer360_View.MyCam.Position.Y;
                    aCameraPos[2] = m_Window.viewer360_View.MyCam.Position.Z;
                }
                else
                {
                    aCameraPos[0] = m_Window.viewer360_View.MyOrthoCam.Position.X;
                    aCameraPos[1] = m_Window.viewer360_View.MyOrthoCam.Position.Y;
                    aCameraPos[2] = m_Window.viewer360_View.MyOrthoCam.Position.Z;
                }

                double[] aPos0 = new double[3];
                aPos0[0] = oLabel.m_aLabelInfo[0].aPolyPointX[0];
                aPos0[1] = oLabel.m_aLabelInfo[0].aPolyPointY[0];
                aPos0[2] = oLabel.m_aLabelInfo[0].aPolyPointZ[0];

                double[] aPos1= new double[3];
                aPos1[0] = oLabel.m_aLabelInfo[0].aPolyPointX[1];
                aPos1[1] = oLabel.m_aLabelInfo[0].aPolyPointY[1];
                aPos1[2] = oLabel.m_aLabelInfo[0].aPolyPointZ[1];

                double[] aPos2 = new double[3];
                aPos2[0] = oLabel.m_aLabelInfo[0].aPolyPointX[2];
                aPos2[1] = oLabel.m_aLabelInfo[0].aPolyPointY[2];
                aPos2[2] = oLabel.m_aLabelInfo[0].aPolyPointZ[2];

                double[] aPos3 = new double[3];
                aPos3[0] = oLabel.m_aLabelInfo[0].aPolyPointX[3];
                aPos3[1] = oLabel.m_aLabelInfo[0].aPolyPointY[3];
                aPos3[2] = oLabel.m_aLabelInfo[0].aPolyPointZ[3];

                m_oMsgManager.SendBuildNewElementRequest(sLabelFileName, sLabelName, sParentEl, iCatalogID, iCategory, aCameraPos,
                                                         aPos0,aPos1,aPos2,aPos3);

                SharingHelper.m_oMsgInfo1 = null;
                return;
            }

            if(SharingHelper.m_bSendInfoForCloudClick == true)
            {
                m_oMsgManager.SendCloudClickInfoToViewer(SharingHelper.m_sNewJsonFileName, SharingHelper.m_sViewerPngFile, SharingHelper.m_sCloudClickPngFile);
                SharingHelper.m_bSendInfoForCloudClick = false;
                return;
            }
        }
    }
}
