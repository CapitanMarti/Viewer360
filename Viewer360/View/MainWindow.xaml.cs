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

namespace Viewer360.View
{
    struct SPointInfo
    {
        public int iPointIndex;
        public Ellipse ellipse;
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private bool isDragging;
        private int  iDraggingPoint;
        private Point offset;
        private Point vViewfinderCentre;
        private Point vVewfinderBBox;
        private PointCollection aPointTmp;
        private List<int> aItemDefaultEntry;

        List<Ellipse> m_EllipseList;
        int m_iEllipseIncrementalNum;
        Canvas myCanvas;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            iDraggingPoint = -1;

            m_iEllipseIncrementalNum = 0;
            myCanvas = new Canvas();
            Grid.SetRow(myCanvas, 1);
            Grid.SetColumnSpan(myCanvas, 5);
            myGrid.Children.Add(myCanvas);

            m_EllipseList=new List<Ellipse>();

            for (int i=0; i< ViewFinderPolygon.Points.Count; i++)
                AddEllipse(myCanvas, ViewFinderPolygon.Points[i].X, ViewFinderPolygon.Points[i].Y,i);


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
                Window.WindowStyleProperty,typeof(Window));
            d.AddValueChanged(this, (sender, args) =>
            {
                Window w = (Window)sender;
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

            // Esegui qui le azioni desiderate in risposta al clic sul cerchio
            if (cerchioCliccato != null)
            {
                int iIndex= FindEllipseIndex(cerchioCliccato.Name);

                if (iIndex >= 0)
                {
                    // rimuovo il cerchio dal canvas
                    myCanvas.Children.Remove(cerchioCliccato);

                    // Rimuovo il cerclio dalla lista
                    m_EllipseList.Remove(cerchioCliccato);

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
                oCategoryCombo.Items.Add(oLList[iCat][0].sCategory);

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

            oCategoryCombo.SelectedIndex = 2;  // Wall

        }

        private void CategorySelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();

            oItemCombo.Items.Clear();
            int iSelectedCat = oCategoryCombo.SelectedIndex+1; // Ho escluso la categoria 0 

            for (int iItem = 0; iItem < oLList[iSelectedCat].Count; iItem++)
                oItemCombo.Items.Add(oLList[iSelectedCat][iItem].sUI_CategoryInfo);

            oItemCombo.SelectedIndex = aItemDefaultEntry[oCategoryCombo.SelectedIndex];
            oElementName.Text = oCategoryCombo.SelectedItem.ToString();

        }
        private void ItemSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        public void SaveJason(string sNewJpgFileName, Size ViewSize, double dTheta, double dPhi, double dVFov, double dHFov,Vector3D vLookDirection)
        {
            // Creo file manager per file attuale
            CSingleFileLabel oLabelManager = new CSingleFileLabel();
            oLabelManager.m_sJsonAthor= "ScanToBim-Viewer360";
            oLabelManager.SetImageSize(Convert.ToInt32(ViewSize.Height*1.5), Convert.ToInt32(ViewSize.Width * 1.5));  // INVERTO PER COMPATIBILITA' CON SCISSOR!!!!
//            oLabelManager.SetImageSize(Convert.ToInt32(ViewSize.Width * 1.5), Convert.ToInt32(ViewSize.Height * 1.5));
            oLabelManager.m_dTheta = dTheta;
            oLabelManager.m_dPhi = dPhi;
            oLabelManager.m_vLookDirectionX = vLookDirection.X;
            oLabelManager.m_vLookDirectionY = vLookDirection.Y;
            oLabelManager.m_vLookDirectionZ = vLookDirection.Z;
            oLabelManager.m_hFov = dHFov;
            oLabelManager.m_vFov= dHFov;

            // Creo e inizializzo LabelInfo
            CSingleFileLabel.SLabelInfo oLabelInfo=new CSingleFileLabel.SLabelInfo();

            // Aggiungo nome file .png
//            oLabelInfo.sImageFileName = sNewFileName;
            oLabelInfo.sLabelName = oElementName.Text;

            // Aggiungo la category
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();
            int iSelectedCat = oCategoryCombo.SelectedIndex + 1; // Ho escluso la categoria 0 --> devo aggiungere 1 agli indici
            int iSelectedItem = oItemCombo.SelectedIndex;

            // Aggiungo il catalogID
            oLabelInfo.iObjCatalogID = oLList[iSelectedCat][iSelectedItem].nId;

            // Aggiungo i punti
            oLabelInfo.aPolyPointX = new List<double>();
            oLabelInfo.aPolyPointY = new List<double>();
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                oLabelInfo.aPolyPointX.Add((float)(ViewFinderPolygon.Points[i].X*1.5));
                oLabelInfo.aPolyPointY.Add((float)(ViewFinderPolygon.Points[i].Y*1.5));
            }

            // Aggiungo le info sulla camera 3D usata per scattare la foto

            // Aggiungo LabelInfo a LabelManager
            oLabelManager.Add(oLabelInfo);

            // Salvo file .json
            oLabelManager.SaveToJsonFile(sNewJpgFileName);

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

        }

        private void PrevLabel_Click(object sender, RoutedEventArgs e)
        {

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


        public void Polygon_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && (isDragging || iDraggingPoint>=0))
            {
                // Recupero posizione mouse
                Point newPosition = new Point(Mouse.GetPosition(viewer360_View.vp).X, Mouse.GetPosition(viewer360_View.vp).Y);

                if (iDraggingPoint >= 0)  // Sto modificando la posizione di un vertice
                {
                    ViewFinderPolygon.Points[iDraggingPoint] = newPosition;

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
                    if (aPointTmp[0].X >= 0 && aPointTmp[1].X <= dSizeX && aPointTmp[0].Y >= 0 && aPointTmp[2].Y <= dSizeY)
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

                }

                // Se tutti i 4 vertici sono interni alla view aggiorno il mirino
                if (aPointTmp[0].X >= 0 && aPointTmp[1].X <= dSizeX && aPointTmp[0].Y >= 0 && aPointTmp[2].Y <= dSizeY)
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
