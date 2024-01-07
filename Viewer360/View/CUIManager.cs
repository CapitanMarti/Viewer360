using HelixToolkit.Wpf;
using PointCloudUtility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Printing;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Viewer360.View
{
    static public class CUIManager
    {
        // ************* Sezione inerente la UI esterna
        static public View.MainWindow m_Window;
        static ViewerMode m_eMode= ViewerMode.Undefined;
        static public bool m_bDebugMode = false;
        static int m_iCategory=-1;

        public enum ViewerMode
        {
            Undefined=-1,
            Create,
            Edit
        }


        // ************* Sezione inerente la UI interna (poligono, ecc)
        static System.Windows.Shapes.Polygon m_oVFinder;
        static private Point[] m_aOriginalPolygon;

        static List<Ellipse> m_EllipseList;
        static int m_iEllipseIncrementalNum;
        static Canvas myCanvas;
        static private PointCollection m_aPointTmp;
        static private Point vViewfinderCentre;
        static private Point vVewfinderBBox;
        static private bool isDragging;
        static private int iDraggingPoint;
        static private Point offset;


        static public void SetCurrentCategory(int iCategory)
        {
            m_iCategory = iCategory;  // 1==Wall,2==Floor,3==Ceiling,4==Window,5==Door,6==PCSection
        }
        static public int GetCurrentCategory()
        {
            return m_iCategory; // 1==Wall,2==Floor,3==Ceiling,4==Window,5==Door,6==PCSection
        }

        static public void Polygon_LeftCtrlMouseDown(MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point vPoint = new Point(Mouse.GetPosition(m_Window.viewer360_View.vp).X, Mouse.GetPosition(m_Window.viewer360_View.vp).Y);

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
        static public void Polygon_MouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && (isDragging || iDraggingPoint >= 0))
            {
                // Recupero posizione mouse
                Point newPosition = new Point(Mouse.GetPosition(m_Window.viewer360_View.vp).X, Mouse.GetPosition(m_Window.viewer360_View.vp).Y);

                if (iDraggingPoint >= 0)  // Sto modificando la posizione di un vertice
                {
                    m_oVFinder.Points[iDraggingPoint] = newPosition;
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
                    m_aPointTmp.Clear();
                    double dX, dY;
                    for (int i = 0; i < m_oVFinder.Points.Count; i++)
                    {
                        dX = m_oVFinder.Points[i].X + newPosition.X - offset.X;
                        dY = m_oVFinder.Points[i].Y + newPosition.Y - offset.Y;
                        m_aPointTmp.Add(new Point(dX, dY));
                    }

                    // Recupero dimensioni finestra
                    double dSizeX = m_Window.viewer360_View.m_ViewSize.Width;
                    double dSizeY = m_Window.viewer360_View.m_ViewSize.Height;

                    // Se tutti i 4 vertici sono interni alla view aggiorno il mirino
                    if (ArePointInsideView(m_aPointTmp, dSizeX, dSizeY)) // Se tutti i 4 vertici sono interni alla view aggiorno il mirino
                    {
                        // Aggiorno i vertici
                        for (int i = 0; i < m_oVFinder.Points.Count; i++)
                            m_oVFinder.Points[i] = m_aPointTmp[i];

                        // Aggiorno centro mirino
                        vViewfinderCentre.X = (m_oVFinder.Points[0].X + m_oVFinder.Points[1].X) / 2;
                        vViewfinderCentre.Y = (m_oVFinder.Points[1].Y + m_oVFinder.Points[2].Y) / 2;

                        // Aggiorno le posizioni dei pallini
                        UpdateVertexCircle();
                    }

                    offset = newPosition;
                }
            }
        }
        static public void Polygon_MouseWheel(MouseWheelEventArgs e)
        {
            if (!isDragging)
            {
                // Recupero dimensioni finestra
                double dSizeX = m_Window.viewer360_View.m_ViewSize.Width;
                double dSizeY = m_Window.viewer360_View.m_ViewSize.Height;

                // Calcolo nuove posizioni
                m_aPointTmp.Clear();

                Point vP = new Point();
                if (Keyboard.IsKeyDown(Key.A))  // Stretch Verticale
                {
                    float fIncrease = (float)(e.Delta) / 25;

                    for (int i = 0; i < m_oVFinder.Points.Count; i++)
                    {
                        vP.X = m_oVFinder.Points[i].X;

                        if (i < 2)
                            vP.Y = m_oVFinder.Points[i].Y - fIncrease;
                        else
                            vP.Y = m_oVFinder.Points[i].Y + fIncrease;

                        m_aPointTmp.Add(vP);
                    }

                    // Verifico di non aver stratcato troppo
                    if (m_aPointTmp[0].Y > m_aPointTmp[3].Y - 5)  // minimo 5 pixel
                    {
                        vP.X = -1;
                        m_aPointTmp[0] = vP;
                    }
                }
                else if (Keyboard.IsKeyDown(Key.S)) // Stretch Orizzontale
                {
                    float fIncrease = (float)(e.Delta) / 25;
                    for (int i = 0; i < m_oVFinder.Points.Count; i++)
                    {
                        vP.Y = m_oVFinder.Points[i].Y;
                        if (i == 0 || i == 3)
                            vP.X = m_oVFinder.Points[i].X - fIncrease;
                        else
                            vP.X = m_oVFinder.Points[i].X + fIncrease;

                        m_aPointTmp.Add(vP);
                    }

                    // Verifico di non aver stratcato troppo
                    if (m_aPointTmp[0].X > m_aPointTmp[1].X - 10)   // minimo 10 pixel
                    {
                        vP.X = -1;
                        m_aPointTmp[0] = vP;
                    }
                }
                else  // Resize 
                {
                    float fIncrease = 1 + (float)(e.Delta) / 1500;
                    for (int i = 0; i < m_oVFinder.Points.Count; i++)
                    {
                        vP = vViewfinderCentre + (m_oVFinder.Points[i] - vViewfinderCentre) * fIncrease;
                        m_aPointTmp.Add(vP);
                    }

                    if (m_aPointTmp[0].X > m_aPointTmp[1].X - 10 || m_aPointTmp[0].Y > m_aPointTmp[3].Y - 10)   // minimo 10 pixel
                    {
                        vP.X = -1;
                        m_aPointTmp[0] = vP;
                    }
                    if (CUIManager.GetMode() == ViewerMode.Edit && CProjectPlane.m_bPlaneDefined)  // TODO aggiungere check opportuni
                        UpdateViewPolygonFromFace3D();
                }

                if (ArePointInsideView(m_aPointTmp, dSizeX, dSizeY)) // Se tutti i 4 vertici sono interni alla view aggiorno il mirino
                {
                    // Aggiorno i vertici
                    for (int i = 0; i < m_oVFinder.Points.Count; i++)
                        m_oVFinder.Points[i] = m_aPointTmp[i];

                    // Aggiorno centro mirino
                    vViewfinderCentre.X = (m_oVFinder.Points[0].X + m_oVFinder.Points[1].X) / 2;
                    vViewfinderCentre.Y = (m_oVFinder.Points[1].Y + m_oVFinder.Points[2].Y) / 2;

                    // Aggiorno le posizioni dei pallini
                    UpdateVertexCircle();

                }

            }
        }
        static public void Polygon_MouseUp()
        {
            isDragging = false;
            iDraggingPoint = -1;
        }





        static private void AddEllipse(Canvas canvas, double left, double top, int iPointIndex = -1)
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

            Canvas.SetLeft(ellipse, left - 4);
            Canvas.SetTop(ellipse, top - 4);

            canvas.Children.Add(ellipse);

            if (iPointIndex < 0) // Append
                m_EllipseList.Add(ellipse);
            else
                m_EllipseList.Insert(iPointIndex, ellipse);
        
        }

        static private void Cerchio_BottonUp(object sender, MouseButtonEventArgs e)
        {
            //++++++++++++++++++++++
            // Console.WriteLine("Cerchio_BottonUp");
            //++++++++++++++++++++++
            if (CUIManager.GetMode() == ViewerMode.Edit)
                m_oVFinder.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 0, 0));
            else
                m_oVFinder.Fill = null;

            Ellipse cerchioCliccato = sender as Ellipse;

            // Esegui qui le azioni desiderate in risposta al clic sul cerchio
            if (cerchioCliccato != null)
            {
                cerchioCliccato.Fill = Brushes.Green;
            }
            iDraggingPoint = -1;
        }

        static private void Cerchio_Click(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                return;
            //++++++++++++++++++++++
            //Console.WriteLine("Cerchio_Click");
            //++++++++++++++++++++++
            m_oVFinder.Fill = null;


            // Ottieni il cerchio su cui è stato effettuato il clic
            Ellipse cerchioCliccato = sender as Ellipse;

            // Esegui qui le azioni desiderate in risposta al clic sul cerchio
            if (cerchioCliccato != null)
            {
                cerchioCliccato.Fill = Brushes.Red;
                iDraggingPoint = FindEllipseIndex(cerchioCliccato.Name); 
            }
        }

        static private void DeleteCerchio_Click(object sender, MouseButtonEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
                return;

            if (m_EllipseList.Count == 3)  // Se sono già a 3 vertici smetto di eliminare punti
                return;

            // Ottieni il cerchio su cui è stato effettuato il clic
            Ellipse cerchioCliccato = sender as Ellipse;

            DeleteEllipse(cerchioCliccato);
        }

        static public void DeleteAllEllipse()
        {
            foreach (var eEllipse in m_EllipseList)
            {
                myCanvas.Children.Remove(eEllipse);
            }
            m_EllipseList.Clear();
        }

        static int FindEllipseIndex(string sEllipseName)
        {


            for (int i = 0; i < m_EllipseList.Count; i++)
            {
                if (m_EllipseList[i].Name == sEllipseName)
                    return i;
            }

            return -1;
        }

        static public void DeleteEllipse(Ellipse eEllipse)
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
                    m_oVFinder.Points.RemoveAt(iIndex);
                }
            }
        }

        public static void Init(System.Windows.Shapes.Polygon oFinder, Grid myGrid)
        {
            m_oVFinder = oFinder;
            m_EllipseList = new List<Ellipse>();
            m_aOriginalPolygon = new Point[m_oVFinder.Points.Count];
            iDraggingPoint = -1;

            m_iEllipseIncrementalNum = 0;
            myCanvas = new Canvas();
            Grid.SetRow(myCanvas, 1);
            Grid.SetColumnSpan(myCanvas, 5);
            myGrid.Children.Add(myCanvas);

            for (int i = 0; i < m_oVFinder.Points.Count; i++)
            {
                m_aOriginalPolygon[i].X = m_oVFinder.Points[i].X;
                m_aOriginalPolygon[i].Y = m_oVFinder.Points[i].Y;
                AddEllipse(myCanvas, m_oVFinder.Points[i].X, m_oVFinder.Points[i].Y, i);
            }


            // Inizializzo centro mirino
            vViewfinderCentre.X = (m_oVFinder.Points[0].X + m_oVFinder.Points[1].X) / 2;
            vViewfinderCentre.Y = (m_oVFinder.Points[1].Y + m_oVFinder.Points[2].Y) / 2;
            vVewfinderBBox.X = (m_oVFinder.Points[1].X - m_oVFinder.Points[0].X) / 2;
            vVewfinderBBox.Y = (m_oVFinder.Points[2].Y - m_oVFinder.Points[1].Y) / 2;

            m_aPointTmp = new PointCollection();



            if (m_bDebugMode == false)
            {
                m_Window.CreateMode.Visibility = Visibility.Collapsed;
                m_Window.EditMode.Visibility = Visibility.Collapsed;
                m_Window.Sep1.Visibility = Visibility.Collapsed;
            }

        }
        static public ViewerMode GetMode() { return m_eMode; }

        static public void UpdateViewPolygonFromFace3D()
        {
            if (m_oVFinder.Points == null)
                m_oVFinder.Points = new PointCollection { new Point(0, 0), new Point(0, 0), new Point(0, 0), new Point(0, 0) };

            for (int i = 0; i < 4; i++)
            {
                double dDist = CProjectPlane.CameraDist(CProjectPlane.m_aFace3DPoint[i], m_Window.viewer360_View.MyCam);
                if (dDist > 0)  // Dietro la telecamera
                {
                    m_oVFinder.Points = null;
                    return;
                }
                Point3D pTmp = new Point3D(CProjectPlane.m_aFace3DPoint[i].X, CProjectPlane.m_aFace3DPoint[i].Y, -CProjectPlane.m_aFace3DPoint[i].Z);
                m_oVFinder.Points[i] = Viewport3DHelper.Point3DtoPoint2D(m_Window.viewer360_View.vp, pTmp);
            }
        }

        static void CreateNewPoint(int iSegment, Point vPoint)
        {
            if (iSegment < m_oVFinder.Points.Count - 1)
            {
                m_oVFinder.Points.Insert(iSegment + 1, vPoint);
                AddEllipse(myCanvas, vPoint.X, vPoint.Y, iSegment + 1);
            }
            else
            {
                m_oVFinder.Points.Add(vPoint);
                AddEllipse(myCanvas, vPoint.X, vPoint.Y);
            }
        }

        static public void ResetPolygon()
        {

            DeleteAllEllipse();

            PointCollection NewPoints = new PointCollection();
            for (int i = 0; i < m_aOriginalPolygon.Length; i++)
            {
                Point p = new Point(m_aOriginalPolygon[i].X, m_aOriginalPolygon[i].Y);
                AddEllipse(myCanvas, p.X, p.Y, i);
                NewPoints.Add(p);
            }

            m_oVFinder.Points = NewPoints;

        }
        static public void RestorePolygon(CSingleFileLabel oLabel)
        {
            DeleteAllEllipse();

            PointCollection NewPoints = new PointCollection();
            double dScaleX;
            double dScaleY;
            if (m_Window.viewer360_View.GetProjection() == Viewer360View.ViewerProjection.Spheric)
            {
                dScaleX = 1 / SharingHelper.m_dConvFactor;
                dScaleY = 1 / SharingHelper.m_dConvFactor;
            }
            else
            {
                dScaleX = m_Window.viewer360_View.RenderSize.Width / oLabel.m_ImageWidth;
                dScaleY = m_Window.viewer360_View.RenderSize.Height / oLabel.m_ImageHeight;
            }

            for (int i = 0; i < oLabel.m_aLabelInfo[0].aPolyPointX.Count; i++)
            {
                Point p = new Point(oLabel.m_aLabelInfo[0].aPolyPointX[i] * dScaleX, oLabel.m_aLabelInfo[0].aPolyPointY[i] * dScaleY);
                AddEllipse(myCanvas, p.X, p.Y, i);
                NewPoints.Add(p);
            }

            m_oVFinder.Points = NewPoints;
        }

        static double Length(Point v)
        {
            return Math.Sqrt(v.X * v.X + v.Y * v.Y);
        }
        static int CheckSegmentClick(Point vPoint)
        {
            int iPixelTol = 4;

            Point vSide;

            double fMinDist2 = 1e+30;
            int iCandidate = -1;
            for (int i = 0; i < m_oVFinder.Points.Count; i++)
            {
                Point v1 = new Point(vPoint.X - m_oVFinder.Points[i].X, vPoint.Y - m_oVFinder.Points[i].Y);
                double d1Len = Length(v1);

                if (i < m_oVFinder.Points.Count - 1)
                    vSide = new Point(m_oVFinder.Points[i + 1].X - m_oVFinder.Points[i].X, m_oVFinder.Points[i + 1].Y - m_oVFinder.Points[i].Y);
                else
                    vSide = new Point(m_oVFinder.Points[0].X - m_oVFinder.Points[m_oVFinder.Points.Count - 1].X, m_oVFinder.Points[0].Y - m_oVFinder.Points[m_oVFinder.Points.Count - 1].Y);

                if (Length(vSide) < 1e-8)
                    continue;

                double dVSideLen = Length(vSide);
                vSide.X /= dVSideLen;
                vSide.Y /= dVSideLen;

                double fProjection = v1.X * vSide.X + v1.Y * vSide.Y;
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

        static bool ArePointInsideView(PointCollection aPointTmp, double dSizeX, double dSizeY)
        {
            // Se tutti i 4 vertici sono interni alla view aggiorno il mirino

            foreach (var point in aPointTmp)
            {
                if (point.X < 0 || point.X > dSizeX || point.Y < 0 || point.Y > dSizeY)
                    return false;
            }

            return true;
        }

        static void UpdateVertexCircle()
        {
            for (int i = 0; i < m_EllipseList.Count; i++)
            {
                Point point = m_oVFinder.Points[i];
                m_EllipseList[i].SetValue(Canvas.LeftProperty, point.X - 4);
                m_EllipseList[i].SetValue(Canvas.TopProperty, point.Y - 4);

            }

        }

        static public void RescaleViewfinderOnWindowChange(SizeChangedInfo sizeInfo)
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
            for (int i = 0; i < m_oVFinder.Points.Count; i++)
            {
                vNewDelta = new Point((m_oVFinder.Points[i].X - vViewfinderCentre.X) * dScaleX, (m_oVFinder.Points[i].Y - vViewfinderCentre.Y) * dScaleY);
                vP = new Point(vNewCentre.X + vNewDelta.X, vNewCentre.Y + vNewDelta.Y);
                m_oVFinder.Points[i] = vP;
            }

            vViewfinderCentre = vNewCentre;
            UpdateVertexCircle();

        }

        static public void InitUI(CSingleFileLabel oLabel)
        {
            CCatalogManager oCM = SharingHelper.GetCatalogManager();
            // Inizializzo le Combo
            int iCategoryIndex = oCM.GetCategoryIndex(oLabel.m_aLabelInfo[0].iCategory);
            m_Window.CategoryCombo.SelectedIndex= iCategoryIndex-1;

//            int iItemIndex = oCM.GetItemIndex(oLabel.m_aLabelInfo[0].iCategory, oLabel.m_aLabelInfo[0].iObjCatalogID);
            int iItemIndex = oCM.GetItemIndex(iCategoryIndex, oLabel.m_aLabelInfo[0].iObjCatalogID);
            m_Window.ItemCombo.SelectedIndex = iItemIndex;

            m_Window.ElementName.Text = oLabel.m_aLabelInfo[0].sLabelName;

            if(m_Window.viewer360_View.GetProjection()==Viewer360View.ViewerProjection.Spheric)
                (m_Window.DataContext as ViewModel.MainViewModel).RestoreFovAndPolygons(oLabel);
            else
                RestorePolygon(oLabel);
//            (m_Window.DataContext as ViewModel.MainViewModel).RestorePolygons(oLabel);

        }

        static public void UpdateUI()
        {

            if (m_eMode == ViewerMode.Create)
            {
                m_Window.NextImageButton.Visibility = Visibility.Visible;
                m_Window.PrevImageButton.Visibility = Visibility.Visible;
                m_Window.NextLabelButton.Visibility = Visibility.Collapsed;
                m_Window.PrevLabelButton.Visibility = Visibility.Collapsed;
                m_Window.PrevLabelButton.Visibility = Visibility.Collapsed;
                m_Window.DeleteLabelButton.Visibility = Visibility.Collapsed;
                m_Window.NewLabelButton.Visibility = Visibility.Collapsed;


                m_Window.CategoryCombo.IsEnabled = true;
                m_Window.CategoryCombo.BorderBrush = Brushes.Black;
                m_Window.CategoryCombo.Foreground = Brushes.Black;

                m_Window.ItemCombo.IsEnabled = true;
                m_Window.ItemCombo.BorderBrush = Brushes.Black;
                m_Window.ItemCombo.Foreground = Brushes.Black;

                m_Window.ElementName.IsEnabled = true;
                m_Window.ElementName.BorderBrush = Brushes.Black;
                m_Window.ElementName.Foreground = Brushes.Black;

                m_Window.SaveButton.Content = "Save new";

                if (m_iCategory == 4 || m_iCategory == 5)  // Windows or Door
                {
                    m_Window.Project2PlaneButton.Visibility = Visibility.Visible;
                    if (CProjectPlane.m_bPlaneDefined)
                    {
                        m_Window.Project2PlaneButton.BorderBrush = Brushes.Black;
                        m_Window.Project2PlaneButton.Foreground = Brushes.Black;
                        m_Window.Project2PlaneButton.IsEnabled = true;
                    }
                    else
                    {
                        m_Window.Project2PlaneButton.IsEnabled = false;
                        m_Window.Project2PlaneButton.BorderBrush = Brushes.LightGray;
                        m_Window.Project2PlaneButton.Foreground = Brushes.LightGray;
                    }

                    m_Window.AIButton.Visibility = Visibility.Visible;
                }
                else
                {
                    m_Window.Project2PlaneButton.Visibility = Visibility.Collapsed;
                    m_Window.AIButton.Visibility = Visibility.Collapsed;
                }



                }
            else  // Edit mode
            {
                m_Window.NextImageButton.Visibility = Visibility.Collapsed;
                m_Window.PrevImageButton.Visibility = Visibility.Collapsed;
                m_Window.NextLabelButton.Visibility = Visibility.Visible;
                m_Window.PrevLabelButton.Visibility = Visibility.Visible;
                m_Window.PrevLabelButton.Visibility = Visibility.Visible;
                m_Window.DeleteLabelButton.Visibility = Visibility.Visible;
                m_Window.NewLabelButton.Visibility = Visibility.Visible;

                m_Window.CategoryCombo.IsEnabled = false;
                m_Window.CategoryCombo.BorderBrush = Brushes.LightGray;
                m_Window.CategoryCombo.Foreground = Brushes.LightGray;

                m_Window.ItemCombo.IsEnabled = true;
                m_Window.ItemCombo.BorderBrush = Brushes.Black;
                m_Window.ItemCombo.Foreground = Brushes.Black;

                m_Window.ElementName.IsEnabled = true;
                m_Window.ElementName.BorderBrush = Brushes.Black;
                m_Window.ElementName.Foreground = Brushes.Black;

                m_Window.SaveButton.Content = "Save change";

                m_Window.Project2PlaneButton.Visibility = Visibility.Collapsed;

                m_Window.AIButton.Visibility = Visibility.Collapsed;
            }

        }
        static public void SetViewerMode(ViewerMode eMode)
        {
            if(m_eMode==eMode)
                return;

////            m_eMode = eMode;
            if (eMode == ViewerMode.Create)
            {
                m_Window.CreateMode.IsChecked = true;
                m_Window.EditMode.IsChecked = false;
            }
            else
            {
                m_Window.CreateMode.IsChecked = false;
                m_Window.EditMode.IsChecked = true;
            }

            ChangeMode();

            ////UpdateUI();
        }

        static public void ChangeMode()
        {
            if (m_Window.CreateMode.IsChecked == true)
            {

                if (m_eMode != ViewerMode.Create)  // Sto passando da Edit a Create
                {
                    ResetPolygon();  // Resetto il mirino
                }
                m_eMode = ViewerMode.Create;
                m_oVFinder.Fill = null;
            }
            else
            {
                if (m_eMode != ViewerMode.Edit)  // Sto passando da Create a Edit
                {
                    (m_Window.DataContext as ViewModel.MainViewModel).GetClosestLabel();

                    // Se non esiste alcuna label ripristino la modalità Create
                    if ((m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentLabelIndex == -1)
                    {
                        m_Window.CreateMode.IsChecked = true;
                        m_Window.EditMode.IsChecked = false;
                        m_eMode= ViewerMode.Create;   
                        UpdateUI();
                        return;
                    }

                }

                m_eMode = ViewerMode.Edit;
                m_oVFinder.Fill = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
            }
            UpdateUI();
        }

        static private void Project2Plane_Click(object sender, RoutedEventArgs e)
        {
            Ray3D oRay;

            Point3D[] aPoint = new Point3D[4];
            for (int i = 0; i < m_oVFinder.Points.Count; i++)
            {
                oRay = Viewport3DHelper.GetRay(m_Window.viewer360_View.vp, m_oVFinder.Points[i]);
                aPoint[i] = (Point3D)CProjectPlane.GetIntersection(oRay);
                aPoint[i].Z = -aPoint[i].Z;  // Per ragioni misteriose l'oggetto oRay è ribaltato rispetto a Z e quindi anche il punto di intersezione col piano (verticale!)
                aPoint[i] = m_Window.viewer360_View.PointLoc2Glob(aPoint[i]);
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
                aPoint[i] = m_Window.viewer360_View.PointGlob2Loc(aPoint[i]);

            // Memorizzo 
            CProjectPlane.m_aFace3DPoint = aPoint;

            // Aggiorno il mirino
            UpdateViewPolygonFromFace3D();

            //CUIManager.SetViewerMode(ViewerMode.Edit);
            //            CUIManager.ChangeMode();
        }


    }
}
