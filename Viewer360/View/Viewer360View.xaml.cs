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
using System.Windows.Interop;
using System.Diagnostics;
using static Viewer360.View.CUIManager;
using System.Windows.Forms;
using static PointCloudUtility.CommonUtil;


namespace Viewer360.View
{
    /// <summary>
    /// Class for viewing an equirectangular 360° view by projecting it on the
    /// inner surface of a sphere
    /// </summary>
    public partial class Viewer360View : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        public enum ViewerProjection
        {
            Planar,
            Spheric
        }
        //*************************************
        private ViewerProjection m_Projection= ViewerProjection.Planar;
        //*************************************
        private MeshGeometry3D sphereMesh = null; // Tessellated sphere mesh
        private MeshGeometry3D planeMesh = null; // Tessellated sphere mesh
        private ImageBrush brush = null;          // Brush containing the 360° view
        private double camTheta = 180;            // Camera horizontal orientation
        private double camPhi = 90;               // Camera vertical orientation
        private double camThetaSpeed = 0;         // Camera horizontal movement speed
        private double camPhiSpeed = 0;           // Camera vertical movement speed
        private double clickX, clickY;            // Coordinates of the mouse press
        private DispatcherTimer timer;            // Timer for animating camera
        private DispatcherTimer ClickTimer;       // Timer per animazione mirino
        private Stopwatch ClickTimerWatch;
        private bool isMouseDown = false;         // Is the mouse pressed
        private Matrix3D m_mRotX;
        private Matrix3D m_mRotY;
        private Matrix3D m_mRotZ;
        private Matrix3D m_mRotXYZ;
        private Matrix3D m_mInvRotXYZ;
        public PerspectiveCamera MyCam;
        public OrthographicCamera MyOrthoCam;


        public System.Windows.Size m_ViewSize;
        public View.MainWindow m_Window;

        public Matrix3D GetWorldMatrix()
        {
            return m_mRotXYZ;
        }
        public ViewerProjection GetProjection() { return m_Projection; }
        public void SetProjection(ViewerProjection eProg) {m_Projection= eProg; }

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

        public void InitCamera()
        {
            timer = new DispatcherTimer(); // Initialize timer
            timer.Interval = TimeSpan.FromMilliseconds(25);

            if (m_Projection == ViewerProjection.Planar)
            {
                timer.Tick += timerPlane_Tick;

                MyOrthoCam = new OrthographicCamera
                {
                    //                    Width = 3,
                    Position = new Point3D(0, 0, 1),
                    LookDirection = new Vector3D(0, 0, -1),
                    UpDirection = new Vector3D(0, 1, 0)
                };
                vp.Camera = MyOrthoCam;
                SharingHelper.m_dConvFactor = 1.5;
            }
            else
            {
                timer.Tick += timerSpheric_Tick;

                MyCam = new PerspectiveCamera
                {
                    Position = new Point3D(0, 0, 0),
                    LookDirection = new Vector3D(0, -1, 0),
                    UpDirection = new Vector3D(0, 0, 1),
                    FieldOfView = 120
                };
                vp.Camera = MyCam;
                SharingHelper.m_dConvFactor = 1.5;
            }


        }

        /// <summary>
        /// Constructor
        /// </summary>
        public Viewer360View()
        {


            InitializeComponent();

            brush = new ImageBrush(); // Initialize brush with no image
            brush.TileMode = TileMode.Tile;
            

            ClickTimer = new DispatcherTimer(); // Initialize timer
            ClickTimer.Interval = TimeSpan.FromMilliseconds(10);
            ClickTimer.Tick += ClickTimer_Tick;
            ClickTimerWatch = new Stopwatch();

            m_ViewSize = new System.Windows.Size();

            m_mRotX = new Matrix3D();
            m_mRotY = new Matrix3D();
            m_mRotZ = new Matrix3D();
            m_mRotXYZ = new Matrix3D();
            m_mInvRotXYZ= new Matrix3D();
        }

