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

namespace Viewer360.ViewModel
{
    /// <summary>
    /// Main ViewModel
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        public View.MainWindow m_Window;
        int m_iCurrentPhotoIndex;

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
            /*
            OpenCommand = new RelayCommand(a => Open());
            OpenWithFilenameCommand = new RelayCommand(a => Open((string)a,"",""));
            ExitCommand = new RelayCommand(a => Exit());
            FullscreenCommand = new RelayCommand(a => FullScreen());
            ControlsCommand = new RelayCommand(a => Controls());
            AboutCommand = new RelayCommand(a => About());

            RecentImageManager = new Model.RecentImageManager(); RaisePropertyChanged("RecentImages");
            */
            Image = null; RaisePropertyChanged("Image");
            IsFullscreen = false; RaisePropertyChanged("IsFullscreen");
            IsLoading = false; RaisePropertyChanged("IsLoading");
            m_iCurrentPhotoIndex = -1;
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
                await Open(ofd.FileName,"","");
            }
        }

        // Open image by file name
//        public async Task Open(string sObjCatalogFile, string sImageFile, string sNewPath, string sX, string sY, string sZ,string sRotX, string sRotY, string sRotZ)
        public async Task Open(string sObjCatalogFile, string sImageFile, string sNewPath)
        {
            SharingHelper.SetFileAndFolderNames(sImageFile, sNewPath);
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
            RaisePropertyChanged("Image");
        }

        public void LoadNewImage(string sImageFile)
        {
            // Memorizzo la cameraAt attuale in coordinate mondo
            double dOldAtX = 0;
            double dOldAtY = 0;
            double dOldAtZ = 0;
            m_Window.viewer360_View.Compute3DCameraAt(ref dOldAtX, ref dOldAtY, ref dOldAtZ);

            // Aggiorno il sistema di riferimento
            SharingHelper.SetFileName(sImageFile);
            m_Window.Title = "Scan2Bim 360° Viewer    -    " + System.IO.Path.GetFileName(sImageFile);

            m_iCurrentPhotoIndex = CLabelManager.GetPhotoIndexFromName(System.IO.Path.GetFileName(sImageFile));
            CViewerCameraManager.CameraInfo oInfo = CViewerCameraManager.GetCameraInfo(m_iCurrentPhotoIndex);
            SharingHelper.SetCameraPos(oInfo.dPosX, oInfo.dPosY, oInfo.dPosZ);
            SharingHelper.SetCameraRot(oInfo.dRotX, oInfo.dRotY, oInfo.dRotZ);

//            SharingHelper.SetCameraPos(sX, sY, sZ);
//            SharingHelper.SetCameraRot(sRotX, sRotY, sRotZ);
            m_Window.viewer360_View.ComputeGlobalRotMatrix();
            SharingHelper.m_bCameraAtHasChanged = true;

            // Imposto la nuova CameraAt in modo che sia orientata nel mondo come quella precedente
            m_Window.viewer360_View.SetNewCameraAt(dOldAtX, dOldAtY, dOldAtZ);

            //++++++++++++++++++++++++++++++++
            // double dNewAtX = 0;
            // double dNewAtY = 0;
            // double dNewAtZ = 0;
            // m_Window.viewer360_View.Compute3DCameraAt(ref dNewAtX, ref dNewAtY, ref dNewAtZ);
            //++++++++++++++++++++++++++++++++
            RaisePropertyChanged("Theta");
            RaisePropertyChanged("Phi");


            Image = null; RaisePropertyChanged("Image");
            IsLoading = true; RaisePropertyChanged("IsLoading");

//            Task.Factory.StartNew(() =>
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
            };

            if (Image != null)
            {
                if (Math.Abs(Image.Width / Image.Height - 2) > 0.001)
                    WarningMessage("Warning", "The opened image is not equirectangular (2:1)! Rendering may be improper.");

//                RecentImageManager.AddAndSave(sImageFile);
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
