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
using System.Xml.Linq;

namespace Viewer360.View
{
    static public class CUIManager
    {
        static public View.MainWindow m_Window;
        static ViewerMode m_eMode;

        public enum ViewerMode
        {
            Create=0,
            Edit
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

            (m_Window.DataContext as ViewModel.MainViewModel).RestoreFovAndPolygons(oLabel);
        }

        static public void UpdateUI()
        {

            if (m_eMode == ViewerMode.Create)
            {
                m_Window.NextImageButton.IsEnabled = true;
                m_Window.NextImageButton.BorderBrush = Brushes.Black;
                m_Window.NextImageButton.Foreground= Brushes.Black;

                m_Window.PrevImageButton.IsEnabled = true;
                m_Window.PrevImageButton.BorderBrush = Brushes.Black;
                m_Window.PrevImageButton.Foreground = Brushes.Black;

                m_Window.NextLabelButton.IsEnabled = false;
                m_Window.NextLabelButton.BorderBrush = Brushes.LightGray;
                m_Window.NextLabelButton.Foreground = Brushes.LightGray;

                m_Window.PrevLabelButton.IsEnabled = false;
                m_Window.PrevLabelButton.BorderBrush = Brushes.LightGray;
                m_Window.PrevLabelButton.Foreground = Brushes.LightGray;

                m_Window.DeleteLabelButton.IsEnabled= false;
                m_Window.DeleteLabelButton.BorderBrush = Brushes.LightGray;
                m_Window.DeleteLabelButton.Foreground = Brushes.LightGray;

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

            }
            else  // Edit mode
            {
                m_Window.NextImageButton.IsEnabled = false;
                m_Window.NextImageButton.BorderBrush = Brushes.LightGray;
                m_Window.NextImageButton.Foreground = Brushes.LightGray;


                m_Window.PrevImageButton.IsEnabled = false;
                m_Window.PrevImageButton.BorderBrush = Brushes.LightGray;
                m_Window.PrevImageButton.Foreground = Brushes.LightGray;


                m_Window.NextLabelButton.IsEnabled = true;
                m_Window.NextLabelButton.BorderBrush = Brushes.Black;
                m_Window.NextLabelButton.Foreground = Brushes.Black;

                m_Window.DeleteLabelButton.IsEnabled = true;
                m_Window.DeleteLabelButton.BorderBrush = Brushes.Black;
                m_Window.DeleteLabelButton.Foreground = Brushes.Black;

                m_Window.PrevLabelButton.IsEnabled = true;
                m_Window.PrevLabelButton.BorderBrush = Brushes.Black;
                m_Window.PrevLabelButton.Foreground = Brushes.Black;

                m_Window.CategoryCombo.IsEnabled = true;
                m_Window.CategoryCombo.BorderBrush = Brushes.Black;
                m_Window.CategoryCombo.Foreground = Brushes.Black;

                m_Window.ItemCombo.IsEnabled = true;
                m_Window.ItemCombo.BorderBrush = Brushes.Black;
                m_Window.ItemCombo.Foreground = Brushes.Black;

                m_Window.ElementName.IsEnabled = true;
                m_Window.ElementName.BorderBrush = Brushes.Black;
                m_Window.ElementName.Foreground = Brushes.Black;

                m_Window.SaveButton.Content = "Save change";
            }

        }
        static public void SetViewerMode(ViewerMode eMode)
        {
            m_eMode = eMode;
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

                UpdateUI();
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
  //              m_Window.ViewFinderPolygon.Fill = new SolidColorBrush(Color.FromArgb(0, 255, 0, 0));
            }
            else
            {
                if (m_eMode != ViewerMode.Edit)  // Sto passando da Create a Edit
                {
                    (m_Window.DataContext as ViewModel.MainViewModel).GetClosestLabel();

                    // Se non esiste alcuna label ripristino la modalità Create
                    if ((m_Window.DataContext as ViewModel.MainViewModel).m_iCurrentLabelIndex == -1)
                    {
                        SetViewerMode(ViewerMode.Create);
                        return;
                    }

                }

                m_eMode = ViewerMode.Edit;
//                m_Window.ViewFinderPolygon.Fill = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
            }
            UpdateUI();
        }
    }
}
