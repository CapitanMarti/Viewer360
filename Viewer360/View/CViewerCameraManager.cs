using PointCloudUtility;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Xml;

namespace Viewer360.View
{
    static public class CViewerCameraManager
    {

        public class CameraInfo
        {
            public string sPhotoName { get; set; }
            public double dPosX { get; set; }
            public double dPosY { get; set; }
            public double dPosZ { get; set; }
            public double dRotX { get; set; }
            public double dRotY { get; set; }
            public double dRotZ { get; set; }
            public double dAtX { get; set; }
            public double dAtY { get; set; }
            public double dAtZ { get; set; }
            public double dFovH { get; set; }
            public double dFovV { get; set; }

            public CameraInfo()
            {
                sPhotoName = "";
                dPosX = dPosY = dPosZ = 0;
                dRotX = dRotY = dRotZ = 0;
            }

        }


        static List<CameraInfo> m_aCameraInfo;



        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, int dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenFileMapping(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);

        static public int Init()
        {
//            const uint PAGE_READWRITE = 0x04;
            const int FILE_MAP_ALL_ACCESS = 0xF001F;

            // Aprire l'oggetto mappato in memoria
            IntPtr hMapFileRead = OpenFileMapping(FILE_MAP_ALL_ACCESS, false, "CameraManager_SharedMemory");

            if (hMapFileRead == IntPtr.Zero)
                return -1;

            // Ottenere un puntatore al buffer mappato in memoria
            IntPtr pBufferRead = MapViewOfFile(hMapFileRead, FILE_MAP_ALL_ACCESS, 0, 0, 0);

            if (pBufferRead == IntPtr.Zero)
            {
                CloseHandle(hMapFileRead);
                return -2;
            }

            // Leggere la lunghezza della stringa
            int length = Marshal.ReadInt32(pBufferRead);

            // Leggere la stringa dalla memoria condivisa
            byte[] byteBuffer = new byte[length];
            Marshal.Copy(pBufferRead + sizeof(uint), byteBuffer, 0, length);
            string readString = Encoding.UTF8.GetString(byteBuffer);

            // Chiudere l'handle della mappatura in memoria
            CloseHandle(hMapFileRead);

            m_aCameraInfo = new List<CameraInfo>();

            // Inizializzo xml
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(readString);
            XmlNode eleRoot = XmlUtil.SearchXmlFirstElement(xmlDoc.DocumentElement, "CameraManager");

            XmlNode cameraNode = eleRoot.FirstChild;
            while (cameraNode != null)
            {
                CameraInfo oTmp = new CameraInfo();
                XmlNode paramNode = cameraNode.FirstChild;
                while (paramNode != null)
                {
                    string sName = paramNode.Name.ToString();

                    if (sName == "Name")
                        oTmp.sPhotoName = paramNode.InnerText.ToString();
                    else if (sName == "X")
                        oTmp.dPosX = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "Y")
                        oTmp.dPosY = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "Z")
                        oTmp.dPosZ = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "RX")
                        oTmp.dRotX = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "RY")
                        oTmp.dRotY = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "RZ")
                        oTmp.dRotZ = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "vAtX")
                        oTmp.dAtX = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "vAtY")
                        oTmp.dAtY = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "vAtZ")
                        oTmp.dAtZ = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "fovH")
                        oTmp.dFovH = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);
                    else if (sName == "fovV")
                        oTmp.dFovV = double.Parse(paramNode.InnerText.ToString(), CultureInfo.InvariantCulture);

                    paramNode = paramNode.NextSibling;
                }
                m_aCameraInfo.Add(oTmp);

                cameraNode = cameraNode.NextSibling;
            }



            return 0;
        }

        static public CameraInfo GetCameraInfo(string sCameraName)
        {
            if (m_aCameraInfo == null)
                return null;

            foreach(var oInfo in m_aCameraInfo)
            {
                if (sCameraName == oInfo.sPhotoName)
                    return oInfo;
            }

            return null;
        }


        static public CameraInfo GetCameraInfo(int index)
        {
            if(m_aCameraInfo!=null)
                return m_aCameraInfo[index];

            CameraInfo oDummy=new CameraInfo();
            return oDummy;
        }
    }
}
