using HelixToolkit.Wpf;
using PointCloudUtility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Linq;
using Viewer360.Model;
using static PointCloudUtility.CCatalogManager;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static Viewer360.View.Viewer360View;

namespace Viewer360.View
{
    static public class CUIManager
    {
        // ************* Sezione inerente la UI esterna
        static public View.MainWindow m_Window;
        static ViewerMode m_eMode= ViewerMode.Undefined;
        static public bool m_bDebugMode = false;
        static int m_iCategory=-1;
        static int m_iObjCatalogId = -1;

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
        static private Point offset;  // Posizione mouse al momento di MouseDown
//        static private List<int> aItemDefaultEntry;
        static int m_iVFMode = 0; // 0=standard, 1==Projection mode
        static bool m_bProjDragging = false;
        static int m_iSide = -1;
        static bool m_bIgnoreCategoryChange = false;
        static public List<int> m_aObjId;
        static public List<bool> m_aViewerEditable;

        static void SetVFMode(int iMode)  // Modalità visualizzazione poligono: 0-->normale (dashed), 1-->proiettato (continuo)
        {
            if (iMode == m_iVFMode)
                return;

            m_iVFMode = iMode;
/*
            if(iMode == 0)
                CProjectPlane.RemovePlane();
*/
            ResetPolygon();

        }
        static public void SetCurrentCategory(int iCategory)
        {
            m_iCategory = iCategory;
            m_iObjCatalogId = m_aObjId[m_Window.ItemCombo.SelectedIndex];

            
            //if (iCategory != 4 && iCategory != 5)
            if (m_aViewerEditable[m_Window.ItemCombo.SelectedIndex]==false)
                CProjectPlane.RemovePlane();

        }
        static public int GetCurrentCategory()
        {
            return m_iCategory; // 1==Wall,2==Floor,3==Ceiling,4==Window,5==Door,6==PCSection
        }

