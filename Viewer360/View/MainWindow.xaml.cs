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

//++++++++++++++++++++++  // TODO  eliminare
            m_iEllipseIncrementalNum = 0;
            myCanvas = new Canvas();
            Grid.SetRow(myCanvas, 1);
            Grid.SetColumnSpan(myCanvas, 5);
            myGrid.Children.Add(myCanvas);

            m_EllipseList = new List<Ellipse>();
            m_aOriginalPolygon=new Point[ViewFinderPolygon.Points.Count];

            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                m_aOriginalPolygon[i].X = ViewFinderPolygon.Points[i].X;
                m_aOriginalPolygon[i].Y = ViewFinderPolygon.Points[i].Y;
                AddEllipse(myCanvas, ViewFinderPolygon.Points[i].X, ViewFinderPolygon.Points[i].Y, i);
            }
//++++++++++++++++++++++  

            // Inizializzo centro mirino
            vViewfinderCentre.X = (ViewFinderPolygon.Points[0].X + ViewFinderPolygon.Points[1].X) / 2;
            vViewfinderCentre.Y = (ViewFinderPolygon.Points[1].Y + ViewFinderPolygon.Points[2].Y) / 2;
            vVewfinderBBox.X= (ViewFinderPolygon.Points[1].X - ViewFinderPolygon.Points[0].X) / 2;
            vVewfinderBBox.Y = (ViewFinderPolygon.Points[2].Y - ViewFinderPolygon.Points[1].Y) / 2;

            aPointTmp =new PointCollection();
            aItemDefaultEntry=new List<int>();


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

        int FindEllipseIndex(string sEllipseName)
        {


            for(int i=0; i< m_EllipseList.Count; i++ ) 
            {
                if (m_EllipseList[i].Name == sEllipseName)
                    return i;
            }

            return -1;
        }

        private void AddEllipse(Canvas canvas, double left, double top, int iPointIndex=-1)
        {

            Ellipse ellipse = new Ellipse();
            ellipse.Width = 8;
            ellipse.Height = 8;
            ellipse.Fill = Brushes.Green;
            ellipse.Name = "P" + m_iEllipseIncrementalNum.ToString();
            m_iEllipseIncrementalNum++;

            ellipse.MouseLeftButtonDown += Cerchio_Click;
            ellipse.MouseLeftButtonUp += Cerchio_BottonUp;
            ellipse.MouseRightButtonDown += DeleteCerchio_Click;

            Canvas.SetLeft(ellipse, left-4);
            Canvas.SetTop(ellipse, top-4);

            canvas.Children.Add(ellipse);

            if(iPointIndex<0) // Append
                m_EllipseList.Add(ellipse);
            else
                m_EllipseList.Insert(iPointIndex, ellipse);
            /*
            SPointInfo sPointInfo=new SPointInfo();
            sPointInfo.ellipse= ellipse;
            sPointInfo.iPointIndex= iPointIndex;
            m_aPointinfo.Add(sPointInfo);
            */
        }


        private void Cerchio_BottonUp(object sender, MouseButtonEventArgs e)
        {
            //++++++++++++++++++++++
            // Console.WriteLine("Cerchio_BottonUp");
            //++++++++++++++++++++++
            if(CUIManager.GetMode()==ViewerMode.Edit)
                m_Window.ViewFinderPolygon.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 0, 0));
            else
                m_Window.ViewFinderPolygon.Fill = null;

            Ellipse cerchioCliccato = sender as Ellipse;

            // Esegui qui le azioni desiderate in risposta al clic sul cerchio
            if (cerchioCliccato != null)
            {
                cerchioCliccato.Fill = Brushes.Green;
            }
            iDraggingPoint = -1;
        }
            

        private void Cerchio_Click(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                    return;
            //++++++++++++++++++++++
            //Console.WriteLine("Cerchio_Click");
            //++++++++++++++++++++++
            m_Window.ViewFinderPolygon.Fill = null;


            // Ottieni il cerchio su cui è stato effettuato il clic
            Ellipse cerchioCliccato = sender as Ellipse;

            // Esegui qui le azioni desiderate in risposta al clic sul cerchio
            if (cerchioCliccato != null)
            {
                cerchioCliccato.Fill = Brushes.Red;
                iDraggingPoint = FindEllipseIndex(cerchioCliccato.Name);
            }
        }

       

        private void DeleteCerchio_Click(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                return;

            if (m_EllipseList.Count == 3)  // Se sono già a 3 vertici smetto di eliminare punti
                return;

            // Ottieni il cerchio su cui è stato effettuato il clic
            Ellipse cerchioCliccato = sender as Ellipse;

            DeleteEllipse(cerchioCliccato);
        }

        public void DeleteAllEllipse()
        {
            foreach (var eEllipse in m_EllipseList)
            {
                myCanvas.Children.Remove(eEllipse);
            }
            m_EllipseList.Clear();
        }

        public void DeleteEllipse(Ellipse eEllipse)
        {
            // Esegui qui le azioni desiderate in risposta al clic sul cerchio
            if (eEllipse != null)
            {
                int iIndex = FindEllipseIndex(eEllipse.Name);

                if (iIndex >= 0)
                {
                    // rimuovo il cerchio dal canvas
                    myCanvas.Children.Remove(eEllipse);

                    // Rimuovo il cerclio dalla lista
                    m_EllipseList.Remove(eEllipse);

                    // Rimuovo il punto dall'elenco
                    ViewFinderPolygon.Points.RemoveAt(iIndex);
                }
            }
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
            //++++++++++++++++++++++++++++++++
            /// CProjectPlane.SetPlane(80.62, 12, 0, -1);  // AM

            /*
            double dVx = 0;
            double dVy = -1;
            double dVz = 0;
            Vector3D vAt= new Vector3D(dVx,dVy,dVz);
            vAt.Normalize();

            viewer360_View.MyCam.LookDirection = vAt;
            */
            //+++++++++++++++++++++++++++++++++


            Point3D[] aPoint = new Point3D[4];
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
//++++++++++++++++++++++++++
/*
                Point3D pointNear, pointFar;
                bool ok = Viewport3DHelper.Point2DtoPoint3D(viewer360_View.vp, ViewFinderPolygon.Points[i], out pointNear, out pointFar);
                Point3D pointNear1, pointFar1;
                bool ok1 = CProjectPlane.Point2DtoPoint3D(viewer360_View.vp, ViewFinderPolygon.Points[i], out pointNear1, out pointFar1);
*/
//++++++++++++++++++++++++++
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

        public void UpdateViewPolygonFromFace3D()
        {
            if (ViewFinderPolygon.Points == null)
                ViewFinderPolygon.Points = new PointCollection { new Point(0, 0), new Point(0, 0), new Point(0, 0), new Point(0, 0) };

            for (int i = 0; i <4; i++)
            {
                double dDist = CProjectPlane.CameraDist(CProjectPlane.m_aFace3DPoint[i], viewer360_View.MyCam);
                if(dDist > 0)  // Dietro la telecamera
                {
                    ViewFinderPolygon.Points=null;
                    return;
                }
                Point3D pTmp=new Point3D(CProjectPlane.m_aFace3DPoint[i].X, CProjectPlane.m_aFace3DPoint[i].Y, -CProjectPlane.m_aFace3DPoint[i].Z);
                ViewFinderPolygon.Points[i] = Viewport3DHelper.Point3DtoPoint2D(viewer360_View.vp, pTmp);
            }
        }

        public void Polygon_LeftCtrlMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point vPoint = new Point(Mouse.GetPosition(viewer360_View.vp).X, Mouse.GetPosition(viewer360_View.vp).Y);
                //+++++++++++++++++++++++++
                //Console.WriteLine("vPoint=" + vPoint.ToString());
                //++++++++++++++++++++++
                // Verifico se ho cliccato su un segmento
                int iSegment = CheckSegmentClick(vPoint);

                if (iSegment >= 0)
                {
                    CreateNewPoint(iSegment, vPoint);
                }
                else
                {
                    offset = vPoint;
                    isDragging = true;
                }
            }
        }


        void CreateNewPoint(int iSegment, Point vPoint)
        {
            if (iSegment < ViewFinderPolygon.Points.Count - 1)
            {
                ViewFinderPolygon.Points.Insert(iSegment + 1, vPoint);
                AddEllipse(myCanvas, vPoint.X, vPoint.Y, iSegment + 1);
            }
            else
            {
                ViewFinderPolygon.Points.Add(vPoint);
                AddEllipse(myCanvas, vPoint.X, vPoint.Y);
            }
        }


        public void ResetPolygon()
        {

            DeleteAllEllipse();

            PointCollection NewPoints = new PointCollection();
            for (int i = 0; i < m_aOriginalPolygon.Length; i++)
            {
                Point p = new Point(m_aOriginalPolygon[i].X, m_aOriginalPolygon[i].Y);
                AddEllipse(myCanvas, p.X, p.Y, i);
                NewPoints.Add(p);
            }

            ViewFinderPolygon.Points = NewPoints;

        }
        public void RestorePolygon(CSingleFileLabel oLabel)
        {
            DeleteAllEllipse();

            PointCollection NewPoints = new PointCollection();
            double dScaleX;
            double dScaleY;
            if (viewer360_View.GetProjection() == Viewer360View.ViewerProjection.Spheric)
            {
                dScaleX = 1 / SharingHelper.m_dConvFactor;
                dScaleY = 1 / SharingHelper.m_dConvFactor;
            }
            else
            {
                dScaleX = viewer360_View.RenderSize.Width / oLabel.m_ImageWidth;
                dScaleY = viewer360_View.RenderSize.Height / oLabel.m_ImageHeight;
            }

            for (int i= 0; i < oLabel.m_aLabelInfo[0].aPolyPointX.Count; i++) 
            {
                Point p = new Point(oLabel.m_aLabelInfo[0].aPolyPointX[i]*dScaleX, oLabel.m_aLabelInfo[0].aPolyPointY[i] * dScaleY);
                AddEllipse(myCanvas, p.X, p.Y, i);
                NewPoints.Add(p);
            }

            ViewFinderPolygon.Points = NewPoints;
        }


        double Length(Point v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y);
        }
        int CheckSegmentClick(Point vPoint)
        {
            int iPixelTol = 4;

            Point vSide;

            double fMinDist2 = 1e+30;
            int iCandidate = -1;
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                Point v1 = new Point(vPoint.X - ViewFinderPolygon.Points[i].X, vPoint.Y - ViewFinderPolygon.Points[i].Y);
                double d1Len = Length(v1);

                if (i < ViewFinderPolygon.Points.Count-1)
                    vSide = new Point(ViewFinderPolygon.Points[i + 1].X - ViewFinderPolygon.Points[i].X, ViewFinderPolygon.Points[i + 1].Y - ViewFinderPolygon.Points[i].Y);
                else
                    vSide = new Point(ViewFinderPolygon.Points[0].X - ViewFinderPolygon.Points[ViewFinderPolygon.Points.Count - 1].X, ViewFinderPolygon.Points[0].Y - ViewFinderPolygon.Points[ViewFinderPolygon.Points.Count - 1].Y);

                if (Length(vSide) < 1e-8)
                    continue;

                double dVSideLen = Length(vSide);
                vSide.X /= dVSideLen;
                vSide.Y /= dVSideLen;

                double fProjection = v1.X * vSide.X+ v1.Y * vSide.Y;
                if (fProjection < 0 || fProjection > dVSideLen)  // Il punto vCenter non cade sul lato corrente
                    continue;

                double fDist2 = d1Len * d1Len - fProjection * fProjection;
                if (fDist2 <= iPixelTol * iPixelTol && fDist2 < fMinDist2)
                {
                    fMinDist2 = fDist2;
                    iCandidate = i;
                }
            }


            return iCandidate;
        }

        static double Dist(Point punto1, Point punto2)
        {
            double deltaX = punto2.X - punto1.X;
            double deltaY = punto2.Y - punto1.Y;

            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }


        public void Polygon_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && (isDragging || iDraggingPoint>=0))
            {
                // Recupero posizione mouse
                Point newPosition = new Point(Mouse.GetPosition(viewer360_View.vp).X, Mouse.GetPosition(viewer360_View.vp).Y);

                if (iDraggingPoint >= 0)  // Sto modificando la posizione di un vertice
                {
                    ViewFinderPolygon.Points[iDraggingPoint] = newPosition;
                    //++++++++++++++++++++++++++++++++++++++++++
                    /*
                    int nSizeCorrected = (int)(viewer360_View.vp.RenderSize.Width * viewer360_View.Image.Height / viewer360_View.Image.Width);
                    int nOffset = (int)(viewer360_View.vp.RenderSize.Height - nSizeCorrected) / 2;
                    int nMaxY = nSizeCorrected- nOffset;
                    Console.WriteLine("Pos=" + newPosition.ToString() + "   WinSize=" + m_Window.Width.ToString() + "," + m_Window.Height.ToString() + " " + m_Window.Width / m_Window.Height
                        + "   vp.Size=" + viewer360_View.vp.RenderSize.Width.ToString() + "," + viewer360_View.vp.RenderSize.Height.ToString() 
                        + " vp.SizeY corretto="+ nSizeCorrected + " maxY="+ nMaxY + "  nOffset="+ nOffset);
                    */
                    //++++++++++++++++++++++++++++++++++++++++++

                    // Aggiorno le posizioni dei pallini
                    UpdateVertexCircle();
                }
                else
                {

                    // Calcolo nuove posizioni
                    aPointTmp.Clear();
                    double dX, dY;
                    for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                    {
                        dX = ViewFinderPolygon.Points[i].X + newPosition.X - offset.X;
                        dY = ViewFinderPolygon.Points[i].Y + newPosition.Y - offset.Y;
                        aPointTmp.Add(new Point(dX, dY));
                    }

                    // Recupero dimensioni finestra
                    double dSizeX = viewer360_View.m_ViewSize.Width;
                    double dSizeY = viewer360_View.m_ViewSize.Height;

                    // Se tutti i 4 vertici sono interni alla view aggiorno il mirino
                    if (ArePointInsideView(aPointTmp, dSizeX, dSizeY)) // Se tutti i 4 vertici sono interni alla view aggiorno il mirino
                    {
                        // Aggiorno i vertici
                        for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                            ViewFinderPolygon.Points[i] = aPointTmp[i];

                        // Aggiorno centro mirino
                        vViewfinderCentre.X = (ViewFinderPolygon.Points[0].X + ViewFinderPolygon.Points[1].X) / 2;
                        vViewfinderCentre.Y = (ViewFinderPolygon.Points[1].Y + ViewFinderPolygon.Points[2].Y) / 2;

                        // Aggiorno le posizioni dei pallini
                        UpdateVertexCircle();
                    }

                    offset = newPosition;
                }
            }
        }

        public void Polygon_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ( !isDragging)
            {
                // Recupero dimensioni finestra
                double dSizeX = viewer360_View.m_ViewSize.Width;
                double dSizeY = viewer360_View.m_ViewSize.Height;

                // Calcolo nuove posizioni
                aPointTmp.Clear();

                Point vP = new Point();
                if (Keyboard.IsKeyDown(Key.A))  // Stretch Verticale
                {
                    float fIncrease = (float)(e.Delta) / 25;

                    for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                    {
                        vP.X = ViewFinderPolygon.Points[i].X;

                        if(i<2)
                            vP.Y = ViewFinderPolygon.Points[i].Y - fIncrease;
                        else
                            vP.Y = ViewFinderPolygon.Points[i].Y + fIncrease;

                        aPointTmp.Add(vP);
                    }

                    // Verifico di non aver stratcato troppo
                    if (aPointTmp[0].Y > aPointTmp[3].Y - 5)  // minimo 5 pixel
                    {
                        vP.X = -1;
                        aPointTmp[0] = vP;
                    }
                }
                else if (Keyboard.IsKeyDown(Key.S)) // Stretch Orizzontale
                {
                    float fIncrease = (float)(e.Delta) / 25;
                    for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                    {
                        vP.Y = ViewFinderPolygon.Points[i].Y;
                        if(i==0 || i==3)
                            vP.X = ViewFinderPolygon.Points[i].X - fIncrease;
                        else
                            vP.X = ViewFinderPolygon.Points[i].X + fIncrease;

                        aPointTmp.Add(vP);
                    }

                    // Verifico di non aver stratcato troppo
                    if (aPointTmp[0].X > aPointTmp[1].X - 10)   // minimo 10 pixel
                    {
                        vP.X = -1;
                        aPointTmp[0] = vP;
                    }
                }
                else  // Resize 
                {
                    float fIncrease = 1 + (float)(e.Delta) / 1500;
                    for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                    {
                        vP = vViewfinderCentre + (ViewFinderPolygon.Points[i] - vViewfinderCentre) * fIncrease;
                        aPointTmp.Add(vP);
                    }

                    if (aPointTmp[0].X > aPointTmp[1].X - 10 || aPointTmp[0].Y > aPointTmp[3].Y - 10)   // minimo 10 pixel
                    {
                        vP.X = -1;
                        aPointTmp[0] = vP;
                    }
                    if(CUIManager.GetMode()== ViewerMode.Edit && CProjectPlane.m_bPlaneDefined)  // TODO aggiungere check opportuni
                        UpdateViewPolygonFromFace3D();
                }

                if(ArePointInsideView(aPointTmp,dSizeX,dSizeY)) // Se tutti i 4 vertici sono interni alla view aggiorno il mirino
                { 
                    // Aggiorno i vertici
                    for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                        ViewFinderPolygon.Points[i] = aPointTmp[i];

                    // Aggiorno centro mirino
                    vViewfinderCentre.X = (ViewFinderPolygon.Points[0].X + ViewFinderPolygon.Points[1].X) / 2;
                    vViewfinderCentre.Y = (ViewFinderPolygon.Points[1].Y + ViewFinderPolygon.Points[2].Y) / 2;

                    // Aggiorno le posizioni dei pallini
                    UpdateVertexCircle();

                }

            }
        }

        bool ArePointInsideView(PointCollection aPointTmp, double dSizeX, double dSizeY)
        {
            // Se tutti i 4 vertici sono interni alla view aggiorno il mirino

            foreach(var point in aPointTmp)
            {
                if (point.X < 0 || point.X > dSizeX || point.Y < 0 || point.Y > dSizeY)
                    return false;
            }

            return true;
        }


        void UpdateVertexCircle()
        {
            for(int i = 0; i< m_EllipseList.Count; i++)
            {
                Point point = ViewFinderPolygon.Points[i];
                m_EllipseList[i].SetValue(Canvas.LeftProperty, point.X - 4);
                m_EllipseList[i].SetValue(Canvas.TopProperty, point.Y - 4);

            }

        }


        public void RescaleViewfinderOnWindowChange(SizeChangedInfo sizeInfo)
        {
            // Calcolo fattori di scala
            if (sizeInfo.PreviousSize.Width + sizeInfo.PreviousSize.Height < 1)
                return;

            double dScaleX = sizeInfo.NewSize.Width / sizeInfo.PreviousSize.Width;
            double dScaleY = sizeInfo.NewSize.Height / sizeInfo.PreviousSize.Height;

            // Calcolo nuova posizione centro
            Point vNewCentre = new Point(vViewfinderCentre.X * dScaleX, vViewfinderCentre.Y * dScaleY);

            // Aggiorno coordinate vertici e centro mirino
            Point vNewDelta;
            Point vP;
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                vNewDelta = new Point((ViewFinderPolygon.Points[i].X - vViewfinderCentre.X)*dScaleX, (ViewFinderPolygon.Points[i].Y - vViewfinderCentre.Y)*dScaleY);
                vP = new Point(vNewCentre.X + vNewDelta.X, vNewCentre.Y + vNewDelta.Y);
                ViewFinderPolygon.Points[i] = vP;
            }

            vViewfinderCentre = vNewCentre;
            UpdateVertexCircle();
            
        }

        public void Polygon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            iDraggingPoint = -1;
        }

        private void viewer360_View_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
