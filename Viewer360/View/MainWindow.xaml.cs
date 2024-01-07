using Microsoft.Win32;
using Viewer360.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using PointCloudUtility;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using static PointCloudUtility.CSingleFileLabel;
using System.Windows.Forms;
using static PointCloudUtility.CommonUtil;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static Viewer360.View.CUIManager;
using System.Globalization;
using HelixToolkit.Wpf;
using System.Net;


namespace Viewer360.View
{
    class SPointInfo
    {
        public int iPointIndex=-1;
        public Ellipse ellipse=null;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {

        private bool isDragging;
        private int  iDraggingPoint;
        private Point offset;
        private Point vViewfinderCentre;
        private Point vVewfinderBBox;
        private PointCollection aPointTmp;
        private List<int> aItemDefaultEntry;
        private Point []  m_aOriginalPolygon;

        List<Ellipse> m_EllipseList;
        int m_iEllipseIncrementalNum;
        Canvas myCanvas;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            iDraggingPoint = -1;

            CUIManager.m_Window = this;
            CUIManager.m_bDebugMode = false;
            CUIManager.Init(ViewFinderPolygon, myGrid);

            aItemDefaultEntry = new List<int>();

            // If the window style is set to none when the window is maximized, the taskbar will not
            // be covered. Therefore, the window is restored to normal and maximized again.
            DependencyPropertyDescriptor d = DependencyPropertyDescriptor.FromProperty(
                System.Windows.Window.WindowStyleProperty,typeof(System.Windows.Window));
            d.AddValueChanged(this, (sender, args) =>
            {
                System.Windows.Window w = (System.Windows.Window)sender;
                if (w.WindowStyle == System.Windows.WindowStyle.None)
                {
                    w.WindowState = System.Windows.WindowState.Normal;
                    w.WindowState = System.Windows.WindowState.Maximized;
                }
            });

        }

        public void InitUI()
        {
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();

            int iDefEntry;
            for (int iCat = 1; iCat < oLList.Count; iCat++)
            {
                CategoryCombo.Items.Add(oLList[iCat][0].sCategory);

                iDefEntry = 0;
                for (int iItem = 0; iItem< oLList[iCat].Count; iItem++)
                {
                    if (oLList[iCat][iItem].bUIDefEntry)
                    {
                        iDefEntry = iItem;
                        break;
                    }
                }
                aItemDefaultEntry.Add(iDefEntry);
            }

            CategoryCombo.SelectedIndex = 2;  // Wall

        }

        private void CategorySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();

            ItemCombo.Items.Clear();
            int iSelectedCatIndex = CategoryCombo.SelectedIndex+1; // Ho escluso la categoria 0 

            for (int iItem = 0; iItem < oLList[iSelectedCatIndex].Count; iItem++)
                ItemCombo.Items.Add(oLList[iSelectedCatIndex][iItem].sUI_CategoryInfo);

            ItemCombo.SelectedIndex = aItemDefaultEntry[CategoryCombo.SelectedIndex];
            ElementName.Text = CategoryCombo.SelectedItem.ToString();

            int iCat = oLList[iSelectedCatIndex][0].nCategory;
            CUIManager.SetCurrentCategory(iCat);
            CUIManager.UpdateUI();

            SharingHelper.m_iSendCategoryToServer = iCat;

        }
        private void ItemSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        public CSingleFileLabel BuildSavingLabelCandidatePlanar(string sNewJsonFileName, Size ViewSize, Size ImageSize)
        {
            // Creo file manager per file attuale
            CSingleFileLabel oLabel = new CSingleFileLabel();
            oLabel.m_sJsonAuthor = "ScanToBim-Viewer360_Planar";
//            oLabel.SetImageSize((int)(ImageSize.Height), (int)(ImageSize.Width));// INVERTO PER COMPATIBILITA' CON SCISSOR!!!!  VERIFICARE
            oLabel.SetImageSize((int)(ImageSize.Width), (int)(ImageSize.Height));
            oLabel.m_dTheta = 0;
            oLabel.m_dPhi = 0;
            oLabel.m_vLocalAtX = 2;
            oLabel.m_vLocalAtY = 2;
            oLabel.m_vLocalAtZ = 2;
            oLabel.m_hFov = -1;
            oLabel.m_vFov = -1;
            oLabel.m_sJsonFileName = System.IO.Path.GetFileName(sNewJsonFileName);

            // Creo e inizializzo LabelInfo
            CSingleFileLabel.SLabelInfo oLabelInfo = new CSingleFileLabel.SLabelInfo();

            // Aggiungo nome file .png
            //            oLabelInfo.sImageFileName = sNewFileName;
            oLabelInfo.sLabelName = ElementName.Text;
            oLabelInfo.sParentLabelName = ""; // TODO il valore va ricevuto dal Server 

            // Aggiungo la category
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();
            int iSelectedCat = CategoryCombo.SelectedIndex + 1; // Ho escluso la categoria 0 --> devo aggiungere 1 agli indici
            int iSelectedItem = ItemCombo.SelectedIndex;

            // Aggiungo il catalogID
            oLabelInfo.iObjCatalogID = oLList[iSelectedCat][iSelectedItem].nId;
            oLabelInfo.iCategory = SharingHelper.GetCatalogManager().SearchCategoryByID(oLabelInfo.iObjCatalogID);

            // Aggiungo i punti
            oLabelInfo.aPolyPointX = new List<double>();
            oLabelInfo.aPolyPointY = new List<double>();
            double dConvFactorX = ImageSize.Width / ViewSize.Width;
            double dConvFactorY = ImageSize.Height / ViewSize.Height;
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                oLabelInfo.aPolyPointX.Add((float)(ViewFinderPolygon.Points[i].X * dConvFactorX));
                oLabelInfo.aPolyPointY.Add((float)(ViewFinderPolygon.Points[i].Y * dConvFactorY));
            }

