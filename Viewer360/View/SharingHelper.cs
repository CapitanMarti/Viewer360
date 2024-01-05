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
        private static string m_sJsonPath;
        private static string m_sSegmentPath;
        private static Vector3D m_vCameraPos;
        private static Vector3D m_vCameraRot;
        private static List<List<CCatalogManager.CObjInfo>> m_oCatalogGroupedElem;

        public static bool m_bCameraAtHasChanged = false;
        public static bool m_bPhotoHasChanged = false;
        public static bool m_bLabelHasChanged = false;
        public static bool m_bElementDeleted = false;
        public static bool m_bLabelAdded = false;
        public static bool m_bCastPlaneRequestedWall = false;
        public static double m_dConvFactor;
        public static double m_dPlanarZoomFactor = 1;




        static public CCatalogManager GetCatalogManager() { return m_oCM; } 
        public static void SetFileAndFolderNames(string sFileName, string sJsonPath, string sSegmentPath)
        {
            m_sFullFileName = sFileName;
            m_sJsonPath = sJsonPath;
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

        public static void SetCameraPos(double dX, double dY, double dZ)
        {
            m_vCameraPos = new Vector3D(dX, dY, dZ);
        }
        public static void SetCameraRot(double dX, double dY, double dZ)
        {
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
            return m_sJsonPath;
        }

        public static string GetSegmentPath()
        {
            return m_sSegmentPath;
        }

        public static string GetNewJsonFileName()
        {
            string sTmp=Path.GetFileNameWithoutExtension(m_sFullFileName);
            bool bContinue = true;
            string sNewFile="";
            int iCount = 0;
            while (bContinue) 
            {
                sNewFile = m_sJsonPath + "\\" + sTmp + "##" + Convert.ToString(iCount)+ ".json";
//                sNewFile = m_sJsonPath + "\\" + sTmp + "##" + Convert.ToString(iCount) + Path.GetExtension(m_sFullFileName);
                if (!File.Exists(sNewFile)) 
                {
                    break;
                }
                iCount++;
            }
            return sNewFile;
        }
    }
}



