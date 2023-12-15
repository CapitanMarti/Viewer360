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

namespace Viewer360.View
{
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

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            iDraggingPoint = -1;

            // Associare i gestori di eventi per il clic su ciascun cerchio
            Point1.MouseLeftButtonDown += Cerchio_Click;
            Point2.MouseLeftButtonDown += Cerchio_Click;
            Point3.MouseLeftButtonDown += Cerchio_Click;
            Point4.MouseLeftButtonDown += Cerchio_Click;

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

        private void Cerchio_Click(object sender, MouseButtonEventArgs e)
        {
            // Ottieni il cerchio su cui è stato effettuato il clic
            Ellipse cerchioCliccato = sender as Ellipse;

            // Esegui qui le azioni desiderate in risposta al clic sul cerchio
            if (cerchioCliccato != null)
            {
                if(cerchioCliccato.Name=="Point1")
                    iDraggingPoint = 0;
                else if (cerchioCliccato.Name == "Point2")
                    iDraggingPoint = 1;
                else if (cerchioCliccato.Name == "Point3")
                    iDraggingPoint = 2;
                else if (cerchioCliccato.Name == "Point4")
                    iDraggingPoint = 3;

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

        public void SaveJason(string sNewFileName, Size ViewSize)
        {
            // Creo file manager per file attuale
            CFileLabelManager oLabelManager= new CFileLabelManager();
            oLabelManager.SetImageSize(Convert.ToInt32(ViewSize.Width*1.5), Convert.ToInt32(ViewSize.Height*1.5));

            // Creo e inizializzo LabelInfo
            CFileLabelManager.SLabelInfo oLabelInfo=new CFileLabelManager.SLabelInfo();

            oLabelInfo.sImageFileName = sNewFileName;
            oLabelInfo.sLabelName = oElementName.Text;

            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();
            int iSelectedCat = oCategoryCombo.SelectedIndex + 1; // Ho escluso la categoria 0 --> devo aggiungere 1 agli indici
            int iSelectedItem = oItemCombo.SelectedIndex;

            oLabelInfo.iObjCatalogID = oLList[iSelectedCat][iSelectedItem].nId;

            oLabelInfo.aPolyPointX = new List<float>();
            oLabelInfo.aPolyPointY = new List<float>();
            for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
            {
                oLabelInfo.aPolyPointX.Add((float)(ViewFinderPolygon.Points[i].X*1.5));
                oLabelInfo.aPolyPointY.Add((float)(ViewFinderPolygon.Points[i].Y*1.5));
            }

            // Aggiungo LabelInfo a LabelManager
            oLabelManager.Add(oLabelInfo);

            // Salvo file .json
            oLabelManager.SaveToJsonFile(sNewFileName);
        }

        public void Polygon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                offset = new Point(Mouse.GetPosition(viewer360_View.vp).X, Mouse.GetPosition(viewer360_View.vp).Y);

                /*
                // Verifico se ho cliccato su un vertice
                for (int i = 0; i < 4; i++)
                {
                    if (Dist(ViewFinderPolygon.Points[i], offset) < 4)
                    {
                        iDraggingPoint = i;
                        return;
                    }
                }
                iDraggingPoint = -1;
*/
                isDragging = true;
            }
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
                else if (Keyboard.IsKeyDown(Key.Z)) // Deform. Orizzontale
                {
                    float fIncrease = (float)(e.Delta) / 25;
                    for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                    {
                        vP.Y = ViewFinderPolygon.Points[i].Y;
//                        if (i < 2)
                        {
                            double dNewSize = (ViewFinderPolygon.Points[i].X - vViewfinderCentre.X) * fIncrease;
                            if (i == 0 || i == 2)
                                vP.X = ViewFinderPolygon.Points[i].X + fIncrease;
                            else
                                vP.X = ViewFinderPolygon.Points[i].X - fIncrease;
                        }
/*
                        else
                        {
                            double dNewSize = (ViewFinderPolygon.Points[i].X - vViewfinderCentre.X) * fIncrease;
                            vP.X = vViewfinderCentre.X - (ViewFinderPolygon.Points[i].X - vViewfinderCentre.X) * fIncrease;
                        }
*/
                        aPointTmp.Add(vP);
                    }
                }
                else
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
            Point1.SetValue(Canvas.LeftProperty, ViewFinderPolygon.Points[0].X-4);
            Point1.SetValue(Canvas.TopProperty, ViewFinderPolygon.Points[0].Y - 4);
            Point2.SetValue(Canvas.LeftProperty, ViewFinderPolygon.Points[1].X - 4);
            Point2.SetValue(Canvas.TopProperty, ViewFinderPolygon.Points[1].Y - 4);
            Point3.SetValue(Canvas.LeftProperty, ViewFinderPolygon.Points[2].X - 4);
            Point3.SetValue(Canvas.TopProperty, ViewFinderPolygon.Points[2].Y - 4);
            Point4.SetValue(Canvas.LeftProperty, ViewFinderPolygon.Points[3].X - 4);
            Point4.SetValue(Canvas.TopProperty, ViewFinderPolygon.Points[3] .Y - 4);
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
            
        }

        public void Polygon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            iDraggingPoint = -1;
        }

    }
}