        static public void Polygon_LeftCtrlMouseDown(MouseButtonEventArgs e)
        {
            m_bProjDragging = false;
            m_iSide = -1;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point vPoint = new Point(Mouse.GetPosition(m_Window.viewer360_View.vp).X, Mouse.GetPosition(m_Window.viewer360_View.vp).Y);

                // Verifico se ho cliccato su un segmento
                int iSegment = CheckSegmentClick(vPoint);

                //                if (m_iVFMode == 0 && (m_iCategory!=4 && m_iCategory!=5))
                if (m_iVFMode == 0 && (m_aViewerEditable[m_Window.ItemCombo.SelectedIndex] == false))
                {
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
                else  // visualizzazione in modalità projected
                {
                    m_bProjDragging = true;
                    m_iSide = iSegment;
                    offset = vPoint;
                    isDragging = true;
                    m_oVFinder.Fill = null;
                }
            }
        }
        static public void Polygon_MouseMove(System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && (isDragging || iDraggingPoint >= 0))
            {
                SharingHelper.m_bCameraAtHasChanged = true;

                // Recupero posizione mouse
                Point newPosition = new Point(Mouse.GetPosition(m_Window.viewer360_View.vp).X, Mouse.GetPosition(m_Window.viewer360_View.vp).Y);

                if (iDraggingPoint >= 0)  // Sto modificando la posizione di un vertice
                {
                    m_oVFinder.Points[iDraggingPoint] = newPosition;

                    // Aggiorno le posizioni dei pallini
                    UpdateVertexCircle();
                }
                else
                {

                    if (m_iVFMode == 0)  // Edit di label normale  // 0=standard, 1==Projection mode
                    {
                        double dX, dY;
                        // Calcolo nuove posizioni
                        m_aPointTmp.Clear();
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
                    }
                    else if (m_eMode == ViewerMode.Edit && m_iVFMode == 1)  // Edit di porte/finestra // 0=standard, 1==Projection mode
                    {
                        double dH = (newPosition.X - offset.X)/400;  // 1 pixel==1cm
                        double dV = -(newPosition.Y - offset.Y)/400; // 1 pixel==1cm

                        if (m_iSide==0)  // Bordo superiore
                        {
                            CProjectPlane.m_aFace3DPointGlob[0].Z += dV;
                            CProjectPlane.m_aFace3DPointGlob[1].Z += dV;

                        }
                        else if (m_iSide == 1)  // Bordo dx
                        {
                            CProjectPlane.m_aFace3DPointGlob[1].X += dH* CProjectPlane.m_vTangGlobal.X;
                            CProjectPlane.m_aFace3DPointGlob[1].Y += dH * CProjectPlane.m_vTangGlobal.Y;
                            CProjectPlane.m_aFace3DPointGlob[2].X += dH * CProjectPlane.m_vTangGlobal.X;
                            CProjectPlane.m_aFace3DPointGlob[2].Y += dH * CProjectPlane.m_vTangGlobal.Y;
                        }
                        else if (m_iSide == 2)  // Bordo inferiore
                        {
                            CProjectPlane.m_aFace3DPointGlob[2].Z += dV;
                            CProjectPlane.m_aFace3DPointGlob[3].Z += dV;

                        }
                        else if (m_iSide == 3)  // Bordo sn
                        {
                            CProjectPlane.m_aFace3DPointGlob[3].X += dH * CProjectPlane.m_vTangGlobal.X;
                            CProjectPlane.m_aFace3DPointGlob[3].Y += dH * CProjectPlane.m_vTangGlobal.Y;
                            CProjectPlane.m_aFace3DPointGlob[0].X += dH * CProjectPlane.m_vTangGlobal.X;
                            CProjectPlane.m_aFace3DPointGlob[0].Y += dH * CProjectPlane.m_vTangGlobal.Y;
                        }

                        for (int i = 0; i < 4; i++)
                            CProjectPlane.m_aFace3DPointLoc[i] = m_Window.viewer360_View.PointGlob2Loc(CProjectPlane.m_aFace3DPointGlob[i]);

                        UpdateViewPolygonFromFace3D();
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

            //            if (m_iVFMode == 1 && (m_iCategory == 4 || m_iCategory == 5))
            if (m_iVFMode == 1 && m_aViewerEditable[m_Window.ItemCombo.SelectedIndex])
            {
                m_oVFinder.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 0, 0));
            }
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
            //ellipse.PreviewMouseLeftButtonDown += Cerchio_Click;

            ellipse.MouseLeftButtonUp += Cerchio_BottonUp;
            //ellipse.PreviewMouseLeftButtonUp += Cerchio_BottonUp;

            ellipse.MouseRightButtonDown += DeleteCerchio_Click;
            //ellipse.PreviewMouseRightButtonDown += DeleteCerchio_Click;

            ellipse.PreviewMouseMove += m_Window.viewer360_View.vp_MouseMove;

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
            {
                m_oVFinder.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 0, 0));
            }
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
            //            if (!Keyboard.IsKeyDown(Key.LeftCtrl) || m_iVFMode==1 || m_iCategory==4 || m_iCategory==5)
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) || m_iVFMode == 1 || m_aViewerEditable[m_Window.ItemCombo.SelectedIndex])
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
            SharingHelper.m_bCameraAtHasChanged = true;

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

                    SharingHelper.m_bCameraAtHasChanged = true;

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
            m_aObjId = new List<int>();
            m_aViewerEditable = new List<bool>();
            //            aItemDefaultEntry = new List<int>();
            m_iVFMode = 0;
        }
        static public ViewerMode GetMode() { return m_eMode; }

        static public void UpdateViewPolygonFromFace3D()
        {
            if (m_oVFinder.Points == null)
                m_oVFinder.Points = new PointCollection { new Point(0, 0), new Point(0, 0), new Point(0, 0), new Point(0, 0) };


            double dDist;
            if (m_Window.viewer360_View.GetProjection() == ViewerProjection.Spheric)
            {
                for (int i = 0; i < 4; i++)
                {
                    dDist = CProjectPlane.CameraDist(CProjectPlane.m_aFace3DPointLoc[i], m_Window.viewer360_View.MyCam);
                    if (dDist > 0)  // Dietro la telecamera
                    {
                        m_oVFinder.Points = null;
                        return;
                    }
                    Point3D pTmp = new Point3D(CProjectPlane.m_aFace3DPointLoc[i].X, CProjectPlane.m_aFace3DPointLoc[i].Y, -CProjectPlane.m_aFace3DPointLoc[i].Z);
                    m_oVFinder.Points[i] = Viewport3DHelper.Point3DtoPoint2D(m_Window.viewer360_View.vp, pTmp);
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    dDist = CProjectPlane.CameraDist(CProjectPlane.m_aFace3DPointLoc[i], (m_Window.viewer360_View.vp.Camera as OrthographicCamera));
                    if (dDist < 0)  // Dietro la telecamera
                    {
                        m_oVFinder.Points = null;
                        return;
                    }
                    Point3D pTmp = new Point3D(CProjectPlane.m_aFace3DPointLoc[i].X, CProjectPlane.m_aFace3DPointLoc[i].Y, CProjectPlane.m_aFace3DPointLoc[i].Z);
                    m_oVFinder.Points[i] = MyPoint3DtoPoint2D(pTmp);
                }

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
                if (m_iVFMode == 0  || m_eMode == ViewerMode.Create)
                    AddEllipse(myCanvas, p.X, p.Y, i);
                NewPoints.Add(p);
            }
            m_oVFinder.Points = NewPoints;

            if (m_iVFMode == 0)
            {
                m_oVFinder.StrokeDashArray = new DoubleCollection(new double[] { 2, 2 }); ;
            }
            else
            {
                m_oVFinder.StrokeDashArray = null;
            }
        }
        static public void RestorePolygon(CSingleFileLabel oLabel)
        {
            DeleteAllEllipse();


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

            int iCat = oLabel.m_aLabelInfo[0].iCategory;
            if (m_aViewerEditable[m_Window.ItemCombo.SelectedIndex] == false)
            //if (iCat != 4 && iCat != 5)  // Non windows/door
            {
                PointCollection NewPoints = new PointCollection();

                for (int i = 0; i < oLabel.m_aLabelInfo[0].aPolyPointX.Count; i++)
                {
                    Point p = new Point(oLabel.m_aLabelInfo[0].aPolyPointX[i] * dScaleX, oLabel.m_aLabelInfo[0].aPolyPointY[i] * dScaleY);
                    AddEllipse(myCanvas, p.X, p.Y, i);
                    NewPoints.Add(p);
                }

                m_oVFinder.Points = NewPoints;
                CProjectPlane.RemovePlane();
                SetVFMode(0);

            }
            else
            {
                m_oVFinder.Points = null;
                CSingleFileLabel.SLabelInfo oLInfo= oLabel.m_aLabelInfo[0];

                // Calcolo piano di proiezione
                double dPosX = oLInfo.aPolyPointX[0];
                double dPosY = oLInfo.aPolyPointY[0];
                double dNX = oLInfo.aPolyPointY[1] - oLInfo.aPolyPointY[0];
                double dNY = oLInfo.aPolyPointX[1] - oLInfo.aPolyPointX[0];
                double dLen = Math.Sqrt(dNX * dNX + dNY * dNY);
                CProjectPlane.SetPlane(dPosX, dPosY, dNX / dLen, dNY / dLen, oLInfo.sParentElementName);

                Point3D[] aPoint = new Point3D[oLInfo.aPolyPointX.Count];

                for (int i = 0; i < oLInfo.aPolyPointX.Count; i++)
                    aPoint[i] = new Point3D(oLInfo.aPolyPointX[i], oLInfo.aPolyPointY[i], oLInfo.aPolyPointZ[i]);

                CProjectPlane.m_aFace3DPointGlob = aPoint;

                // Trasformo i punti in coordinate locali
                CProjectPlane.m_aFace3DPointLoc = new Point3D[4];
                for (int i = 0; i < 4; i++)
                    CProjectPlane.m_aFace3DPointLoc[i] = m_Window.viewer360_View.PointGlob2Loc(CProjectPlane.m_aFace3DPointGlob[i]);

                CProjectPlane.ComputeTangAxes();

                SetVFMode(1);

                // Ricalcolo coordinate schermo
                UpdateViewPolygonFromFace3D();

            }

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


        static public void RescaleViewfinderOnFovChange(double dOldFov, double dNewFov)
        {
            if(dOldFov==dNewFov)
                return;

            if (m_iVFMode == 1)
                UpdateViewPolygonFromFace3D();
            else
            {
                // TODO
            }

/*
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
*/
        }

        static public void InitUI(CSingleFileLabel oLabel)
        {
            CCatalogManager oCM = SharingHelper.GetCatalogManager();

            // Imposto la Family
            string sFamilyName = oCM.GetFamilyNameFromObjID(oLabel.m_aLabelInfo[0].iObjCatalogID);
            if(sFamilyName!= m_Window.FamilyCombo.SelectedValue.ToString())
                m_Window.FamilyCombo.SelectedValue= sFamilyName;

            // Imposto la Category
            string sCategoryName = oCM.SearchCategoryUINameByID(oLabel.m_aLabelInfo[0].iCategory);
            if (sCategoryName != m_Window.CategoryCombo.SelectedValue.ToString())
                m_Window.CategoryCombo.SelectedValue = sCategoryName;

            // Imposto Item
            string sObjName = oCM.SearchObjUITypeByID(oLabel.m_aLabelInfo[0].iObjCatalogID);
            if (sObjName != m_Window.ItemCombo.SelectedValue.ToString())
                m_Window.ItemCombo.SelectedValue = sObjName;

            // Imposto Label name
            m_Window.ElementName.Text = oLabel.m_aLabelInfo[0].sLabelName;

            // Ripristino poligono
            if(m_Window.viewer360_View.GetProjection()==Viewer360View.ViewerProjection.Spheric)
                (m_Window.DataContext as ViewModel.MainViewModel).RestoreFovAndPolygons(oLabel);
            else
                RestorePolygon(oLabel);

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
                //m_Window.NewLabelButton.Visibility = Visibility.Collapsed;
                m_Window.NewLabelButton.Content = "View Element";


                m_Window.CategoryCombo.IsEnabled = true;
                m_Window.CategoryCombo.BorderBrush = Brushes.Black;
                m_Window.CategoryCombo.Foreground = Brushes.Black;

                m_Window.CategoryCombo.IsEnabled = true;
                m_Window.CategoryCombo.BorderBrush = Brushes.Black;
                m_Window.CategoryCombo.Foreground = Brushes.Black;

                m_Window.ElementName.IsEnabled = true;
                m_Window.ElementName.BorderBrush = Brushes.Black;
                m_Window.ElementName.Foreground = Brushes.Black;

                m_Window.SaveButton.Content = "Save new";

//                if (m_iCategory == 4 || m_iCategory == 5)  // Windows or Door
                if (m_aViewerEditable[m_Window.ItemCombo.SelectedIndex])  // Windows or Door
                {
                    m_Window.Project2PlaneButton.Visibility = Visibility.Visible;
                    m_Window.SaveButton.Visibility = Visibility.Collapsed;
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
                    m_Window.SaveButton.Visibility = Visibility.Visible;
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
                //m_Window.NewLabelButton.Visibility = Visibility.Visible;
                m_Window.NewLabelButton.Content = "New Element";


                m_Window.CategoryCombo.IsEnabled = false;
                m_Window.CategoryCombo.BorderBrush = Brushes.LightGray;
                m_Window.CategoryCombo.Foreground = Brushes.LightGray;

                m_Window.CategoryCombo.IsEnabled = true;
                m_Window.CategoryCombo.BorderBrush = Brushes.Black;
                m_Window.CategoryCombo.Foreground = Brushes.Black;

                m_Window.ElementName.IsEnabled = true;
                m_Window.ElementName.BorderBrush = Brushes.Black;
                m_Window.ElementName.Foreground = Brushes.Black;

                m_Window.SaveButton.Visibility = Visibility.Visible;
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
                    m_eMode = ViewerMode.Create;
                    ResetPolygon();  // Resetto il mirino
                    SetVFMode(0);  // Sicuramentein questa modalià il "mirino" è standard
                    SharingHelper.m_oSendCategoryToServer = new CObjShortInfo(m_iCategory, m_iObjCatalogId);  // Scatena aggiornamento server con restituzione info muro 
                }
                
                m_oVFinder.Fill = null;
            }
            else
            {
                if (m_eMode != ViewerMode.Edit)  // Sto passando da Create a Edit
                {
                    m_eMode = ViewerMode.Edit;  // MAH!! SPERIAMO BENE
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

        static Point MyPoint3DtoPoint2D(Point3D oPoint3D)
        {
            Point oPoint = new Point(-1000000, -1000000);

            OrthographicCamera oCam = (m_Window.viewer360_View.vp.Camera as OrthographicCamera);
            Vector3D vTras = Vector3D.CrossProduct(oCam.LookDirection, oCam.UpDirection);
            vTras.Normalize();

            // Calcolo ray come differenza fra punto e centro camera
            Vector3D vRayDirection = new Vector3D(oPoint3D.X - oCam.Position.X, oPoint3D.Y - oCam.Position.Y, oPoint3D.Z - oCam.Position.Z);
            vRayDirection.Normalize();

            // Proietto raggio lungo vCameraAt
            double dTmp = Vector3D.DotProduct(oCam.LookDirection, vRayDirection);
            if (dTmp > 0)
                vRayDirection /= dTmp;
            else
                return oPoint;

            //+++++++++++++++++++++++++++++++++++
            double dTest= Vector3D.DotProduct(oCam.LookDirection, vRayDirection);
            //+++++++++++++++++++++++++++++++++++

            // Calcolo vettore scostamento da centro camera 
            Vector3D vDiff =vRayDirection - oCam.LookDirection;

            double dX = Vector3D.DotProduct(vDiff, vTras);
            double dY = Vector3D.DotProduct(vDiff , oCam.UpDirection);

            double dSizeX = m_Window.viewer360_View.m_ViewSize.Width / 2;
            double dSizeY = m_Window.viewer360_View.m_ViewSize.Height/ 2;  // La Y dell'immagine corrisponde alla Up della camera

            oPoint = new Point(dSizeX*(1+dX/Math.Sin(m_Window.viewer360_View.Hfov/2)), dSizeY * (1 - dY / Math.Sin(m_Window.viewer360_View.Vfov/2)));

            return oPoint;
        }

        static Ray3D OrthoCameraGetRay(Point oPoint)
        {
            Ray3D oRay=new Ray3D();

            OrthographicCamera oCam= (m_Window.viewer360_View.vp.Camera as OrthographicCamera);

            // Imposto origine raggio in centro camera
            oRay.Origin = oCam.Position;

            // Calcolo la direzione
            Vector3D vRayDirection = new Vector3D();
            Vector3D vTras = Vector3D.CrossProduct(oCam.LookDirection, oCam.UpDirection);
            vTras.Normalize();


            double dSizeX = m_Window.viewer360_View.m_ViewSize.Width / 2;
            double dSizeY = m_Window.viewer360_View.m_ViewSize.Height / 2;  // La Y dell'immagine corrisponde alla Up della camera

            double dX = (oPoint.X / dSizeX - 1) * Math.Sin(m_Window.viewer360_View.Hfov/2);
            double dY = (oPoint.Y / dSizeY - 1) * Math.Sin(m_Window.viewer360_View.Vfov/2);

            vRayDirection = oCam.LookDirection + vTras * dX - oCam.UpDirection * dY;

            oRay.Direction = vRayDirection;
            return oRay;
        }
        static public void Project2Plane_Click()
        {
            Ray3D oRay;

            Point3D[] aPoint = new Point3D[4];
            if (m_Window.viewer360_View.GetProjection() == Viewer360View.ViewerProjection.Spheric)  // Proiezione sferica --> uso Viewport3DHelper per calcolo raggi
            {
                for (int i = 0; i < m_oVFinder.Points.Count; i++)
                {
                    oRay = Viewport3DHelper.GetRay(m_Window.viewer360_View.vp, m_oVFinder.Points[i]);

                    oRay.Origin = new Point3D(oRay.Origin.X, oRay.Origin.Y, -oRay.Origin.Z);
                    oRay.Direction = new Vector3D(oRay.Direction.X, oRay.Direction.Y, -oRay.Direction.Z);

                    aPoint[i] = (Point3D)CProjectPlane.GetIntersection(oRay);
                    aPoint[i] = m_Window.viewer360_View.PointLoc2Glob(aPoint[i]);

                    //+++++++++++++++++++++
                    //Vector3D vTmp=m_Window.viewer360_View.VectorLoc2Glob(oRay.Direction);
                    //vTmp.Z = 0;
                    //vTmp.Normalize();
                    //Point3D vP= m_Window.viewer360_View.PointLoc2Glob(new Point3D(oRay.Direction.X, oRay.Direction.Y, oRay.Direction.Z));
                    //+++++++++++++++++++++
                }
            }
            else  // Proiezione planare --> mi calcolo i raggi a mano
            {
                for (int i = 0; i < m_oVFinder.Points.Count; i++)
                {
                    oRay = OrthoCameraGetRay(m_oVFinder.Points[i]);

                    //++++++++++++++++++++++++++++++++++++++++++++++++++++
                    //Point3D oTmp3D = new Point3D(oRay.Origin.X + oRay.Direction.X, oRay.Origin.Y + oRay.Direction.Y, oRay.Origin.Z + oRay.Direction.Z);
                    //Point oTmp=MyPoint3DtoPoint2D(oTmp3D);
                    //++++++++++++++++++++++++++++++++++++++++++++++++++++

                    aPoint[i] = (Point3D)CProjectPlane.GetIntersection(oRay);
                    aPoint[i] = m_Window.viewer360_View.PointLoc2Glob(aPoint[i]);

                }
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

            // Memorizzo punti in coordinate Globali
            CProjectPlane.m_aFace3DPointGlob = aPoint;

            // Trasformo i punti in coordinate locali
            CProjectPlane.m_aFace3DPointLoc = new Point3D[4];
            for (int i = 0; i < 4; i++)
                CProjectPlane.m_aFace3DPointLoc[i] = m_Window.viewer360_View.PointGlob2Loc(CProjectPlane.m_aFace3DPointGlob[i]);

//            // Passo a modalità Proiezione
//            SetVFMode(1);

            // Salvo tutto sul file .mlb
            m_Window.viewer360_View.SaveMlb();

            // Passo a modalità Proiezione
            SetVFMode(1);

            // Calcolo l'asse orizzontale (per eventuale edit)
            CProjectPlane.ComputeTangAxes();

            // Aggiorno il mirino
            UpdateViewPolygonFromFace3D();

            CUIManager.SetViewerMode(ViewerMode.Edit);
            //CUIManager.ChangeMode();
        }
        
        static public void FamilySelectionChanged()
        {
            Dictionary<string, int> oFamilyDictionary=SharingHelper.GetCatalogManager().GetFamilyDictionary();

            string sSelectedFamily= m_Window.FamilyCombo.SelectedValue.ToString();
            int iFamilyId = oFamilyDictionary[sSelectedFamily];

            List<CCategoryInfo> aCategoryList= SharingHelper.GetCatalogManager().GetCategoryListByFamily(iFamilyId);
            m_bIgnoreCategoryChange = true;
            m_Window.CategoryCombo.Items.Clear();
            for (int iItem = 0; iItem < aCategoryList.Count; iItem++)
                m_Window.CategoryCombo.Items.Add(aCategoryList[iItem].sUI_CategoryType);
            m_bIgnoreCategoryChange = false;


            if (m_Window.CategoryCombo.SelectedIndex == 0)
                CategorySelectionChanged();
            else
                m_Window.CategoryCombo.SelectedIndex = 0;

        }

        static public void ComputeFrustumVersor(out List<MyVector3D> avPoint, ref int iThetaMin, ref int iThetaMax)
        {
            Vector3D vTras;
            if (m_Window.viewer360_View.GetProjection() == ViewerProjection.Spheric)
            {
                vTras = Vector3D.CrossProduct(m_Window.viewer360_View.MyCam.LookDirection, m_Window.viewer360_View.MyCam.UpDirection);
            }
            else
            {
                OrthographicCamera oCam = (m_Window.viewer360_View.vp.Camera as OrthographicCamera);
                vTras = Vector3D.CrossProduct(oCam.LookDirection, oCam.UpDirection);
            }
            vTras.Normalize();
            Vector3D vTras1 = m_Window.viewer360_View.ApplyGlobalRotation(vTras);
            vTras1.Z = 0;
            vTras1.Normalize();

            avPoint = new List<MyVector3D>();
            double fThetaMin = 10000;
            double fThetaMax = -10000;
            double fTmp;

            // Crezione linee Frustrum
            List<Point> aPoint = new List<Point>();
            Point pFrustPoint;
            pFrustPoint = new Point(0, 0);
            aPoint.Add(pFrustPoint);
            pFrustPoint = new Point(m_Window.viewer360_View.m_ViewSize.Width - 1, 0);
            aPoint.Add(pFrustPoint);
            pFrustPoint = new Point(m_Window.viewer360_View.m_ViewSize.Width - 1, m_Window.viewer360_View.m_ViewSize.Height - 1);
            aPoint.Add(pFrustPoint);
            pFrustPoint = new Point(0, m_Window.viewer360_View.m_ViewSize.Height - 1);
            aPoint.Add(pFrustPoint);

            for (int i = 0; i < aPoint.Count; i++)
            {
                Ray3D oRay = null;
                if (m_Window.viewer360_View.GetProjection() == Viewer360View.ViewerProjection.Spheric)  // Proiezione sferica --> uso Viewport3DHelper per calcolo raggi
                    oRay = Viewport3DHelper.GetRay(m_Window.viewer360_View.vp, aPoint[i]);
                else
                    oRay = OrthoCameraGetRay(aPoint[i]);

                Vector3D v = new Vector3D(oRay.Direction.X, oRay.Direction.Y, -oRay.Direction.Z);
                v.Normalize();
                v = -m_Window.viewer360_View.ApplyGlobalRotation(v);

                // Aggiungo a lista punti
                avPoint.Add(new MyVector3D(v.X, v.Y, v.Z));

                // Identifico estremi sinistro e destro 
                v.Z = 0;
                v.Normalize();
                fTmp = Vector3D.DotProduct(vTras1, v);

                if (fTmp < fThetaMin)
                {
                    fThetaMin = fTmp;
                    iThetaMin = i;
                }
                if (fTmp > fThetaMax)
                {
                    fThetaMax = fTmp;
                    iThetaMax = i;
                }
            }

        }

        static public void ComputeVFVersor(out List<MyVector3D> avPoint, ref int iThetaMin, ref int iThetaMax)
        {

            Vector3D vTras;
            if (m_Window.viewer360_View.GetProjection() == ViewerProjection.Spheric)
            {
                vTras = Vector3D.CrossProduct(m_Window.viewer360_View.MyCam.LookDirection, m_Window.viewer360_View.MyCam.UpDirection);
            }
            else
            {
                OrthographicCamera oCam = (m_Window.viewer360_View.vp.Camera as OrthographicCamera);
                vTras = Vector3D.CrossProduct(oCam.LookDirection, oCam.UpDirection);
            }
            vTras.Normalize();
            Vector3D vTras1 = m_Window.viewer360_View.ApplyGlobalRotation(vTras);
            vTras1.Z = 0;
            vTras1.Normalize();

            vTras.Z = 0;
            vTras.Normalize();
            if(m_oVFinder.Points==null || m_oVFinder.Points.Count==0)
            {
                avPoint = null;
                return;
            }
            avPoint = new List<MyVector3D>();
            double fThetaMin = 10000;
            double fThetaMax = -10000;
            double fTmp;

            for (int i = 0; i < m_oVFinder.Points.Count; i++)
            {
                Ray3D oRay = null;
                if (m_Window.viewer360_View.GetProjection() == Viewer360View.ViewerProjection.Spheric)  // Proiezione sferica --> uso Viewport3DHelper per calcolo raggi
                    oRay = Viewport3DHelper.GetRay(m_Window.viewer360_View.vp, m_oVFinder.Points[i]);
                else
                    oRay = OrthoCameraGetRay(m_oVFinder.Points[i]);

                Vector3D v = new Vector3D(oRay.Direction.X, oRay.Direction.Y, -oRay.Direction.Z);
                v.Normalize();
                Vector3D v1 = -m_Window.viewer360_View.ApplyGlobalRotation(v);
                v= -m_Window.viewer360_View.VectorLoc2Glob(v);

                // Aggiungo a lista punti
                avPoint.Add(new MyVector3D(v.X, v.Y, v.Z));

                // Identifico estremi sinistro e destro 
                v.Z = 0;
                v.Normalize();
                fTmp = Vector3D.DotProduct(vTras1, v);


                if (fTmp < fThetaMin)
                {
                    fThetaMin = fTmp;
                    iThetaMin = i;
                }
                if (fTmp > fThetaMax)
                {
                    fThetaMax = fTmp;
                    iThetaMax = i;
                }

            }

            //+++++++++++++++++++++++++++
            /*
            Vector3D vTmp0 = new Vector3D(avPoint[0].X, avPoint[0].Y, 0);
            vTmp0.Normalize();
            Vector3D vTmp3 = new Vector3D(avPoint[3].X, avPoint[3].Y, 0);
            vTmp3.Normalize();
            */
            //+++++++++++++++++++++++++++

        }

        static public Vector3D ScreenPointToVersor(Point vPoint, Matrix3D oRMatrix)  // Restituisce il versore (in coordinate locali) associato al punto di coordinate schermo vPoint
        {
            double dSizeX = m_Window.viewer360_View.m_ViewSize.Width / 2;
            double dSizeY = m_Window.viewer360_View.m_ViewSize.Height / 2;

            double dFovH2 = m_Window.viewer360_View.Hfov * Math.PI / 360;
            double dFovV2 = m_Window.viewer360_View.Vfov * Math.PI / 360;

            // Calcolo parametri angolari di punto corrente
            double fTheta = Math.Atan(Math.Tan(dFovH2) * (vPoint.X - dSizeX) / (dSizeX)) + Math.PI / 2;
            double fPhi = Math.Atan(Math.Tan(dFovV2) * (vPoint.Y - dSizeY) / (dSizeY)) + Math.PI / 2;
            //++++++++++++++++++++++++++++++++
            //Console.WriteLine("fPhi="+fPhi.ToString(CultureInfo.InvariantCulture));
            //+++++++++++++++++++++++++++++++++

            // Calcolo versore vertice i (rispetto alla camera)
            Vector3D v = -GeometryHelper.GetNormal(fTheta, fPhi);

            // Ruoto v in base a orientamento camera
            double dX = oRMatrix.M11 * v.X + oRMatrix.M12 * v.Y + oRMatrix.M13 * v.Z;
            double dY = oRMatrix.M21 * v.X + oRMatrix.M22 * v.Y + oRMatrix.M23 * v.Z;
            double dZ = oRMatrix.M31 * v.X + oRMatrix.M32 * v.Y + oRMatrix.M33 * v.Z;
            v = new Vector3D(dX, dY, dZ);
            v.Z = -v.Z;  // Per la solita ragione misteriosa inverto la Z

            return v;
        }

        static public void CategorySelectionChanged()
        {
            if (m_bIgnoreCategoryChange)
                return;
             
            string sCategorySelected = m_Window.CategoryCombo.SelectedValue.ToString();
            int iCatId=SharingHelper.GetCatalogManager().GetCategoryDictionary()[sCategorySelected];
            List<CObjInfo> aObjList= SharingHelper.GetCatalogManager().GetObjListByCategory(iCatId);

            m_Window.ItemCombo.Items.Clear();
            m_aObjId.Clear();
            m_aViewerEditable.Clear();
            for (int iItem = 0; iItem < aObjList.Count; iItem++)
            {
                m_Window.ItemCombo.Items.Add(aObjList[iItem].sUI_ObjType);
                m_aObjId.Add(aObjList[iItem].nId);
                m_aViewerEditable.Add(aObjList[iItem].oCatalogEntityInfo.m_bViewerEditable);
            }

            m_Window.ItemCombo.SelectedIndex = 0; // aItemDefaultEntry[m_Window.CategoryCombo.SelectedIndex];
            m_Window.ElementName.Text = m_Window.CategoryCombo.SelectedItem.ToString();

            if(iCatId != m_iCategory)
                SetVFMode(0);

            CUIManager.SetCurrentCategory(iCatId);
            CUIManager.UpdateUI();

            //            SharingHelper.m_iSendCategoryToServer = iCatId;
            SharingHelper.m_oSendCategoryToServer = new CObjShortInfo(m_iCategory, m_aObjId[m_Window.ItemCombo.SelectedIndex]);

        }

        static public void StartUpUI()
        {
            Dictionary<string, int> oFamilyDictionary = SharingHelper.GetCatalogManager().GetFamilyDictionary();

            m_Window.FamilyCombo.Items.Clear();
            foreach (var oFam in  oFamilyDictionary) 
                m_Window.FamilyCombo.Items.Add(oFam.Key);

            m_Window.FamilyCombo.SelectedIndex = 0;

            /*
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();

            int iDefEntry;
            for (int iCat = 1; iCat < oLList.Count; iCat++)
            {
                m_Window.CategoryCombo.Items.Add(oLList[iCat][0].sCategory);

                iDefEntry = 0;
                for (int iItem = 0; iItem < oLList[iCat].Count; iItem++)
                {
                    if (oLList[iCat][iItem].bUIDefEntry)
                    {
                        iDefEntry = iItem;
                        break;
                    }
                }
                aItemDefaultEntry.Add(iDefEntry);
            }

            m_Window.CategoryCombo.SelectedIndex = 2;  // Wall
*/
        }

    }
}