            // Aggiungo LabelInfo a LabelManager
            oLabel.Add(oLabelInfo);

            return oLabel;

        }

        public CSingleFileLabel BuildSavingLabelCandidate(string sNewJsonFileName, Size ViewSize, double dTheta, double dPhi, double dVFov, double dHFov, Vector3D vLookDirection)
        {
            // Creo file manager per file attuale
            CSingleFileLabel oLabel = new CSingleFileLabel();
            oLabel.m_sJsonAuthor = "ScanToBim-Viewer360";
            oLabel.SetImageSize((int)(ViewSize.Width* SharingHelper.m_dConvFactor), (int)(ViewSize.Height * SharingHelper.m_dConvFactor));  
            oLabel.m_dTheta = dTheta;
            oLabel.m_dPhi = dPhi;
            oLabel.m_vLocalAtX = vLookDirection.X;
            oLabel.m_vLocalAtY = vLookDirection.Y;
            oLabel.m_vLocalAtZ = vLookDirection.Z;
            oLabel.m_hFov = dHFov;
            oLabel.m_vFov = dVFov;
            oLabel.m_sJsonFileName = System.IO.Path.GetFileName(sNewJsonFileName);
            Matrix3D mTmp=viewer360_View.ComputeCameraRTMatrix(true);
            oLabel.m_MatrixRT[0] = mTmp.M11;
            oLabel.m_MatrixRT[1] = mTmp.M12;
            oLabel.m_MatrixRT[2] = mTmp.M13;
            oLabel.m_MatrixRT[3] = mTmp.M14;
            oLabel.m_MatrixRT[4] = mTmp.M21;
            oLabel.m_MatrixRT[5] = mTmp.M22;
            oLabel.m_MatrixRT[6] = mTmp.M23;
            oLabel.m_MatrixRT[7] = mTmp.M24;
            oLabel.m_MatrixRT[8] = mTmp.M31;
            oLabel.m_MatrixRT[9] = mTmp.M22;
            oLabel.m_MatrixRT[10] = mTmp.M33;
            oLabel.m_MatrixRT[11] = mTmp.M34;
            oLabel.m_MatrixRT[12] = mTmp.OffsetX;
            oLabel.m_MatrixRT[13] = mTmp.OffsetY;
            oLabel.m_MatrixRT[14] = mTmp.OffsetZ;
            oLabel.m_MatrixRT[15] = mTmp.M44;


            // Creo e inizializzo LabelInfo
            CSingleFileLabel.SLabelInfo oLabelInfo = new CSingleFileLabel.SLabelInfo();

            // Aggiungo nome file .png
            //            oLabelInfo.sImageFileName = sNewFileName;
            oLabelInfo.sLabelName = ElementName.Text;
            oLabelInfo.sParentLabelName = ""; // TODO il valore va ricevuto dal Server 

            // Aggiungo la category
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();
            int iSelectedCat = CategoryCombo.SelectedIndex + 1; // Ho escluso la categoria 0 --> devo aggiungere 1 agli indici
            int iSelectedItem = ItemCombo.SelectedIndex;

            // Aggiungo il catalogID
            oLabelInfo.iObjCatalogID = oLList[iSelectedCat][iSelectedItem].nId;
            oLabelInfo.iCategory = SharingHelper.GetCatalogManager().SearchCategoryByID(oLabelInfo.iObjCatalogID);

            // Aggiungo i punti
            oLabelInfo.aPolyPointX = new List<double>();
            oLabelInfo.aPolyPointY = new List<double>();
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                oLabelInfo.aPolyPointX.Add((float)(ViewFinderPolygon.Points[i].X * SharingHelper.m_dConvFactor));
                oLabelInfo.aPolyPointY.Add((float)(ViewFinderPolygon.Points[i].Y * SharingHelper.m_dConvFactor));
            }

            // Aggiungo LabelInfo a LabelManager
            oLabel.Add(oLabelInfo);

            return oLabel;

        }

        public void SaveJason(CSingleFileLabel oLabel, string sNewJsonFileName)
        {

            // Salvo file .json
            string sJpegFileName = System.IO.Path.GetFileName(SharingHelper.GetFullJpegFileName());
            oLabel.SaveToJsonFile(sNewJsonFileName, sJpegFileName);

            // Aggiorno CLabelManager
            CLabelManager.AddLabel(oLabel);
        }

        private void NextImage_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ViewModel.MainViewModel).NextImage_Click(sender, e);
        }
        private void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ViewModel.MainViewModel).PrevImage_Click(sender, e);
        }
        private void NextLabel_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ViewModel.MainViewModel).NextLabel_Click(sender, e);
        }
        private void NewLabel_Click(object sender, RoutedEventArgs e)
        {
            m_Window.CreateMode.IsChecked = true;
            CUIManager.ChangeMode();
        }
        private void PrevLabel_Click(object sender, RoutedEventArgs e)
        {
            (DataContext as ViewModel.MainViewModel).PrevLabel_Click(sender, e);
        }
        private void ChangeMode_Click(object sender, RoutedEventArgs e)
        {
            CUIManager.ChangeMode();
        }
        private void DeleteLabel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult result = ConfirmDialog("La label corrente verrà cancellata; anche le eventuali entità derivate da essa saranno eliminate.\n\n\nConfermi?");
            if (result == System.Windows.Forms.DialogResult.No)
                return;

            CSingleFileLabel oCurrentLabel=(DataContext as MainViewModel).m_oCurrentLabel;
            string sJsonFileName = System.IO.Path.GetFileNameWithoutExtension(oCurrentLabel.m_sJsonFileName);

            try
            {
                // Cancello i file  che iniziano con sJsonFileName in JsonPath  (.jSon e .Cif)
                var files = Directory.GetFiles(SharingHelper.GetJsonPath(), sJsonFileName + "*.*");
                foreach (string file in files)
                {
                    System.IO.File.SetAttributes(file, FileAttributes.Normal);
                    System.IO.File.Delete(file);
                }

                // Cancello tutti i file che contengono XXXXXXXX##YY in SegmentedPC (.pcd, .wif, .ply e .lbi)
                files = Directory.GetFiles(SharingHelper.GetSegmentPath(), "* -^- " + sJsonFileName + "*.*");
                foreach (string file in files)
                {
                    System.IO.File.SetAttributes(file, FileAttributes.Normal);
                    System.IO.File.Delete(file);

                }
                SharingHelper.m_bElementDeleted = true;
            }
            catch (IOException ex)
            {
                return;
            }

            // TODO  Mando messaggio di cancellazione a server

            // Elimino la label dalla lista 
            CLabelManager.RemoveLabel(oCurrentLabel);

            // Cerco nuova label
            (DataContext as MainViewModel).m_iCurrentLabelIndex = -1;
            (DataContext as MainViewModel).GetClosestLabel();
            // Se non esiste alcuna label ripristino la modalità precedente
            if ((DataContext as ViewModel.MainViewModel).m_iCurrentLabelIndex == -1)
            {
                CUIManager.SetViewerMode(ViewerMode.Create);
                return;
            }


        }
        private void SaveLabel_Click(object sender, RoutedEventArgs e)
        {
            viewer360_View.SaveImageAndJson();
        }
        private void LaunchAI_Click(object sender, RoutedEventArgs e)
        { 
        }

        private void Project2Plane_Click(object sender, RoutedEventArgs e)
        {
            Ray3D oRay;

            Point3D[] aPoint = new Point3D[4];
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                oRay = Viewport3DHelper.GetRay(viewer360_View.vp, ViewFinderPolygon.Points[i]);
                aPoint[i] = (Point3D)CProjectPlane.GetIntersection(oRay);
                aPoint[i].Z = -aPoint[i].Z;  // Per ragioni misteriose l'oggetto oRay è ribaltato rispetto a Z e quindi anche il punto di intersezione col piano (verticale!)
                aPoint[i] = viewer360_View.PointLoc2Glob(aPoint[i]);
            }

            double dZMax = (aPoint[0].Z + aPoint[1].Z) / 2;
            aPoint[0].Z = dZMax;
            aPoint[1].Z = dZMax;

            double dZMin = (aPoint[2].Z + aPoint[3].Z) / 2;
            aPoint[2].Z = dZMin;
            aPoint[3].Z = dZMin;

            double dXLeft = (aPoint[0].X + aPoint[3].X) / 2;
            aPoint[0].X = dXLeft;
            aPoint[3].X = dXLeft;
            double dYLeft = (aPoint[0].Y + aPoint[3].Y) / 2;
            aPoint[0].Y = dYLeft;
            aPoint[3].Y = dYLeft;


            double dXRight = (aPoint[1].X + aPoint[2].X) / 2;
            aPoint[1].X = dXRight;
            aPoint[2].X = dXRight;
            double dYRight = (aPoint[1].Y + aPoint[2].Y) / 2;
            aPoint[1].Y = dYRight;
            aPoint[2].Y = dYRight;

            for (int i = 0; i < 4; i++)
                aPoint[i] = viewer360_View.PointGlob2Loc(aPoint[i]);

            // Memorizzo 
            CProjectPlane.m_aFace3DPoint=aPoint;

            // Aggiorno il mirino
            UpdateViewPolygonFromFace3D();

            //CUIManager.SetViewerMode(ViewerMode.Edit);
//            CUIManager.ChangeMode();
        }
    }
}
