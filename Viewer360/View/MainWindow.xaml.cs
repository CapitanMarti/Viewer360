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
        private Point vViewfinderCentre;
        private Point vVewfinderBBox;
        private PointCollection aPointTmp;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();

            // Inizializzo centro mirino
            vViewfinderCentre.X = (ViewFinderPolygon.Points[0].X + ViewFinderPolygon.Points[1].X) / 2;
            vViewfinderCentre.Y = (ViewFinderPolygon.Points[1].Y + ViewFinderPolygon.Points[2].Y) / 2;
            vVewfinderBBox.X= (ViewFinderPolygon.Points[1].X - ViewFinderPolygon.Points[0].X) / 2;
            vVewfinderBBox.Y = (ViewFinderPolygon.Points[2].Y - ViewFinderPolygon.Points[1].Y) / 2;

            aPointTmp =new PointCollection();


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
                // Recupero dimensioni finestra
                double dSizeX = viewer360_View.m_ViewSize.Width;
                double dSizeY = viewer360_View.m_ViewSize.Height;

                // Recupero posizione mouse
                Point newPosition = new Point(Mouse.GetPosition(viewer360_View.vp).X, Mouse.GetPosition(viewer360_View.vp).Y);

                // Calcolo nuove posizioni
                aPointTmp.Clear();
                double dX, dY;
                for ( int i=0; i< ViewFinderPolygon.Points.Count; i++)
                {
                    dX = ViewFinderPolygon.Points[i].X + newPosition.X - offset.X;
                    dY = ViewFinderPolygon.Points[i].Y + newPosition.Y - offset.Y;
                    aPointTmp.Add(new Point(dX,dY));
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
                }

                offset = newPosition;

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
                            vP.Y = ViewFinderPolygon.Points[i].Y + fIncrease;
                        else
                            vP.Y = ViewFinderPolygon.Points[i].Y - fIncrease;

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
                            vP.X = ViewFinderPolygon.Points[i].X + fIncrease;
                        else
                            vP.X = ViewFinderPolygon.Points[i].X - fIncrease;

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
                }

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
        }

        public void Polygon_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
        }
    }
}
