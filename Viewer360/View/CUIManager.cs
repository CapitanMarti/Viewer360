﻿using HelixToolkit.Wpf;
using PointCloudUtility;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing.Printing;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
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
        static private List<int> aItemDefaultEntry;
        static int m_iVFMode = 0; // 0=standard, 1==Projection mode
        static bool m_bProjDragging = false;
        static int m_iSide = -1;


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
            m_iCategory = iCategory;  // 1==Wall,2==Floor,3==Ceiling,4==Window,5==Door,6==PCSection
            if(iCategory != 4 && iCategory != 5)
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

                if (m_iVFMode == 0 && (m_iCategory!=4 && m_iCategory!=5))
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

                    if (m_iVFMode == 0)  // Edit di label normale
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
                    else if (m_eMode == ViewerMode.Edit && m_iVFMode == 1)  // Edit di porte/finestra
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
            if (m_iVFMode == 1 && (m_iCategory == 4 || m_iCategory == 5))
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
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) || m_iVFMode==1 || m_iCategory==4 || m_iCategory==5)
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

            aItemDefaultEntry = new List<int>();
            m_iVFMode = 0;
        }
        static public ViewerMode GetMode() { return m_eMode; }

        static public void UpdateViewPolygonFromFace3D()
        {
            if (m_oVFinder.Points == null)
                m_oVFinder.Points = new PointCollection { new Point(0, 0), new Point(0, 0), new Point(0, 0), new Point(0, 0) };

            for (int i = 0; i < 4; i++)
            {
                double dDist = CProjectPlane.CameraDist(CProjectPlane.m_aFace3DPointLoc[i], m_Window.viewer360_View.MyCam);
                if (dDist > 0)  // Dietro la telecamera
                {
                    m_oVFinder.Points = null;
                    return;
                }
                Point3D pTmp = new Point3D(CProjectPlane.m_aFace3DPointLoc[i].X, CProjectPlane.m_aFace3DPointLoc[i].Y, -CProjectPlane.m_aFace3DPointLoc[i].Z);
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
            if (iCat != 4 && iCat != 5)  // Non windows/door
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
                //m_Window.NewLabelButton.Visibility = Visibility.Collapsed;
                m_Window.NewLabelButton.Content = "View Element";


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

                m_Window.ItemCombo.IsEnabled = true;
                m_Window.ItemCombo.BorderBrush = Brushes.Black;
                m_Window.ItemCombo.Foreground = Brushes.Black;

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
                    SharingHelper.m_iSendCategoryToServer = m_iCategory;  // Scatena aggiornamento server con restituzione info muro 
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

        static public void Project2Plane_Click()
        {
            Ray3D oRay;

            Point3D[] aPoint = new Point3D[4];
            for (int i = 0; i < m_oVFinder.Points.Count; i++)
            {
                oRay = Viewport3DHelper.GetRay(m_Window.viewer360_View.vp, m_oVFinder.Points[i]);
                //++++++++++++++++++
                oRay.Origin = new Point3D(oRay.Origin.X, oRay.Origin.Y, -oRay.Origin.Z);
                oRay.Direction= new Vector3D(oRay.Direction.X, oRay.Direction.Y, -oRay.Direction.Z);
                //++++++++++++++++++
                aPoint[i] = (Point3D)CProjectPlane.GetIntersection(oRay);
//                aPoint[i].Z = -aPoint[i].Z;  // Per ragioni misteriose l'oggetto oRay è ribaltato rispetto a Z e quindi anche il punto di intersezione col piano (verticale!)
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

            // Memorizzo punti in coordinate Globali
            CProjectPlane.m_aFace3DPointGlob = aPoint;

            // Trasformo i punti in coordinate locali
            CProjectPlane.m_aFace3DPointLoc = new Point3D[4];
            for (int i = 0; i < 4; i++)
                CProjectPlane.m_aFace3DPointLoc[i] = m_Window.viewer360_View.PointGlob2Loc(CProjectPlane.m_aFace3DPointGlob[i]);

            // Passo a modalità Proiezione
            SetVFMode(1);

            // Salvo tutto sul file .mlb
            m_Window.viewer360_View.SaveMlb();

            // Calcolo l'asse orizzontale (per eventuale edit)
            CProjectPlane.ComputeTangAxes();

            // Aggiorno il mirino
            UpdateViewPolygonFromFace3D();

            CUIManager.SetViewerMode(ViewerMode.Edit);
            //CUIManager.ChangeMode();
        }


        static public void CategorySelectionChanged()
        {
            List<List<CCatalogManager.CObjInfo>> oLList = SharingHelper.GetAllLabelGroupedByCategory();

            m_Window.ItemCombo.Items.Clear();
            int iSelectedCatIndex = m_Window.CategoryCombo.SelectedIndex + 1; // Ho escluso la categoria 0 

            for (int iItem = 0; iItem < oLList[iSelectedCatIndex].Count; iItem++)
                m_Window.ItemCombo.Items.Add(oLList[iSelectedCatIndex][iItem].sUI_CategoryInfo);

            m_Window.ItemCombo.SelectedIndex = aItemDefaultEntry[m_Window.CategoryCombo.SelectedIndex];
            m_Window.ElementName.Text = m_Window.CategoryCombo.SelectedItem.ToString();

            int iCat = oLList[iSelectedCatIndex][0].nCategory;

            if(iCat!=m_iCategory)
            {
                SetVFMode(0);
                /*
                if (iCat == 4 || iCat == 5)
                    SetVFMode(1);
                else 
                    SetVFMode(0);
                */
            }

            CUIManager.SetCurrentCategory(iCat);
            CUIManager.UpdateUI();

            SharingHelper.m_iSendCategoryToServer = iCat;


        }

        static public void StartUpUI()
        {
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

        }

    }
}
