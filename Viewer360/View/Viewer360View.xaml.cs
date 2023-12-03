using Viewer360.Model;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Drawing;
using Viewer360.View;
using System.Reflection.Emit;
using System.Xml.Linq;
using System.Globalization;

namespace Viewer360.View
{
    /// <summary>
    /// Class for viewing an equirectangular 360° view by projecting it on the
    /// inner surface of a sphere
    /// </summary>
    public partial class Viewer360View : UserControl, INotifyPropertyChanged
    {
        private MeshGeometry3D sphereMesh = null; // Tessellated sphere mesh
        private ImageBrush brush = null;          // Brush containing the 360° view
        private double camTheta = 180;            // Camera horizontal orientation
        private double camPhi = 90;               // Camera vertical orientation
        private double camThetaSpeed = 0;         // Camera horizontal movement speed
        private double camPhiSpeed = 0;           // Camera vertical movement speed
        private double clickX, clickY;            // Coordinates of the mouse press
        private DispatcherTimer timer;            // Timer for animating camera
        private bool isMouseDown = false;         // Is the mouse pressed

        public System.Windows.Size m_ViewSize;
        public View.MainWindow m_Window;

        /// <summary>
        /// Camera horizontal FOV
        /// </summary>
        public double Hfov { get { return MyCam.FieldOfView; } }

        /// <summary>
        /// Camera vertical FOV
        /// </summary>
        public double Vfov { get { return 2 * Math.Atan(ActualHeight / ActualWidth * Math.Tan(MyCam.FieldOfView * Math.PI / 180 / 2)) * 180 / Math.PI; } }

        /// <summary>
        /// Camera horizontal orientation
        /// </summary>
        public double Theta { get { return camTheta; } }

        /// <summary>
        /// Camera vertical orientation
        /// </summary>
        public double Phi { get { return camPhi; } }

        /// <summary>
        /// Constructor
        /// </summary>
        public Viewer360View()
        {
            InitializeComponent();
//            sphereMesh = GeometryHelper.CreateSphereMesh(40, 20, 10); // Initialize mesh 
            sphereMesh = GeometryHelper.CreateSphereMesh(80, 80, 10); // Initialize mesh 

            brush = new ImageBrush(); // Initialize brush with no image
            brush.TileMode = TileMode.Tile;
            
            timer = new DispatcherTimer(); // Initialize timer
            timer.Interval = TimeSpan.FromMilliseconds(25);
            timer.Tick += timer_Tick;

            m_ViewSize = new System.Windows.Size();
        }

        /// <summary>
        /// Dependency property for the 360° view
        /// </summary>
        public static readonly DependencyProperty ImageProperty =
            DependencyProperty.Register("Image", typeof(BitmapImage), typeof(Viewer360View),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, ImageChangedCallback));

