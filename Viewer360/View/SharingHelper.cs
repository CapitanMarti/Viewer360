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
        private static string m_sNewPath;
        private static Vector3D m_vCameraPos;
        private static Vector3D m_vCameraRot;
        public static void SetFileAndFolderNames(string sFileName, string sNewPath)
        {
            m_sFullFileName = sFileName;
            m_sNewPath=sNewPath;
        }

        public static void LoadCatalogManager(string sCatalogName)
        {
            m_oCM = new CCatalogManager();
            m_oCM.LoadCatalogObjForm(sCatalogName);
            List<List<CCatalogManager.CObjInfo>> oLList=m_oCM.GetAllLabelGroupedByCategory();

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

        public static string GetNewPath()
        {
            return m_sNewPath;
        }

        public static string GetNewFileName()
        {
            string sTmp=Path.GetFileNameWithoutExtension(m_sFullFileName);
            bool bContinue = true;
            string sNewFile="";
            int iCount = 0;
            while (bContinue) 
            {
                sNewFile = m_sNewPath + "\\" + sTmp + "##" + Convert.ToString(iCount)+ Path.GetExtension(m_sFullFileName);
                if(!File.Exists(sNewFile)) 
                {
                    break;
                }
                iCount++;
            }
            return sNewFile;
        }
    }
}



