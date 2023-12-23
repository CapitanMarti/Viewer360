using Microsoft.Win32;
using Viewer360.Commands;
using Viewer360.Model;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Viewer360.View;
using PointCloudUtility;
using static Viewer360.View.CViewerCameraManager;
using System.Windows.Media.Media3D;
using System.IO;

namespace Viewer360.ViewModel
{
    /// <summary>
    /// Main ViewModel
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        public View.MainWindow m_Window;
        public int m_iCurrentPhotoIndex;
        public int m_iCurrentLabelIndex;
        public string m_sCurrentLabelFileName="";
        public CSingleFileLabel m_oCurrentLabel = null;

        // Commands
        #region commands

        /// <summary>
        /// Open image with dialog
        /// </summary>
        public ICommand OpenCommand { get; private set; }

        /// <summary>
        /// Open image by file name
        /// </summary>
        public ICommand OpenWithFilenameCommand { get; private set; }

        /// <summary>
        /// Exit application
        /// </summary>
        public ICommand ExitCommand { get; private set; }

        /// <summary>
        /// Toggle fullscreen
        /// </summary>
        public ICommand FullscreenCommand { get; private set; }

        /// <summary>
        /// Display controls
        /// </summary>
        public ICommand ControlsCommand { get; private set; }

        /// <summary>
        /// Display about information
        /// </summary>
        public ICommand AboutCommand { get; private set; }
        #endregion

        // Public properties
        #region public_properties

        /// <summary>
        /// 360° view
        /// </summary>
        public BitmapImage Image { get; private set; }

        /// <summary>
        /// Is fullscreen mode on
        /// </summary>
        public bool IsFullscreen { get; private set; }

        /// <summary>
        /// Is the model loading
        /// </summary>
        public bool IsLoading { get; private set; }

        /// <summary>
        /// Recent images manager
        /// </summary>
        public RecentImageManager RecentImageManager { get; private set; }
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MainViewModel()
        {
            Image = null; RaisePropertyChanged("Image");
            IsFullscreen = false; RaisePropertyChanged("IsFullscreen");
            IsLoading = false; RaisePropertyChanged("IsLoading");
            m_iCurrentPhotoIndex = -1;
            m_iCurrentLabelIndex = -1;
        }

        // Private methods
        #region private_methods