        private void ClickTimer_Tick(object sender, EventArgs e)
        {
            if (!ClickTimerWatch.IsRunning)
            {
                ClickTimerWatch.Reset();
                ClickTimerWatch.Start();
            }

            TimeSpan elapsed = ClickTimerWatch.Elapsed;
            //++++++++++++++++++++++++++++
            //Console.WriteLine("Da ClickTimer_Tick elapsed="+ elapsed.Milliseconds.ToString());
            //++++++++++++++++++++++++++++

            if (elapsed.Milliseconds<100)
                 m_Window.ViewFinderPolygon.Stroke = System.Windows.Media.Brushes.White;
            else if(elapsed.Milliseconds<200)
                m_Window.ViewFinderPolygon.Stroke = System.Windows.Media.Brushes.Red;
            else if (elapsed.Milliseconds < 400)
                m_Window.ViewFinderPolygon.Stroke = System.Windows.Media.Brushes.White;
            else
            {
                m_Window.ViewFinderPolygon.Stroke = System.Windows.Media.Brushes.Blue;
                ClickTimerWatch.Stop();
                ClickTimer.Stop();
            }
//            m_Window.InvalidateVisual();
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

        void SetViewAndWindowSize()
        {
            if (Image.Width > Image.Height)  // Foto orizzontale
            {

                double dWinWidth = 880* SharingHelper.m_dPlanarZoomFactor;
                double dVpWidth = dWinWidth - 81 - 14;
                double dVpHeight = dVpWidth * Image.Height / Image.Width;
                double dWinHeight = dVpHeight + 60 + 7;

                m_Window.Width = dWinWidth;
                m_Window.Height = dWinHeight;
                m_Window.viewer360_View.vp.Width = dVpWidth;
                m_Window.viewer360_View.vp.Height = dVpHeight;

                /*
                m_Window.Width = 880;
                m_Window.viewer360_View.vp.Height = (vp.RenderSize.Width * Image.Height / Image.Width); // Imposto l'altezza di viewport in base ad aspect ratio
                m_Window.Height = m_Window.viewer360_View.vp.Height + 60;  // 60 è lo spessore delle bande in alto
                */
            }
            else
            {
                double dWinWidth = Math.Max(800 * Image.Width / Image.Height, 500)* SharingHelper.m_dPlanarZoomFactor;
                double dVpWidth = dWinWidth - 81 - 14;
                double dVpHeight = dVpWidth * Image.Height / Image.Width;
                double dWinHeight = dVpHeight + 60 + 7;

                m_Window.Width = dWinWidth;
                m_Window.Height = dWinHeight;
                m_Window.viewer360_View.vp.Width = dVpWidth;
                m_Window.viewer360_View.vp.Height = dVpHeight;

            }
        }

        // Image changed
        private void ImageChanged()
        {
            MyModel.Children.Clear();
            brush.ImageSource = Image;


            if (m_Projection == ViewerProjection.Planar)
            {
                double dSizeX = Image.PixelWidth;
                double dSizeY = Image.PixelHeight;

                planeMesh = GeometryHelper.CreatePlaneMesh(dSizeX, dSizeY); // Initialize mesh 
                sphereMesh = null;

                // Adatta l'aspetto di finestra e view a foto
                SetViewAndWindowSize();
            }
            else
            {
                planeMesh = null;
                sphereMesh = GeometryHelper.CreateSphereMesh(40, 20, 10); // Initialize mesh 
            }


            ModelVisual3D oModel = new ModelVisual3D();
            if (m_Projection == ViewerProjection.Planar)
                oModel.Content = new GeometryModel3D(planeMesh, new DiffuseMaterial(brush));
            else
                oModel.Content = new GeometryModel3D(sphereMesh, new DiffuseMaterial(brush));

            MyModel.Children.Add(oModel);
            
            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }

        // Timer: animate camera

        private void timerPlane_Tick(object sender, EventArgs e)
        {
            // TODO
        }

        private void timerSpheric_Tick(object sender, EventArgs e)
        {
            if (!isMouseDown) 
                return;

            camTheta -= camThetaSpeed / 50;
            camPhi -= camPhiSpeed / 50;
            SharingHelper.m_bCameraAtHasChanged = true;

            if (camTheta < 0) 
                camTheta += 360;
            else if (camTheta > 360) 
                camTheta -= 360;

            if (camPhi < 50)
                camPhi = 50;
            else if (camPhi > 120)
                camPhi = 120;

            MyCam.LookDirection = GeometryHelper.GetNormal(
                GeometryHelper.Deg2Rad(camTheta),
                GeometryHelper.Deg2Rad(camPhi));

            RaisePropertyChanged("Theta");
            RaisePropertyChanged("Phi");

            ComputeGlobalRotMatrix();

            if(CProjectPlane.m_bPlaneDefined && CUIManager.GetMode()==ViewerMode.Edit)
            {
                CUIManager.UpdateViewPolygonFromFace3D();
            }
        }
/*
        static double newWidth = 2;
        static double dxOffset = 0;
        static double dyOffset = 0;
        static double dScale = 1;
*/
        // Mouse move: set camera movement speed
        public void vp_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {

            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                CUIManager.Polygon_MouseMove(e);
                //            m_Window.Polygon_MouseMove(sender, e);
            }
            else  // Rotazione/traslazione camera
            {
                if (!isMouseDown)
                    return;

                if (m_Projection == ViewerProjection.Spheric)  // Caso sferico--> calcolo velocità di rotazione; nel caso planare NON FACCIO NULLA
                {
                    camThetaSpeed = Mouse.GetPosition(vp).X - clickX;
                    camPhiSpeed = Mouse.GetPosition(vp).Y - clickY;
                }
                /*
                else // Caso planare --> Non faccio nulla
                {
                    double dNewPosX= Mouse.GetPosition(vp).X;
                    double dNewPosY = Mouse.GetPosition(vp).Y;
                    double dShiftX = dNewPosX - clickX;
                    double dShiftY = dNewPosY - clickY;
                    //+++++++++++++++++++++++++++
                    Console.WriteLine("dNewPosX=" + dNewPosX + " dShiftX=" + dShiftX + " dxOffset=" + dxOffset + " dNewPosY=" + dNewPosY  + " dShiftY=" + dShiftY + " dyOffset=" + dyOffset);
                    //+++++++++++++++++++++++++++
                    dxOffset -= dShiftX* dScale / Image.PixelWidth;
                    dyOffset += dShiftY* dScale / Image.PixelHeight;


                    MyOrthoCam.Position = new Point3D(dxOffset, dyOffset, 1);
                    MyOrthoCam.Width = newWidth;

                    clickX = dNewPosX;
                    clickY = dNewPosY;
                }
                */

            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            // Scalo anche il mirino
            //m_Window.RescaleViewfinderOnWindowChange(sizeInfo);
            CUIManager.RescaleViewfinderOnWindowChange(sizeInfo);

            m_ViewSize = sizeInfo.NewSize;

            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }

        private void vp_MouseRightButtonDown(object sender, System.Windows.Input.MouseEventArgs e)
        // ********************************
        // AM AM AM  Scrittura grab video su file
        //********************************
        {
            if (Keyboard.IsKeyDown(Key.LeftAlt))
                SaveImageAndJson();

        }

        public void SaveImageAndJson()
        {

            string sNewJsonFileName;
            if (CUIManager.GetMode() == ViewerMode.Create)  // Chiedo filename all'Helper
                sNewJsonFileName = SharingHelper.GetUniqueFileName(".json");
            else
                sNewJsonFileName = (m_Window.DataContext as ViewModel.MainViewModel).m_sCurrentLabelFileName;

            PointCloudUtility.CSingleFileLabel oLabelCandidate;
            if (m_Projection == ViewerProjection.Spheric)
                oLabelCandidate = m_Window.BuildSavingLabelCandidate(sNewJsonFileName, m_ViewSize, camTheta, camPhi, Vfov, Hfov, MyCam.LookDirection);
            else
            {
                System.Windows.Size vViewSize = new System.Windows.Size(m_ViewSize.Width, m_ViewSize.Height);  // Ripristino il size della foto originale 
                System.Windows.Size vImageSize = new System.Windows.Size(Image.PixelWidth, Image.PixelHeight);  // Ripristino il size della foto originale 

                oLabelCandidate = m_Window.BuildSavingLabelCandidatePlanar(sNewJsonFileName, vViewSize, vImageSize);  // TODO aggiungere dati associati a .mlb
            }
            PointCloudUtility.CSingleFileLabel oOldLabel = (m_Window.DataContext as ViewModel.MainViewModel).m_oCurrentLabel;


            // Verifico se il nome della label è cambiato e sono in modalità Create
            if (CUIManager.GetMode() != ViewerMode.Create && oOldLabel != null && oLabelCandidate.m_aLabelInfo[0].sLabelName != oOldLabel.m_aLabelInfo[0].sLabelName)
            {
                DialogResult result = ConfirmDialog("Il nome della label corrente è stato cambiato; le eventuali entità derivate da essa, con il vecchio nome, saranno eliminate.\n\n\nConfermi?");
                if (result == System.Windows.Forms.DialogResult.No)
                    return;

                string sJsonFileName = System.IO.Path.GetFileNameWithoutExtension(oOldLabel.m_sJsonFileName);
                try
                {
                    var files = Directory.GetFiles(SharingHelper.GetJsonPath(), sJsonFileName + "*.*");
                    foreach (string file in files)
                    {
                        System.IO.File.SetAttributes(file, FileAttributes.Normal);
                        System.IO.File.Delete(file);

                    }

                    // Cancello tutti i file che contengono XXXXXXXX##YY in SegmentedPC
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
            }

            /*  Sospendiamo il salvataggio della immagine associata al json --> NON SERVE PIU'!!
            // Genero immagine partendo da render target
            int nWidth;
            int nHeight;
            double dOffset = 0;
            if (m_Projection == ViewerProjection.Spheric)
            {
                nWidth = Convert.ToInt32(m_ViewSize.Width * SharingHelper.m_dConvFactor);
                nHeight = Convert.ToInt32(m_ViewSize.Height * SharingHelper.m_dConvFactor);
            }
            else
            {
                double dSizeCorrected = vp.RenderSize.Width * Image.Height / Image.Width;
                dOffset = SharingHelper.m_dConvFactor*(vp.RenderSize.Height - dSizeCorrected) / 2;

//                nHeight =(int)((vp.RenderSize.Width + dSizeCorrected) * Image.Height * SharingHelper.m_dConvFactor / Image.Width);
                nHeight = (int)((vp.RenderSize.Width + dOffset) * Image.Height * SharingHelper.m_dConvFactor / Image.Width);
                nWidth = Convert.ToInt32(m_ViewSize.Width * SharingHelper.m_dConvFactor);
            }
            var renderTarget = new RenderTargetBitmap(nWidth, nHeight, 144, 144, PixelFormats.Default);
            //            var renderTarget = new RenderTargetBitmap(Convert.ToInt32(m_ViewSize.Width * 3), Convert.ToInt32(m_ViewSize.Height * 3), 144, 144, PixelFormats.Pbgra32);
            renderTarget.Render(vp);

            MemoryStream stream = new MemoryStream();
            BitmapEncoder encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            encoder.Save(stream);
            System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(stream);

            // Salvo la bitmap
            //bitmap.Save(sNewFileName);

            //++++++++++++++++++++++++++++++++++++++++++++++
            // Specifica il numero di righe da escludere dall'alto
            //int righeDaEscludere = (int)(dOffset);

            // Calcola la nuova altezza dell'immagine dopo l'esclusione delle righe
            //int nuovaAltezza = bitmap.Height - righeDaEscludere;

            // Crea una nuova immagine escludendo le prime n righe dall'alto
            //System.Drawing.Bitmap nuovaBitmap = bitmap.Clone(new Rectangle(0, righeDaEscludere, bitmap.Width, nuovaAltezza), bitmap.PixelFormat);

            // Ora hai una nuova immagine che esclude le prime n righe dall'alto
            // Puoi fare ulteriori operazioni o salvarla su file come desiderato
            //nuovaBitmap.Save(sNewFileName);
            //+++++++++++++++++++++++++++++++++++++++++++++++
*/

            // Salvo il file .CIF
            if (m_Projection==ViewerProjection.Spheric)
                SaveCameraInfo(sNewJsonFileName);

            // Scrittura file .json
            //            m_Window.SaveJason(sNewJsonFileName, m_ViewSize, camTheta, camPhi, Vfov, Hfov, MyCam.LookDirection);
            int iPhotoIndex = -1;
            int iLabelIndex = -1;

            m_Window.SaveJason(oLabelCandidate, sNewJsonFileName, ref iPhotoIndex, ref iLabelIndex);

            // Aggiornare la label corrente
            (m_Window.DataContext as ViewModel.MainViewModel).m_oCurrentLabel = oLabelCandidate;

            SharingHelper.m_bLabelAdded = true;

            // Faccio partire il timer per l'animazione del mirino
            ClickTimer.Start();
        }

        public void SaveMlb()
        {

            string sNewJsonFileName;
            if (CUIManager.GetMode() == ViewerMode.Create)  // Chiedo filename all'Helper
                sNewJsonFileName = SharingHelper.GetUniqueFileName(".mlb");
            else
                sNewJsonFileName = (m_Window.DataContext as ViewModel.MainViewModel).m_sCurrentLabelFileName;

            PointCloudUtility.CSingleFileLabel oLabelCandidate;
            if (m_Projection == ViewerProjection.Spheric)
                oLabelCandidate = m_Window.BuildSavingLabelCandidate(sNewJsonFileName, m_ViewSize, camTheta, camPhi, Vfov, Hfov, MyCam.LookDirection);
            else
            {
                System.Windows.Size vViewSize = new System.Windows.Size(m_ViewSize.Width, m_ViewSize.Height);  // Ripristino il size della foto originale 
                System.Windows.Size vImageSize = new System.Windows.Size(Image.PixelWidth, Image.PixelHeight);  // Ripristino il size della foto originale 

                oLabelCandidate = m_Window.BuildSavingLabelCandidatePlanar(sNewJsonFileName, vViewSize, vImageSize);   // TODO aggiungere dati associati a .mlb
            }

            PointCloudUtility.CSingleFileLabel oOldLabel = (m_Window.DataContext as ViewModel.MainViewModel).m_oCurrentLabel;


            // Verifico se il nome della label è cambiato e sono in modalità Create
            if (CUIManager.GetMode() != ViewerMode.Create && oOldLabel != null && oLabelCandidate.m_aLabelInfo[0].sLabelName != oOldLabel.m_aLabelInfo[0].sLabelName)
            {
                DialogResult result = ConfirmDialog("Il nome della label corrente è stato cambiato; le eventuali entità derivate da essa, con il vecchio nome, saranno eliminate.\n\n\nConfermi?");
                if (result == System.Windows.Forms.DialogResult.No)
                    return;

                string sJsonFileName = System.IO.Path.GetFileNameWithoutExtension(oOldLabel.m_sJsonFileName);
                try
                {
                    var files = Directory.GetFiles(SharingHelper.GetJsonPath(), sJsonFileName + "*.*");
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
            }

            // Scrittura file .mlb
            int iPhotoIndex = -1;
            int iLabelIndex = -1;
            m_Window.SaveMlb(oLabelCandidate, sNewJsonFileName, ref iPhotoIndex, ref iLabelIndex);

            // Aggiornare la label corrente
            (m_Window.DataContext as ViewModel.MainViewModel).m_oCurrentLabel = oLabelCandidate;
            (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentPhotoIndex=iPhotoIndex;
            (m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentLabelIndex = iLabelIndex;
            PointCloudUtility.CLabelManager.SelectCameraByIndex(iPhotoIndex, iLabelIndex);

            SharingHelper.CNewMsgInfo1 oNewMsg= new SharingHelper.CNewMsgInfo1();
            oNewMsg.m_sLabel = oLabelCandidate;
            oNewMsg.sNewJsonFileName = sNewJsonFileName;
            SharingHelper.m_oMsgInfo1 = oNewMsg;  // Questo attiverà l'invio del messaggio al server

//            SharingHelper.m_bLabelAdded = true;
        }


        public void ComputeGlobalRotMatrix()
        {
            // Recupero rotazioni globali
            Vector3D vCameraRot = SharingHelper.GetCameraRot();

            // Calcolo matrici rotazione angoli di Eulero XYZ
            double dRotX = -GeometryHelper.Deg2Rad(vCameraRot.X);
            m_mRotX.M11 = 1;
            m_mRotX.M12 = 0;
            m_mRotX.M13 = 0;
            m_mRotX.M14 = 0;
            m_mRotX.M21 = 0;
            m_mRotX.M22 = Math.Cos(dRotX);
            m_mRotX.M23 = Math.Sin(dRotX);
            m_mRotX.M24 = 0;
            m_mRotX.M31 = 0;
            m_mRotX.M32 = -Math.Sin(dRotX);
            m_mRotX.M33 = Math.Cos(dRotX);
            m_mRotX.M34 = 0;
            m_mRotX.M44 = 1;


            double dRotY = GeometryHelper.Deg2Rad(vCameraRot.Y);
            m_mRotY.M11 = Math.Cos(dRotY);
            m_mRotY.M12 = 0;
            m_mRotY.M13 = Math.Sin(dRotY);
            m_mRotY.M14 = 0;
            m_mRotY.M21 = 0;
            m_mRotY.M22 = 1;
            m_mRotY.M23 = 0;
            m_mRotY.M24 = 0;
            m_mRotY.M31 = -Math.Sin(dRotY);
            m_mRotY.M32 = 0;
            m_mRotY.M33 = Math.Cos(dRotY);
            m_mRotY.M34 = 0;
            m_mRotY.M44 = 1;

            double dRotZ = -GeometryHelper.Deg2Rad(vCameraRot.Z) + Math.PI / 2;  // OFFSET ANGOLARE PER COMPATIBILITA' TRIMBLE!
            m_mRotZ.M11 = Math.Cos(dRotZ);
            m_mRotZ.M12 = Math.Sin(dRotZ);
            m_mRotZ.M13 = 0;
            m_mRotZ.M14 = 0;
            m_mRotZ.M21 = -Math.Sin(dRotZ);
            m_mRotZ.M22 = Math.Cos(dRotZ);
            m_mRotZ.M23 = 0;
            m_mRotZ.M24 = 0;
            m_mRotZ.M31 = 0;
            m_mRotZ.M32 = 0;
            m_mRotZ.M33 = 1;
            m_mRotZ.M34 = 0;
            m_mRotZ.M44 = 1;

            m_mRotXYZ = m_mRotX * m_mRotY * m_mRotZ ;
            m_mInvRotXYZ= m_mRotX * m_mRotY * m_mRotZ;
            m_mInvRotXYZ.Invert();
        }

        public Vector3D PointGlob2Loc(Vector3D vPoint)
        {
            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            Vector3D vTmp = new Vector3D(vPoint.X - vCameraPos.X, vPoint.Y - vCameraPos.Y, vPoint.Z - vCameraPos.Z);

            return m_mRotXYZ.Transform(vTmp);
        }
        public Point3D PointGlob2Loc(Point3D vPoint)
        {
            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            Point3D vTmp = new Point3D(vPoint.X - vCameraPos.X, vPoint.Y - vCameraPos.Y, vPoint.Z - vCameraPos.Z);

            return m_mRotXYZ.Transform(vTmp);
        }
        public Vector3D VectorGlob2Loc(Vector3D vVec)
        {
            return m_mRotXYZ.Transform(vVec);
        }
        public Vector3D PointLoc2Glob(Vector3D vPoint)
        {
            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            vPoint=m_mInvRotXYZ.Transform(vPoint);
            Vector3D vTmp = new Vector3D(vPoint.X + vCameraPos.X, vPoint.Y + vCameraPos.Y, vPoint.Z + vCameraPos.Z);

            return vTmp;
        }
        public Point3D PointLoc2Glob(Point3D vPoint)
        {
            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            vPoint = m_mInvRotXYZ.Transform(vPoint);
            Point3D vTmp = new Point3D(vPoint.X + vCameraPos.X, vPoint.Y + vCameraPos.Y, vPoint.Z + vCameraPos.Z);

            return vTmp;
        }
        public Vector3D VectorLoc2Glob(Vector3D vVec)
        {
            return m_mInvRotXYZ.Transform(vVec);
        }




        public void SetNewCameraAt(double dGlobalX, double dGlobalY, double dGlobalZ)  // imposta la nuova camera at in base a quella in coordinate mondo passata in argomento
        {
            if (m_Projection == ViewerProjection.Planar)
                return;

            // Calcolo At in coordinate locali
            Vector3D vNewAt = new Vector3D(dGlobalX, dGlobalY, dGlobalZ);

            // Devo trasformare vNewAt con l'inverso della rotazione RotXYZ (=A); bisogna però tenere conto che il metodo A.Transform(v) esegue il prodotto v^ * A 
            // dove v^ è il vettore riga) che è ugiuale a A^ * v; Dato che in una rotazione inversione == trasposizione, nel nostro caso posso utilizzare
            // direttamente la RotXYZ

            vNewAt = m_mRotXYZ.Transform(vNewAt);  // InvRotXYX * vNewAt == vNewAt*RotXYX
            //++++++++++++++++++++++++++++++  // AM
            /*
            vNewAt.X = 0;
            vNewAt.Y = -1;
            vNewAt.Z = 0;

            double sTmp = Math.Sqrt(2) / 2;
            vNewAt.Y = -sTmp;
            vNewAt.Z = sTmp;
            */
            //++++++++++++++++++++++++++++++

            /*
            camPhi = 180.0*Math.Acos(vNewAt.Z)/Math.PI;
            camTheta=180.0*Math.Acos(vNewAt.Y/Math.Sqrt(1- vNewAt.Z* vNewAt.Z)) / Math.PI;
            if (vNewAt.X < 0)
                camTheta = -camTheta;
            */
            UpdateThetaAndPhi(vNewAt);
            MyCam.LookDirection = new Vector3D(vNewAt.X, vNewAt.Y, vNewAt.Z);

            //++++++++++++++++++++++++++++++++++++++++++++++++++++++++
            // Verifico che se la riteasformo torno all'originale
            // Vector3D vNewOrigAt = m_mRotXYZ.Transform(vNewAt);
            //++++++++++++++++++++++++++++++++++++++++++++++++++++++++


        }

        public void UpdateThetaAndPhi(Vector3D vCameraAt)
        {
            camPhi = 180.0 * Math.Acos(vCameraAt.Z) / Math.PI;
            camTheta = 180.0 * Math.Acos(vCameraAt.Y / Math.Sqrt(1 - vCameraAt.Z * vCameraAt.Z)) / Math.PI;
            if (vCameraAt.X < 0)
                camTheta = -camTheta;

            RaisePropertyChanged("Theta");
            RaisePropertyChanged("Phi");
        }

        public Matrix3D ComputeCameraRTMatrix(bool bReverseZ)
        {
            // Calcolo direzioni At,Up di camera locale, tenendo conto che in questo codice risulta sempre Up=[0,0,1]
            Vector3D vAt = MyCam.LookDirection;
            if(bReverseZ)
                vAt.Z = -vAt.Z;  // ATTENZIONE: Z va invertita per compatibilità sistema riferimento adottato da Scan2Bim
            Vector3D vTmp = Vector3D.CrossProduct(vAt, MyCam.UpDirection);  // Vetture trasversale
            vTmp.Normalize();
            Vector3D vRealUp = Vector3D.CrossProduct(vTmp, vAt);

            // Recupero posizione e rotazioni globali
            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            Vector3D vCameraRot = SharingHelper.GetCameraRot();

            // Matrice rotazione locale camera: [ Up | Up X At | At] 
            Matrix3D mCameraRot = new Matrix3D();
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

            mCameraRot = m_mRotXYZ* mCameraRot;



            mCameraRot.M14 = vCameraPos.X;
            mCameraRot.M24 = vCameraPos.Y;
            mCameraRot.M34 = vCameraPos.Z;

            return mCameraRot;
        }

        public Matrix3D GetViewMatrix()
        {
            // Calcolo direzioni At,Up di camera locale, tenendo conto che in questo codice risulta sempre Up=[0,0,1]
            Vector3D vAt = MyCam.LookDirection;
            vAt.Z = -vAt.Z;  // ATTENZIONE: Z va invertita per compatibilità sistema riferimento adottato da Scan2Bim
            Vector3D vTmp = Vector3D.CrossProduct(vAt, MyCam.UpDirection);  // Vetture trasversale
            vTmp.Normalize();
            Vector3D vRealUp = Vector3D.CrossProduct(vTmp, vAt);

            // Recupero posizione e rotazioni globali
            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            Vector3D vCameraRot = SharingHelper.GetCameraRot();

            // Matrice rotazione locale camera: [ Up | Up X At | At] 
            Matrix3D mCameraRot = new Matrix3D();
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

            return mCameraRot;
        }



        public void SaveCameraInfo(string sCameraInfoFileName)
        {
            int nWidth = Convert.ToInt32(m_ViewSize.Width * SharingHelper.m_dConvFactor);
            int nHeight = Convert.ToInt32(m_ViewSize.Height * SharingHelper.m_dConvFactor);
            // vp.Camera.Transform
            // vp.Camera.LookDirection
            double vFov = -1;
            double hFov = -1;
            if (m_Projection == ViewerProjection.Spheric)
            {
                vFov = Vfov;
                hFov = Hfov;
            }

            string sImageName=Path.GetFileName(sCameraInfoFileName);
            string sCIFName = SharingHelper.GetJsonPath()+"\\"+ Path.GetFileNameWithoutExtension(sCameraInfoFileName) +".cif";

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
                    sw.WriteLine("      <property name = \"fov_x\" value = \"" + Convert.ToString(hFov, CultureInfo.InvariantCulture) + "\" />");
                    sw.WriteLine("      <property name = \"fov_y\" value = \"" + Convert.ToString(vFov, CultureInfo.InvariantCulture) + "\" />");
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

                    //+++++++++++++++++++++++++++++++++
                    //Matrix3D mRotTmp = ComputeCameraRTMatrix();
                    //if(mCameraRot!= mRotTmp)
                    //{
                    //    int eccheccazzo=1;
                    //}
                    //++++++++++++++++++++++++++++++++++

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

        public void ComputePlanarCameraAt(ref double dX, ref double dY)  // Restituisce la cameraAt planare in coordinate mondo
        {
            if (m_Projection == ViewerProjection.Spheric)
            {
                Matrix3D mRotTmp = ComputeCameraRTMatrix(true);
                dX = mRotTmp.M13;
                dY = mRotTmp.M23;
                //+++++++++++++++++++
                //Console.WriteLine("Da ComputeCameraAtForServer");
                //Console.WriteLine("dX=" + dX.ToString()+"  dY="+ dY.ToString());
                //+++++++++++++++++++
                double dLen = Math.Sqrt(dX * dX + dY * dY);
                dX /= dLen;
                dY /= dLen;
            }
            else
            {
                // TODO
            }
        }

        public void Compute3DCameraAt(ref double dX, ref double dY, ref double dZ)  // Restituisce la cameraAt in coordinate mondo
        {
            if (m_Projection == ViewerProjection.Planar)
                return;

            Matrix3D mRotTmp = ComputeCameraRTMatrix(false);
            dX = mRotTmp.M13;
            dY = mRotTmp.M23;
            dZ = mRotTmp.M33;
            //+++++++++++++++++++
            //Console.WriteLine("Da ComputeCameraAtForServer");
            //Console.WriteLine("dX=" + dX.ToString() + "  dY=" + dY.ToString());
            //+++++++++++++++++++
        }


        // Mouse down: start moving camera
        public void vp_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))  // Attivazione drag mirino/creazione punti mirino 
            {
//                m_Window.Polygon_LeftCtrlMouseDown(sender, e);
                CUIManager.Polygon_LeftCtrlMouseDown(e);
            }
            else  // Spostamento CameraAt
            {
                isMouseDown = true;
                this.Cursor = System.Windows.Input.Cursors.SizeAll;
                clickX = Mouse.GetPosition(vp).X;
                clickY = Mouse.GetPosition(vp).Y;
                camThetaSpeed = camPhiSpeed = 0;
                timer.Start();
            }
        }


        // Mouse up: stop moving camera
        public void vp_MouseUp(object sender, MouseButtonEventArgs e)
        {
            /*
                        if (Keyboard.IsKeyDown(Key.LeftCtrl))
                        {
                            CUIManager.Polygon_MouseUp();
                        }
                        else
                        {
                            isMouseDown = false;
                            this.Cursor = System.Windows.Input.Cursors.Arrow;
                            timer.Stop();
                        }
            */
            CUIManager.Polygon_MouseUp();
            if (!Keyboard.IsKeyDown(Key.LeftCtrl))
            {
                isMouseDown = false;
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                timer.Stop();
            }


        }

        // Mouse wheel: zoom
        public void vp_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl))
            {
//                m_Window.Polygon_MouseWheel(sender, e);
                CUIManager.Polygon_MouseWheel(e);
            }
            else
            {
                if (m_Projection == ViewerProjection.Spheric)
                {
                    double dOldFov = MyCam.FieldOfView;
                    MyCam.FieldOfView -= e.Delta / 100;
                    if (MyCam.FieldOfView < 1)
                        MyCam.FieldOfView = 1;
                    else if (MyCam.FieldOfView > 140)
                        MyCam.FieldOfView = 140;

                    CUIManager.RescaleViewfinderOnFovChange(dOldFov, MyCam.FieldOfView);

                    SharingHelper.m_bCameraAtHasChanged = true;
                    RaisePropertyChanged("Hfov");
                    RaisePropertyChanged("Vfov");
                }
                else
                {
                    if (e.Delta > 0)
                        SharingHelper.m_dPlanarZoomFactor += 0.05;
                    else if (e.Delta < 0)
                        SharingHelper.m_dPlanarZoomFactor -= 0.05;

                    SetViewAndWindowSize();

                }


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
