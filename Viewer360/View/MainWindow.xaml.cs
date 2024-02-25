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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            //iDraggingPoint = -1;

            CUIManager.m_Window = this;
            CUIManager.m_bDebugMode = false;
            CUIManager.Init(ViewFinderPolygon, myGrid);

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

        private void Polygon_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            viewer360_View.vp_MouseDown(sender, e);
        }

        private void Polygon_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            viewer360_View.vp_MouseUp(sender, e);
        }

        private void Polygon_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            viewer360_View.vp_MouseWheel(sender, e);
        }

        private void Polygon_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            viewer360_View.vp_MouseMove(sender, e);
        }

        private void CategorySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            CUIManager.CategorySelectionChanged();
        }

        private void FamilySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            CUIManager.FamilySelectionChanged();
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
            CSingleFileLabel.SLabelInfo oLabelInfo = new CSingleFileLabel.SLabelInfo(true);

            // Aggiungo nome file .png
            //            oLabelInfo.sImageFileName = sNewFileName;
            oLabelInfo.sLabelName = ElementName.Text;
            if (CProjectPlane.m_bPlaneDefined)
                oLabelInfo.sParentElementName = CProjectPlane.m_sWallName;
            else
                oLabelInfo.sParentElementName = "";

            // Aggiungo la category
            /*
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();
            int iSelectedCat = CategoryCombo.SelectedIndex + 1; // Ho escluso la categoria 0 --> devo aggiungere 1 agli indici
            int iSelectedItem = CategoryCombo.SelectedIndex;
            

            // Aggiungo il catalogID
            oLabelInfo.iObjCatalogID = oLList[iSelectedCat][iSelectedItem].nId;
            oLabelInfo.iCategory = SharingHelper.GetCatalogManager().SearchCategoryByID(oLabelInfo.iObjCatalogID);
            */

            // Aggiungo la category
            string sCategorySelected = CategoryCombo.SelectedValue.ToString();
            oLabelInfo.iCategory = SharingHelper.GetCatalogManager().GetCategoryDictionary()[sCategorySelected];

            // Aggiungo il catalogID
            oLabelInfo.iObjCatalogID=CUIManager.m_aObjId[ItemCombo.SelectedIndex];

            // Aggiungo i punti
            oLabelInfo.aPolyPointX = new List<double>();
            oLabelInfo.aPolyPointY = new List<double>();

            if (CProjectPlane.m_bPlaneDefined)
            {
                oLabelInfo.aPolyPointZ = new List<double>();
                for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                {
                    oLabelInfo.aPolyPointX.Add((float)(CProjectPlane.m_aFace3DPointGlob[i].X));
                    oLabelInfo.aPolyPointY.Add((float)(CProjectPlane.m_aFace3DPointGlob[i].Y));
                    oLabelInfo.aPolyPointZ.Add((float)(CProjectPlane.m_aFace3DPointGlob[i].Z));
                }
            }
            else
            {
                double dConvFactorX = ImageSize.Width / ViewSize.Width;
                double dConvFactorY = ImageSize.Height / ViewSize.Height;
                for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                {
                    oLabelInfo.aPolyPointX.Add((float)(ViewFinderPolygon.Points[i].X * dConvFactorX));
                    oLabelInfo.aPolyPointY.Add((float)(ViewFinderPolygon.Points[i].Y * dConvFactorY));
                }
            }
            // Aggiungo LabelInfo a LabelManager
            oLabel.Add(oLabelInfo);

            return oLabel;

        }
        public CSingleFileLabel BuildSavingLabelCandidate(string sNewJsonFileName, Size ViewSize, double dTheta, double dPhi, double dVFov, double dHFov, Vector3D vLookDirection,bool bMlb=false)
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
            CSingleFileLabel.SLabelInfo oLabelInfo = new CSingleFileLabel.SLabelInfo(bMlb);

            // Aggiungo nome file .png
            //            oLabelInfo.sImageFileName = sNewFileName;
            oLabelInfo.sLabelName = ElementName.Text;
            if (CProjectPlane.m_bPlaneDefined)
                oLabelInfo.sParentElementName = CProjectPlane.m_sWallName;
            else
                oLabelInfo.sParentElementName = "";

            /*
            // Aggiungo la category
            List <List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();
            int iSelectedCat = CategoryCombo.SelectedIndex + 1; // Ho escluso la categoria 0 --> devo aggiungere 1 agli indici
            int iSelectedItem = CategoryCombo.SelectedIndex;

            // Aggiungo il catalogID
            oLabelInfo.iObjCatalogID = oLList[iSelectedCat][iSelectedItem].nId;
            oLabelInfo.iCategory = SharingHelper.GetCatalogManager().SearchCategoryByID(oLabelInfo.iObjCatalogID);
            */

            // Aggiungo la category
            string sCategorySelected = CategoryCombo.SelectedValue.ToString();
            oLabelInfo.iCategory = SharingHelper.GetCatalogManager().GetCategoryDictionary()[sCategorySelected];

            // Aggiungo il catalogID
            oLabelInfo.iObjCatalogID = CUIManager.m_aObjId[ItemCombo.SelectedIndex];

            // Aggiungo i punti
            oLabelInfo.aPolyPointX = new List<double>();
            oLabelInfo.aPolyPointY = new List<double>();

            if (CProjectPlane.m_bPlaneDefined)
            {
                if (ViewFinderPolygon.Points == null)
                    return null;

                oLabelInfo.aPolyPointZ = new List<double>();
                for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                {
                    oLabelInfo.aPolyPointX.Add((float)(CProjectPlane.m_aFace3DPointGlob[i].X));
                    oLabelInfo.aPolyPointY.Add((float)(CProjectPlane.m_aFace3DPointGlob[i].Y));
                    oLabelInfo.aPolyPointZ.Add((float)(CProjectPlane.m_aFace3DPointGlob[i].Z));
                }
            }
            else
            {
                for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                {
                    oLabelInfo.aPolyPointX.Add((float)(ViewFinderPolygon.Points[i].X * SharingHelper.m_dConvFactor));
                    oLabelInfo.aPolyPointY.Add((float)(ViewFinderPolygon.Points[i].Y * SharingHelper.m_dConvFactor));
                }
            }


            List<MyVector3D> avPoint;
            int iThetaMin = 0;
            int iThetaMax = 0;
            CUIManager.ComputeVFVersor(out avPoint, ref iThetaMin, ref iThetaMax);

            List<MyVector3D> avFrustPoint;
            int iFrustThetaMin = 0;
            int iFrustThetaMax = 0;
            CUIManager.ComputeFrustumVersor(out avFrustPoint, ref iFrustThetaMin, ref iFrustThetaMax);

            // Aggiungo info vettoriali su frustum e VF
            oLabelInfo.oVFInfo = new CVFInfo(avPoint, iThetaMin, iThetaMax);
            oLabel.m_oFrustumInfo = new CFrusInfo(avFrustPoint, iFrustThetaMin, iFrustThetaMax);


            // Aggiungo LabelInfo a LabelManager
            oLabel.Add(oLabelInfo);

            return oLabel;

        }
        public void SaveJason(CSingleFileLabel oLabel, string sNewJsonFileName, ref int iPhotoIndex, ref int iLabelIndex)
        {

            // Salvo file .json
            string sJpegFileName = System.IO.Path.GetFileName(SharingHelper.GetFullJpegFileName());
            oLabel.SaveToJsonFile(sNewJsonFileName, sJpegFileName);

            // Aggiorno CLabelManager
            CLabelManager.AddLabel(oLabel, ref iPhotoIndex, ref iLabelIndex);
        }
        public void SaveMlb(CSingleFileLabel oLabel, string sNewJsonFileName, ref int iPhotoIndex, ref int iLabelIndex)
        {

            // Salvo file .json
            string sJpegFileName = System.IO.Path.GetFileName(SharingHelper.GetFullJpegFileName());
            oLabel.SaveToMlbFile(sNewJsonFileName, sJpegFileName);

            // Aggiorno CLabelManager
            CLabelManager.AddLabel(oLabel, ref iPhotoIndex, ref iLabelIndex);
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
            if(m_Window.CreateMode.IsChecked==false)
                m_Window.CreateMode.IsChecked = true;
            else
                m_Window.CreateMode.IsChecked = false;

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

            // Elimino la label dalla lista 
            //            CLabelManager.RemoveLabel(oCurrentLabel);
            CLabelManager.ReloadLabelSet();

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
            if(CUIManager.m_aViewerEditable[ItemCombo.SelectedIndex])
                viewer360_View.SaveMlb();
            else
                viewer360_View.SaveImageAndJson();

            /*
            if(CUIManager.GetCurrentCategory()==4 || CUIManager.GetCurrentCategory() == 5)
                viewer360_View.SaveMlb();
            else
                viewer360_View.SaveImageAndJson();
            */
        }
        private void LaunchAI_Click(object sender, RoutedEventArgs e)
        { 
        }

        private void Project2Plane_Click(object sender, RoutedEventArgs e)
        {
            CUIManager.Project2Plane_Click();
        }
    }
}