        // Callback for image changed
        private static void ImageChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Viewer360View)d).ImageChanged();
        }

        /// <summary>
        /// 360° view
        /// </summary>
        public BitmapImage Image
        {
            get { return (BitmapImage)GetValue(ImageProperty); }
            set { SetValue(ImageProperty, value); }
        }

        // Image changed
        private void ImageChanged()
        {
            MyModel.Children.Clear();
            brush.ImageSource = Image;

            ModelVisual3D sphereModel = new ModelVisual3D();
            sphereModel.Content = new GeometryModel3D(sphereMesh, new DiffuseMaterial(brush));
            MyModel.Children.Add(sphereModel);
            
            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }

        // Timer: animate camera
        private void timer_Tick(object sender, EventArgs e)
        {
            if (!isMouseDown) return;
            camTheta -= camThetaSpeed / 50;
            camPhi -= camPhiSpeed / 50;

            if (camTheta < 0) camTheta += 360;
            else if (camTheta > 360) camTheta -= 360;

/*
            if (camPhi < 0.01) 
                camPhi = 0.01;
            else if (camPhi > 179.99) 
                camPhi = 179.99;
*/
            if (camPhi < 50)
                camPhi = 50;
            else if (camPhi > 120)
                camPhi = 120;

            MyCam.LookDirection = GeometryHelper.GetNormal(
                GeometryHelper.Deg2Rad(camTheta),
                GeometryHelper.Deg2Rad(camPhi));

            RaisePropertyChanged("Theta");
            RaisePropertyChanged("Phi");
        }

        // Mouse move: set camera movement speed
        private void vp_MouseMove(object sender, MouseEventArgs e)
        {

            if (Keyboard.IsKeyDown(Key.LeftCtrl))
                m_Window.Polygon_MouseMove(sender, e);
            else
            {
                if (!isMouseDown) return;
                camThetaSpeed = Mouse.GetPosition(vp).X - clickX;
                camPhiSpeed = Mouse.GetPosition(vp).Y - clickY;
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // Scalo anche il mirino
            m_Window.RescaleViewfinderOnWindowChange(sizeInfo);

            m_ViewSize = sizeInfo.NewSize;
            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }

        private void vp_MouseRightButtonDown(object sender, MouseEventArgs e)
        // ********************************
        // AM AM AM  Scrittura grab video su file
        //********************************
        {
            int nWidth = Convert.ToInt32(m_ViewSize.Width * 1.5);
            int nHeight = Convert.ToInt32(m_ViewSize.Height * 1.5);
            var renderTarget = new RenderTargetBitmap(nWidth, nHeight, 144, 144, PixelFormats.Default);
//            var renderTarget = new RenderTargetBitmap(Convert.ToInt32(m_ViewSize.Width * 3), Convert.ToInt32(m_ViewSize.Height * 3), 144, 144, PixelFormats.Pbgra32);
            renderTarget.Render(vp);
    
            //yield return renderTarget;

            MemoryStream stream = new MemoryStream();
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            encoder.Save(stream);

            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(stream);

            string sNewFileName = SharingHelper.GetNewFileName();
            bitmap.Save(sNewFileName);
            SaveCameraInfo(sNewFileName);
        }

        public void SaveCameraInfo(string CameraInfoFileName)
        {
            int nWidth = Convert.ToInt32(m_ViewSize.Width * 1.5);
            int nHeight = Convert.ToInt32(m_ViewSize.Height * 1.5);
            // vp.Camera.Transform
            // vp.Camera.LookDirection
            double vFov = Vfov;
            double hFov = Hfov;


            string sImageName=Path.GetFileName(CameraInfoFileName);
            string sCIFName = SharingHelper.GetNewPath()+"\\"+ Path.GetFileNameWithoutExtension(CameraInfoFileName)+".cif";

            FileStream fs = null;
            try
            {
                fs = new FileStream(sCIFName, FileMode.Create);
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    sw.WriteLine("<?xml version=\"1.0\"?>");
                    sw.WriteLine("<chunk label = \"Chunk 1\" enabled = \"true\" >");
                    sw.WriteLine("<sensors>");
                    sw.WriteLine("  <sensor id=\"0\" label=\"Made By Viewer360View\" type=\"frame\">");
                    sw.WriteLine("      <property name = \"pixel_width\" value = \"0.004\" />");
                    sw.WriteLine("      <property name = \"pixel_height\" value = \"0.004\" />");
                    sw.WriteLine("      <resolution width = \"" + Convert.ToString(nWidth) + "\" height=\"" + Convert.ToString(nHeight) + "\" />");
                    sw.WriteLine("      <property name = \"fov_x\" value = \"" + Convert.ToString(Hfov, CultureInfo.InvariantCulture) + "\" />");
                    sw.WriteLine("      <property name = \"fov_y\" value = \"" + Convert.ToString(Vfov, CultureInfo.InvariantCulture) + "\" />");
                    sw.WriteLine("  </sensor>");
                    sw.WriteLine("</sensors>");
                    sw.WriteLine("<cameras>");
                    sw.WriteLine("  <camera id=\"0\" label=\"" + sImageName + "\" sensor_id=\"0\" enabled=\"true\">");


                    // Calcolo direzioni At,Up di camera locale, tenendo conto che in questo codice risulta sempre Up=[0,0,1]
                    Vector3D vAt= MyCam.LookDirection;
                    vAt.Z = -vAt.Z;  // ATTENZIONE: Z va invertita per compatibilità sistema riferimento adottato da Scan2Bim
                    Vector3D vTmp = Vector3D.CrossProduct(vAt, MyCam.UpDirection);  // Vetture trasversale
                    vTmp.Normalize();
                    Vector3D vRealUp = Vector3D.CrossProduct(vTmp, vAt);

                    // Recupero posizione e rotazioni globali
                    Vector3D vCameraPos = SharingHelper.GetCameraPos();
                    Vector3D vCameraRot = SharingHelper.GetCameraRot();
       
                    // Matrice rotazione locale camera: [ Up | Up X At | At] 
                    Matrix3D mCameraRot =new Matrix3D();
                    mCameraRot.M11 = vRealUp.X;
                    mCameraRot.M12 = vTmp.X;
                    mCameraRot.M13 = vAt.X;
                    mCameraRot.M14 = 0;
                    mCameraRot.M21 = vRealUp.Y;
                    mCameraRot.M22 = vTmp.Y;
                    mCameraRot.M23 = vAt.Y;
                    mCameraRot.M24 = 0;
                    mCameraRot.M31 = vRealUp.Z;
                    mCameraRot.M32 = vTmp.Z;
                    mCameraRot.M33 = vAt.Z;
                    mCameraRot.M34 = 0;
                    mCameraRot.M44 = 1;

                    // Calcolo matrici rotazione angoli di Eulero XYZ
                    double dRotX = -GeometryHelper.Deg2Rad(vCameraRot.X);
                    Matrix3D mRotX = new Matrix3D();
                    mRotX.M11=1;
                    mRotX.M12 = 0;
                    mRotX.M13=0;
                    mRotX.M14=0;
                    mRotX.M21 = 0;
                    mRotX.M22 = Math.Cos(dRotX);
                    mRotX.M23 = Math.Sin(dRotX);
                    mRotX.M24 = 0;
                    mRotX.M31 = 0;
                    mRotX.M32 = -Math.Sin(dRotX);
                    mRotX.M33 = Math.Cos(dRotX);
                    mRotX.M34 = 0;
                    mRotX.M44 = 1;


                    double dRotY = GeometryHelper.Deg2Rad(vCameraRot.Y);
                    Matrix3D mRotY = new Matrix3D();
                    mRotY.M11 = Math.Cos(dRotY);
                    mRotY.M12 = 0;
                    mRotY.M13 = Math.Sin(dRotY);
                    mRotY.M14 = 0;
                    mRotY.M21 = 0;
                    mRotY.M22 = 1;
                    mRotY.M23 = 0;
                    mRotY.M24 = 0;
                    mRotY.M31 = -Math.Sin(dRotY);
                    mRotY.M32 = 0;
                    mRotY.M33 = Math.Cos(dRotY);
                    mRotY.M34 = 0;
                    mRotY.M44 = 1;

                    double dRotZ = -GeometryHelper.Deg2Rad(vCameraRot.Z) + Math.PI / 2;  // OFFSET ANGOLARE PER COMPATIBILITA' TRIMBLE!
//                    double dRotZ = -GeometryHelper.Deg2Rad(vCameraRot.Z);                //
                    Matrix3D mRotZ = new Matrix3D();
                    mRotZ.M11 = Math.Cos(dRotZ);
                    mRotZ.M12 = Math.Sin(dRotZ);
                    mRotZ.M13 = 0;
                    mRotZ.M14 = 0;
                    mRotZ.M21 = -Math.Sin(dRotZ);
                    mRotZ.M22 = Math.Cos(dRotZ);
                    mRotZ.M23 = 0;
                    mRotZ.M24 = 0;
                    mRotZ.M31 = 0;
                    mRotZ.M32 = 0;
                    mRotZ.M33 = 1;
                    mRotZ.M34 = 0;
                    mRotZ.M44 = 1;



                    mCameraRot = mRotX*mRotY*mRotZ*mCameraRot;

                    mCameraRot.M14 = vCameraPos.X;
                    mCameraRot.M24 = vCameraPos.Y;
                    mCameraRot.M34 = vCameraPos.Z;

 

                    string s1 = mCameraRot.M11.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M12.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M13.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M14.ToString(CultureInfo.InvariantCulture) + " ";
                    string s2 = mCameraRot.M21.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M22.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M23.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M24.ToString(CultureInfo.InvariantCulture) + " ";
                    string s3 = mCameraRot.M31.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M32.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M33.ToString(CultureInfo.InvariantCulture) + " " + mCameraRot.M34.ToString(CultureInfo.InvariantCulture) + " ";

                    /*
                                        string s1 = vRealUp.X.ToString(CultureInfo.InvariantCulture) + " " + vTmp.X.ToString(CultureInfo.InvariantCulture) + " " + MyCam.LookDirection.X.ToString(CultureInfo.InvariantCulture) +" "+ vCameraPos.X.ToString(CultureInfo.InvariantCulture) +" ";
                                        string s2 = vRealUp.Y.ToString(CultureInfo.InvariantCulture) + " " + vTmp.Y.ToString(CultureInfo.InvariantCulture) + " " + MyCam.LookDirection.Y.ToString(CultureInfo.InvariantCulture) +" "+ vCameraPos.Y.ToString(CultureInfo.InvariantCulture) + " ";
                                        string s3 = vRealUp.Z.ToString(CultureInfo.InvariantCulture) + " " + vTmp.Z.ToString(CultureInfo.InvariantCulture) + " " + MyCam.LookDirection.Z.ToString(CultureInfo.InvariantCulture) +" "+ vCameraPos.Z.ToString(CultureInfo.InvariantCulture) + " ";*/
                    sw.WriteLine("      <transform>" + s1+s2+s3+" 0 0 0 1 </transform>");
                    sw.WriteLine("      <orientation>1</orientation>");
                    sw.WriteLine("  </camera>");
                    sw.WriteLine("</cameras>");
                    sw.WriteLine("</chunk>");

                }
            }
            finally
            {
                if (fs != null)
                    fs.Dispose();
            }



        }

        // Mouse down: start moving camera
        private void vp_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
                m_Window.Polygon_MouseDown(sender, e);
            else
            {
                isMouseDown = true;
                this.Cursor = Cursors.SizeAll;
                clickX = Mouse.GetPosition(vp).X;
                clickY = Mouse.GetPosition(vp).Y;
                camThetaSpeed = camPhiSpeed = 0;
                timer.Start();
            }
        }


        // Mouse up: stop moving camera
        private void vp_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
                m_Window.Polygon_MouseUp(sender, e);
            else
            {
                isMouseDown = false;
                this.Cursor = Cursors.Arrow;
                timer.Stop();
            }
        }

        // Mouse wheel: zoom
        private void vp_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
                m_Window.Polygon_MouseWheel(sender, e);
            else
            {
                MyCam.FieldOfView -= e.Delta / 100;
                if (MyCam.FieldOfView < 1) MyCam.FieldOfView = 1;
                else if (MyCam.FieldOfView > 140) MyCam.FieldOfView = 140;

                RaisePropertyChanged("Hfov");
                RaisePropertyChanged("Vfov");
            }
        }
        
/*
        // Size changed: notify FOV change
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            m_ViewSize = sizeInfo.NewSize;
            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }
*/
        // Helper function for INPC
        private void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Property changed event
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
