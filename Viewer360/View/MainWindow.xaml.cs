using Microsoft.Win32;
using Viewer360.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Windows.Media;

namespace Viewer360.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private bool isDragging;
        private Point offset;
        private Point vViewFinderCentre;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            // Inizializzo centro mirino
            vViewFinderCentre.X = (ViewFinderPolygon.Points[0].X + ViewFinderPolygon.Points[1].X) / 2;
            vViewFinderCentre.Y = (ViewFinderPolygon.Points[1].Y + ViewFinderPolygon.Points[2].Y) / 2;

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

        public void Polygon_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDragging = true;
                offset = new Point(Mouse.GetPosition(viewer360_View.vp).X, Mouse.GetPosition(viewer360_View.vp).Y); ;
            }
        }

        public void Polygon_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && isDragging)
            {
                Point newPosition = new Point(Mouse.GetPosition(viewer360_View.vp).X, Mouse.GetPosition(viewer360_View.vp).Y);
                for( int i=0; i< ViewFinderPolygon.Points.Count; i++)
                {
                    ViewFinderPolygon.Points[i]+=newPosition - offset;
                }
                offset = newPosition;

                // Aggiorno centro mirino
                vViewFinderCentre.X = (ViewFinderPolygon.Points[0].X + ViewFinderPolygon.Points[1].X) / 2;
                vViewFinderCentre.Y = (ViewFinderPolygon.Points[1].Y + ViewFinderPolygon.Points[2].Y) / 2;
            }
        }

        public void Polygon_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ( !isDragging)
            {
                float fIncrease=1+(float)(e.Delta) / 1000;

                double fDX0 = ViewFinderPolygon.Points[1].X - ViewFinderPolygon.Points[0].X;

                for (int i = 0; i < ViewFinderPolygon.Points.Count; i++)
                {
                    ViewFinderPolygon.Points[i] = vViewFinderCentre+ (ViewFinderPolygon.Points[i] - vViewFinderCentre)*fIncrease;
                }
                double fDX1 = ViewFinderPolygon.Points[1].X - ViewFinderPolygon.Points[0].X;
            }
        }


        public void Polygon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }
    }
}