        // Open image with dialog
        private async void Open()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Images (*.jpg; *.jpeg; *.gif; *.bmp; *.png)|*.jpg; *.jpeg; *.gif; *.bmp; *.png";
            if (ofd.ShowDialog() == true)
            {
                await Open(ofd.FileName,"","","");
            }
        }

        public void NextImage_Click(object sender, RoutedEventArgs e)
        {
            m_iCurrentPhotoIndex++;
            if (m_iCurrentPhotoIndex >= CLabelManager.GetPhotoNum())
                m_iCurrentPhotoIndex = 0;

            LoadNewImage(CLabelManager.GetPhotoFullName(m_iCurrentPhotoIndex));
            SharingHelper.m_bPhotoHasChanged = true;

            // TODO mandare messaggio server su nuova immagine (camera) selezionata
        }

        public void PrevImage_Click(object sender, RoutedEventArgs e)
        {
            m_iCurrentPhotoIndex--;
            if (m_iCurrentPhotoIndex < 0)
                m_iCurrentPhotoIndex = CLabelManager.GetPhotoNum()-1;

            LoadNewImage(CLabelManager.GetPhotoFullName(m_iCurrentPhotoIndex));
            SharingHelper.m_bPhotoHasChanged = true;

            // TODO mandare messaggio server su nuova immagine (camera) selezionata
        }

        void SetupLabelData(CSingleFileLabel oLabel, int iCurrentPhotoIndex, int iCurrentLabelIndex)
        {
            string sFileName = CLabelManager.GetPhotoFullName(iCurrentPhotoIndex);
            if (m_iCurrentPhotoIndex != iCurrentPhotoIndex)
            {
                m_iCurrentPhotoIndex = iCurrentPhotoIndex;
                LoadNewImage(sFileName);
                SharingHelper.m_bPhotoHasChanged = true;

            }
            // Save label filename
            m_sCurrentLabelFileName = SharingHelper.GetJsonPath() + oLabel.m_sJpgFileName;
            m_oCurrentLabel = oLabel;


            // Qui devo impostare i parametri associati alla nuova Label: valori di UI e poligono
            m_iCurrentLabelIndex = iCurrentLabelIndex;
            CUIManager.InitUI(oLabel);

            // TODO mandare messaggio server su nuova immagine (camera) e oggetto selezionati

        }

        public void NextLabel_Click(object sender, RoutedEventArgs e)
        {
            CSingleFileLabel oLabel = null;

            int iCurrentPhotoIndex = m_iCurrentPhotoIndex;
            int iCurrentLabelIndex = m_iCurrentLabelIndex;
            oLabel = CLabelManager.GetNextLabel(ref iCurrentPhotoIndex, ref iCurrentLabelIndex);

            if (oLabel != null)
                SetupLabelData(oLabel, iCurrentPhotoIndex, iCurrentLabelIndex);

        }

        public void PrevLabel_Click(object sender, RoutedEventArgs e)
        {
            CSingleFileLabel oLabel = null;

            int iCurrentPhotoIndex = m_iCurrentPhotoIndex;
            int iCurrentLabelIndex = m_iCurrentLabelIndex;
            oLabel = CLabelManager.GetPrevLabel(ref iCurrentPhotoIndex, ref iCurrentLabelIndex);

            if (oLabel != null)
                SetupLabelData(oLabel, iCurrentPhotoIndex, iCurrentLabelIndex);

        }

        public void GetClosestLabel()
        {
            CSingleFileLabel oLabel = null;
            int iCurrentPhotoIndex = m_iCurrentPhotoIndex;
            int iCurrentLabelIndex = m_iCurrentLabelIndex;

            // Se esiste label associata ad indici correnti la prendo
            if (iCurrentPhotoIndex >= 0 && iCurrentLabelIndex >= 0)  
                oLabel = CLabelManager.GetLabel(iCurrentPhotoIndex, iCurrentLabelIndex);

            if(oLabel!=null)
            {
                SetupLabelData(oLabel, iCurrentPhotoIndex, iCurrentLabelIndex);
                return;
            }

            // Cerco label precedente
            oLabel = CLabelManager.GetPrevLabel(ref iCurrentPhotoIndex, ref iCurrentLabelIndex);
            if (oLabel != null)
            {
                SetupLabelData(oLabel, iCurrentPhotoIndex, iCurrentLabelIndex);
                return;
            }

            // Cerco label successiva
            oLabel = CLabelManager.GetNextLabel(ref iCurrentPhotoIndex, ref iCurrentLabelIndex);
            if (oLabel != null)
            {
                SetupLabelData(oLabel, iCurrentPhotoIndex, iCurrentLabelIndex);
                return;
            }

            return;

        }


        public void RestoreFovAndPolygons(CSingleFileLabel oLabel)
        {
            m_Window.viewer360_View.MyCam.FieldOfView = oLabel.m_hFov;
            m_Window.viewer360_View.MyCam.LookDirection = new Vector3D(oLabel.m_vLookDirectionX, oLabel.m_vLookDirectionY, oLabel.m_vLookDirectionZ);
            SharingHelper.m_bCameraAtHasChanged = true;
            m_Window.RestorePolygon(oLabel.m_aLabelInfo[0]);

            RaisePropertyChanged("Hfov");
            RaisePropertyChanged("Vfov");
        }

        public void LoadImage(string sImageFile)
        {
            m_Window.Title = "Scan2Bim 360° Viewer    -    " + System.IO.Path.GetFileName(sImageFile);

            m_iCurrentPhotoIndex = CLabelManager.GetPhotoIndexFromName(System.IO.Path.GetFileName(sImageFile));
            CViewerCameraManager.CameraInfo oInfo = CViewerCameraManager.GetCameraInfo(m_iCurrentPhotoIndex);
            SharingHelper.SetCameraPos(oInfo.dPosX, oInfo.dPosY, oInfo.dPosZ);
            SharingHelper.SetCameraRot(oInfo.dRotX, oInfo.dRotY, oInfo.dRotZ);
            m_Window.viewer360_View.ComputeGlobalRotMatrix();

            Image = null; RaisePropertyChanged("Image");
            IsLoading = true; RaisePropertyChanged("IsLoading");
            try
            {
                Image = new BitmapImage();
                Image.BeginInit();
                Image.CacheOption = BitmapCacheOption.OnLoad;
                Image.UriSource = new Uri(sImageFile);
                Image.EndInit();
                Image.Freeze();
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                ErrorMessage("Error", "Image not found.");
                Image = null;
            }
            catch (Exception ex)
            {
                ErrorMessage("Error", "Unknown error while loading image: " + ex.GetType().ToString() + ". Please report.");
                Image = null;
            }

            if (Image != null)
            {
                if (Math.Abs(Image.Width / Image.Height - 2) > 0.001)
                    WarningMessage("Warning", "The opened image is not equirectangular (2:1)! Rendering may be improper.");

            }

            IsLoading = false; RaisePropertyChanged("IsLoading");
            SharingHelper.m_bPhotoHasChanged = true;
            RaisePropertyChanged("Image");

        }

        // Open image by file name
        public async Task Open(string sObjCatalogFile, string sImageFile, string sJsonPath, string sSegmentPath)
        {
            SharingHelper.SetFileAndFolderNames(sImageFile, sJsonPath, sSegmentPath);
            m_Window.Title = "Scan2Bim 360° Viewer    -    " + System.IO.Path.GetFileName(sImageFile);

            //            SharingHelper.SetCameraPos(sX, sY, sZ);
            //            SharingHelper.SetCameraRot(sRotX, sRotY, sRotZ);

            m_iCurrentPhotoIndex = CLabelManager.GetPhotoIndexFromName(System.IO.Path.GetFileName(sImageFile));
            CViewerCameraManager.CameraInfo oInfo = CViewerCameraManager.GetCameraInfo(m_iCurrentPhotoIndex);
            SharingHelper.SetCameraPos(oInfo.dPosX, oInfo.dPosY, oInfo.dPosZ);
            SharingHelper.SetCameraRot(oInfo.dRotX, oInfo.dRotY, oInfo.dRotZ);
            m_Window.viewer360_View.ComputeGlobalRotMatrix();

            SharingHelper.LoadCatalogManager(sObjCatalogFile);

            Image = null; RaisePropertyChanged("Image");
            IsLoading = true; RaisePropertyChanged("IsLoading");

            await Task.Factory.StartNew(() =>
            {
                try
                {
                    Image = new BitmapImage();
                    Image.BeginInit();
                    Image.CacheOption = BitmapCacheOption.OnLoad;
                    Image.UriSource = new Uri(sImageFile);
                    Image.EndInit();
                    Image.Freeze();
                }
                catch (System.IO.DirectoryNotFoundException)
                {
                    ErrorMessage("Error", "Image not found.");
                    Image = null;
                }
                catch (Exception ex)
                {
                    ErrorMessage("Error", "Unknown error while loading image: " + ex.GetType().ToString() + ". Please report.");
                    Image = null;
                }
            });

            if (Image != null)
            {
                if (Math.Abs(Image.Width / Image.Height - 2) > 0.001)
                    WarningMessage("Warning", "The opened image is not equirectangular (2:1)! Rendering may be improper.");

                //RecentImageManager.AddAndSave(sImageFile);
            }

            IsLoading = false; RaisePropertyChanged("IsLoading");
            SharingHelper.m_bPhotoHasChanged=true;
            RaisePropertyChanged("Image");
        }

        public void LoadNewImage(string sImageFile)
        {
            m_Window.Title = "Scan2Bim 360° Viewer    -    " + System.IO.Path.GetFileName(sImageFile);

            // Memorizzo la cameraAt attuale in coordinate mondo
            double dOldAtX = 0;
            double dOldAtY = 0;
            double dOldAtZ = 0;
            m_Window.viewer360_View.Compute3DCameraAt(ref dOldAtX, ref dOldAtY, ref dOldAtZ);

            // Aggiorno il sistema di riferimento
            SharingHelper.SetFileName(sImageFile);

            m_iCurrentPhotoIndex = CLabelManager.GetPhotoIndexFromName(System.IO.Path.GetFileName(sImageFile));
            CViewerCameraManager.CameraInfo oInfo = CViewerCameraManager.GetCameraInfo(m_iCurrentPhotoIndex);
            SharingHelper.SetCameraPos(oInfo.dPosX, oInfo.dPosY, oInfo.dPosZ);
            SharingHelper.SetCameraRot(oInfo.dRotX, oInfo.dRotY, oInfo.dRotZ);

            m_Window.viewer360_View.ComputeGlobalRotMatrix();

            // Imposto la nuova CameraAt in modo che sia orientata nel mondo come quella precedente
            m_Window.viewer360_View.SetNewCameraAt(dOldAtX, dOldAtY, dOldAtZ);

            RaisePropertyChanged("Theta");
            RaisePropertyChanged("Phi");


            Image = null; RaisePropertyChanged("Image");
            IsLoading = true; RaisePropertyChanged("IsLoading");

            try
            {
                Image = new BitmapImage();
                Image.BeginInit();
                Image.CacheOption = BitmapCacheOption.OnLoad;
                Image.UriSource = new Uri(sImageFile);
                Image.EndInit();
                Image.Freeze();
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                ErrorMessage("Error", "Image not found.");
                Image = null;
            }
            catch (Exception ex)
            {
                ErrorMessage("Error", "Unknown error while loading image: " + ex.GetType().ToString() + ". Please report.");
                Image = null;
            }

            if (Image != null)
            {
                if (Math.Abs(Image.Width / Image.Height - 2) > 0.001)
                    WarningMessage("Warning", "The opened image is not equirectangular (2:1)! Rendering may be improper.");
            }

            IsLoading = false; RaisePropertyChanged("IsLoading");
            RaisePropertyChanged("Image");
        }


        // Exit application
        private void Exit()
        {
            App.Current.Shutdown();
        }

        // Toggle fullscreen
        private void FullScreen()
        {
            IsFullscreen = !IsFullscreen;
            RaisePropertyChanged("IsFullscreen");
        }

        //Display controls
        private void Controls()
        {
            InfoMessage("Controls", "Click and drag the mouse to move camera.\r\nScroll to zoom.");
        }

        // Display about information
        private void About()
        {
            InfoMessage("About", "Created by Ákos Hajdu, 2014.");
        }

        // Helper function to display an information
        private void InfoMessage(string caption, string text)
        {
            MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Helper function to display a warning
        private void WarningMessage(string caption, string text)
        {
            MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Helper function to display an error
        private void ErrorMessage(string caption, string text)
        {
            MessageBox.Show(text, caption, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        #endregion
    }
}
