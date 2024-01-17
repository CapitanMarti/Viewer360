using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using PointCloudUtility;


namespace Viewer360.View
{
    public static class SharingHelper
    {
        private static CCatalogManager m_oCM;

        private static string m_sFullFileName;
        private static string m_sLabelPath;
        private static string m_sSegmentPath;
        private static Vector3D m_vCameraPos;
        private static Vector3D m_vCameraRot;
        private static List<List<CCatalogManager.CObjInfo>> m_oCatalogGroupedElem;

        public static bool m_bCameraAtHasChanged = false;
        public static bool m_bPhotoHasChanged = false;
        public static bool m_bLabelHasChanged = false;
        public static bool m_bElementDeleted = false;
        public static bool m_bLabelAdded = false;
        public static int m_iSendCategoryToServer = -1;
        public static double m_dConvFactor;
        public static double m_dPlanarZoomFactor = 1;

        public static bool m_bSendInfoForCloudClick = false;
        public static string m_sNewJsonFileName = "";
        public static string m_sViewerPngFile = "";
        public static string m_sCloudClickPngFile = "";

        public class CNewMsgInfo1
        {
            public CSingleFileLabel m_sLabel;
            public string sNewJsonFileName;
        }
        public static CNewMsgInfo1 m_oMsgInfo1=null;


        static public CCatalogManager GetCatalogManager() { return m_oCM; } 
        public static void SetFileAndFolderNames(string sFileName, string sLabelPath, string sSegmentPath)
        {
            m_sFullFileName = sFileName;
            m_sLabelPath = sLabelPath;
            m_sSegmentPath = sSegmentPath;
        }

        public static string GetFullJpegFileName() { return m_sFullFileName; }

        public static void SetFileName(string sFileName)
        {
            m_sFullFileName = sFileName;
        }

        public static void LoadCatalogManager(string sCatalogName)
        {
            m_oCM = new CCatalogManager();
            m_oCM.LoadCatalogObjForm(sCatalogName);
            m_oCatalogGroupedElem = m_oCM.GetAllLabelGroupedByCategory();
        }

        public static List<List<CCatalogManager.CObjInfo>> GetAllLabelGroupedByCategory() { return m_oCatalogGroupedElem; }

        public static int SearchCategoryByID(int iIndex)
        {
            return m_oCM.SearchCategoryByID(iIndex);
        }

        public static void SetCameraPos(double dX, double dY, double dZ)
        {
            m_vCameraPos = new Vector3D(dX, dY, dZ);
        }
        public static void SetCameraRot(double dX, double dY, double dZ)
        {
            //++++++++++++++++++++++++  // AM
            //dX = 0;
            //dY = 0;
            //dZ = 90;
            //++++++++++++++++++++++++
            m_vCameraRot = new Vector3D(dX, dY, dZ);
        }

        public static void SetCameraPos(string sX, string sY, string sZ)
        {
            double dX=double.Parse(sX, CultureInfo.InvariantCulture);
            double dY = double.Parse(sY, CultureInfo.InvariantCulture);
            double dZ = double.Parse(sZ, CultureInfo.InvariantCulture);
            m_vCameraPos = new Vector3D(dX, dY, dZ);
        }
        public static void SetCameraRot(string sX, string sY, string sZ)
        {
            double dX = double.Parse(sX, CultureInfo.InvariantCulture);
            double dY = double.Parse(sY, CultureInfo.InvariantCulture);
            double dZ = double.Parse(sZ, CultureInfo.InvariantCulture);
            m_vCameraRot = new Vector3D(dX, dY, dZ);
        }

        public static Vector3D GetCameraPos()
        {
            return m_vCameraPos;
        }
        public static Vector3D GetCameraRot()
        {
            return m_vCameraRot;
        }

        public static string GetJsonPath()
        {
            return m_sLabelPath;
        }

        public static string GetSegmentPath()
        {
            return m_sSegmentPath;
        }


        public static void GetUniqueFileNameForCloudClick(ref string sJsonFile, ref string sViewerPngFile, ref string sCloudClickPngFile)
        {
            string sCloudRenderPath = m_sLabelPath + "\\..\\CloudRender\\";
            string sPngFileName=Path.GetFileNameWithoutExtension(m_sFullFileName);
            bool bContinue = true;

            string sTmpName = "";
            int iCount = 0;
            while (bContinue)
            {
                sTmpName = sPngFileName + "##" + Convert.ToString(iCount) + ".*";

                var files = Directory.GetFiles(sCloudRenderPath, sTmpName);
                if (files.Length == 0)
                {
                    sViewerPngFile = sCloudRenderPath + sPngFileName + "##" + Convert.ToString(iCount) + ".png";
                    sCloudClickPngFile = sCloudRenderPath + sPngFileName + "##" + Convert.ToString(iCount) + "_CC.png";
                    sJsonFile = m_sLabelPath + "\\" + sPngFileName + "##" + Convert.ToString(iCount) + "_CC.json";
                    return;
                }
                iCount++;
            }
        }

        public static string GetUniqueFileName(string sExt)
        {
            string sTmp=Path.GetFileNameWithoutExtension(m_sFullFileName);
            bool bContinue = true;
            string sNewFile="";
            string sTmpName = "";
            int iCount = 0;
            while (bContinue) 
            {
                sTmpName= sTmp + "##" + Convert.ToString(iCount) + ".*";
                var files = Directory.GetFiles(m_sLabelPath, sTmpName);
                if (files.Length==0) 
                {
                    sNewFile = m_sLabelPath + "\\" + sTmp + "##" + Convert.ToString(iCount) + sExt;
                    break;
                }
                iCount++;
            }
            return sNewFile;
        }
    }
}



