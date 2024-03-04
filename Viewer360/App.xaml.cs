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
using static Viewer360.View.Viewer360View;


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
            SharingHelper.m_nIdleCount++;

            // *************** Ricezione messaggi *********************
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
                    float fThickness = -1;
                    m_oMsgManager.GetCastPlaneWall(sMsg.m_sMsg, ref dPosX, ref dPosY, ref dNX, ref dNY, ref iSide, ref sWallName, ref fThickness);



                    if (CUIManager.GetMode() == ViewerMode.Create)  // Modalità Create
                    {
                        //++++++++++++++++++++++++
                        //Console.WriteLine(sWallName);
                        //Console.WriteLine("vPos="+ dPosX.ToString(CultureInfo.InvariantCulture)+"  "+ dPosY.ToString(CultureInfo.InvariantCulture));
                        //Console.WriteLine("vN="+ dNX.ToString(CultureInfo.InvariantCulture)+ "  "+dNX.ToString(CultureInfo.InvariantCulture));
                        //Console.WriteLine("iSide="+ iSide.ToString(CultureInfo.InvariantCulture));
                        //Console.WriteLine("");
                        //++++++++++++++++++++++++

                        if (dNX <= 1 && CUIManager.GetMode() == ViewerMode.Create)  // Piano valido
                            CProjectPlane.SetPlane(dPosX, dPosY, dNX, dNY, sWallName, fThickness);
                        else  // Nessun piano identificato
                            CProjectPlane.RemovePlane();
                    }

                    CUIManager.UpdateUI();
                }
                else if (sMsg.m_Type == MsgType.ReloadLabelRequest)
                {
                    m_oMsgManager.DisableSending();
                    string sElementName = "";

                    m_oMsgManager.GetReloadLabelRequest(sMsg.m_sMsg, ref sElementName);
                    CLabelManager.ReloadSingleLabel(CProjectManagerData.GetLabelsFolderName() + sElementName);
                    int index = sElementName.IndexOf(".");
                    sElementName = sElementName.Substring(0, index);
                    (m_Window.DataContext as ViewModel.MainViewModel).LoadLabelForSelectedElement(sElementName);

                    m_oMsgManager.EnableSending();

                    SharingHelper.m_nIdleCount--;
                    return;
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

                        SharingHelper.m_nIdleCount--;

                        if (m_Window.viewer360_View.GetProjection() == ViewerProjection.Planar)
                            SharingHelper.m_bCameraAtHasChanged = true;

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

                        SharingHelper.m_nIdleCount--;
                        return;
                    }
                    else if (sMsg.m_Type == MsgType.ElementDeletedWarning)
                    {
                        CLabelManager.ReloadLabelSet();
                        //(m_Window.DataContext as ViewModel.MainViewModel).GetClosestLabel();

                        // Ripristino la modalità Create
                        CUIManager.SetViewerMode(ViewerMode.Create);

                        SharingHelper.m_nIdleCount--;
                        return;
                    }
                    else if (sMsg.m_Type == MsgType.CloudClickRequest)
                    {
                        m_Window.viewer360_View.SaveImageAndJsonForCloudClick();

                        SharingHelper.m_nIdleCount--;
                        return;
                    }
                }
            }


            // *************** Invio messaggi *********************
            if (SharingHelper.m_bLabelHasChanged)
            {
                int index = (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentPhotoIndex;
                double dX = 1;
                double dY = 0;
                string sElementlName = (m_Window.DataContext as ViewModel.MainViewModel).m_oCurrentLabel.m_sJsonFileName;
                sElementlName = System.IO.Path.GetFileNameWithoutExtension(sElementlName);

                m_Window.viewer360_View.ComputePlanarCameraAt(ref dX, ref dY);

                List<MyVector3D> avPoint;
                int iThetaMin = 0;
                int iThetaMax = 0;
                CUIManager.ComputeVFVersor(out avPoint, ref iThetaMin, ref iThetaMax);

                List<MyVector3D> avFrustPoint;
                int iFrustThetaMin = 0;
                int iFrustThetaMax = 0;
                CUIManager.ComputeFrustumVersor(out avFrustPoint, ref iFrustThetaMin, ref iFrustThetaMax);

                MyVector3D vCenter=new MyVector3D(SharingHelper.GetCameraPos().X, SharingHelper.GetCameraPos().Y, SharingHelper.GetCameraPos().Z);
                m_oMsgManager.SendCameraAndLabelSelected(index, dX, dY, m_Window.viewer360_View.Vfov, m_Window.viewer360_View.Hfov,
                     vCenter, avPoint, iThetaMin, iThetaMax, avFrustPoint, iFrustThetaMin, iFrustThetaMax,sElementlName, (int)CUIManager.GetMode());

                SharingHelper.m_bLabelHasChanged = false;

                SharingHelper.m_nIdleCount--;
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
                m_oMsgManager.SendPhotoChangedOnViewer(index, dX, dY,(int)CUIManager.GetMode());  // Nota: nel caso planare i valori inviati di dX e dY non saranno utilizzati

                SharingHelper.m_bPhotoHasChanged = false;
                if (m_Window.viewer360_View.GetProjection() == ViewerProjection.Planar)
                    SharingHelper.m_bCameraAtHasChanged = true;
                else
                    SharingHelper.m_bCameraAtHasChanged = false; // Per evitare doppio messaggio

                SharingHelper.m_nIdleCount--;
                return;
            }

            if (SharingHelper.m_bCameraAtHasChanged)
            {
                // Comunico la nuova direzione della camera al server
                double dX = 1;
                double dY = 0;

                List<MyVector3D> avPoint;
                int iThetaMin=0;
                int iThetaMax=0;
                CUIManager.ComputeVFVersor(out avPoint, ref iThetaMin, ref iThetaMax);

                List<MyVector3D> avFrustPoint;
                int iFrustThetaMin = 0;
                int iFrustThetaMax = 0;
                CUIManager.ComputeFrustumVersor(out avFrustPoint, ref iFrustThetaMin, ref iFrustThetaMax);

                m_Window.viewer360_View.ComputePlanarCameraAt(ref dX, ref dY);

                //++++++++++++++++++++++++++++++++
                /*
                double fX = avPoint[0].X;
                double fY = avPoint[0].Y;
                double fDen = Math.Sqrt(fX*fX+fY*fY);
                fX/= fDen;
                fY/= fDen;
                double fCosAngle = avPoint[0] * avPoint[1];
                */
                //++++++++++++++++++++++++++++++++
                MyVector3D vCenter = new MyVector3D(SharingHelper.GetCameraPos().X, SharingHelper.GetCameraPos().Y, SharingHelper.GetCameraPos().Z);
                m_oMsgManager.SendCameraInfo(dX, dY, m_Window.viewer360_View.Vfov, m_Window.viewer360_View.Hfov, vCenter,
                    avPoint,iThetaMin, iThetaMax, avFrustPoint, iFrustThetaMin, iFrustThetaMax);

                SharingHelper.m_bCameraAtHasChanged = false;

                SharingHelper.m_nIdleCount--;
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

                SharingHelper.m_nIdleCount--;
                return;
            }

            if (SharingHelper.m_bLabelAdded)  // Aggiunta label di tipo .json (Wall/floor/Ceiling)
            {
                m_oMsgManager.SendLabelAddedWarning();
                SharingHelper.m_bLabelAdded = false;

                SharingHelper.m_nIdleCount--;
                return;
            }
            

            if (SharingHelper.m_oSendCategoryToServer != null)
            {
                m_oMsgManager.SendCategoryToServer(SharingHelper.m_oSendCategoryToServer.m_iCategory, SharingHelper.m_oSendCategoryToServer.m_iCatalogObjId, (int)CUIManager.GetMode());
                SharingHelper.m_oSendCategoryToServer = null;

                SharingHelper.m_nIdleCount--;
                return;
            }

            if(SharingHelper.m_oMsgInfo1!=null)  // Richiesta costruzione nuovo elemento
            {
                CSingleFileLabel oLabel = SharingHelper.m_oMsgInfo1.m_sLabel;
                string sLabelFileName = oLabel.m_sJsonFileName;
                string sLabelName = oLabel.m_aLabelInfo[0].sLabelName;
                string sParentEl = oLabel.m_aLabelInfo[0].sParentElementName;
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
                                                         aPos0,aPos1,aPos2,aPos3, SharingHelper.m_oMsgInfo1.m_fThickness, (int)CUIManager.GetMode());

                SharingHelper.m_oMsgInfo1 = null;

                SharingHelper.m_nIdleCount--;
                return;
            }

            if(SharingHelper.m_bSendInfoForCloudClick == true)
            {
                m_oMsgManager.SendCloudClickInfoToViewer(SharingHelper.m_sNewJsonFileName, SharingHelper.m_sViewerPngFile, SharingHelper.m_sCloudClickPngFile);
                SharingHelper.m_bSendInfoForCloudClick = false;

                SharingHelper.m_nIdleCount--;
                return;
            }
            SharingHelper.m_nIdleCount--;
        }
    }
}
