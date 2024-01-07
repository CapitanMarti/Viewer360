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
using System.Windows.Media;
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
        static Polygon m_oVFinder;
        static private Point[] m_aOriginalPolygon;

        static List<Ellipse> m_EllipseList;
        static int m_iEllipseIncrementalNum;
        static Canvas myCanvas;
        static private PointCollection m_aPointTmp;
        static private Point vViewfinderCentre;
        static private Point vVewfinderBBox;





        static public void SetCurrentCategory(int iCategory)
        {
            m_iCategory = iCategory;  // 1==Wall,2==Floor,3==Ceiling,4==Window,5==Door,6==PCSection
        }
        static public int GetCurrentCategory()
        {
            return m_iCategory; // 1==Wall,2==Floor,3==Ceiling,4==Window,5==Door,6==PCSection
        }

        static private void AddEllipse(Canvas canvas, double left, double top, int iPointIndex = -1)
        {

            Ellipse ellipse = new Ellipse();
            ellipse.Width = 8;
            ellipse.Height = 8;
            ellipse.Fill = Brushes.Green;
            ellipse.Name = "P" + m_iEllipseIncrementalNum.ToString();
            m_iEllipseIncrementalNum++;

            //ellipse.MouseLeftButtonDown += Cerchio_Click;   // TODO
            //ellipse.MouseLeftButtonUp += Cerchio_BottonUp;
            //ellipse.MouseRightButtonDown += DeleteCerchio_Click;

            Canvas.SetLeft(ellipse, left - 4);
            Canvas.SetTop(ellipse, top - 4);

            canvas.Children.Add(ellipse);

            if (iPointIndex < 0) // Append
                m_EllipseList.Add(ellipse);
            else
                m_EllipseList.Insert(iPointIndex, ellipse);
        
        }

        public static void Init(Polygon oFinder, Grid myGrid)
        {
            m_oVFinder = oFinder;
            m_EllipseList = new List<Ellipse>();
            m_aOriginalPolygon = new Point[m_oVFinder.Points.Count];

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
                (m_Window.DataContext as ViewModel.MainViewModel).RestorePolygons(oLabel);

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
                    m_Window.AIButton.Visibility = Visibility.Visible;
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
                    m_Window.ResetPolygon();  // Resetto il mirino
                }
                m_eMode = ViewerMode.Create;
                m_Window.ViewFinderPolygon.Fill = null;
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
                m_Window.ViewFinderPolygon.Fill = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
            }
            UpdateUI();
        }
    }
}
