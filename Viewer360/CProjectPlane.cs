using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Viewer360.View;
using System.Windows.Media;
using System.Numerics;
using System.Windows.Forms;
using HelixToolkit.Wpf;


namespace Viewer360
{
    static public class CProjectPlane
    {
        static public bool m_bIdDefined = false;
        static public Vector3D m_vCentreGlobal;
        static public Vector3D m_vNormalGlobal;
        static public Vector3D m_vCentreLocal;
        static public Vector3D m_vNormalLocalal;
        static public Viewer360View viewer360View = null;
        static public Viewport3D vp;

        static public void Init(Viewer360View viewer)
        {
            m_vCentreGlobal = new Vector3D();
            m_vNormalGlobal = new Vector3D();
            m_vCentreLocal = new Vector3D();
            m_vNormalLocalal = new Vector3D();
            m_bIdDefined = false;
            viewer360View = viewer;
            //            vp = viewport;
        }

        static public void SetPlane(double dGlobalPosX, double dGlobalPosY, double dGlobalNX, double dGlobalNY)
        {
            m_bIdDefined = true;
            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            m_vCentreGlobal = new Vector3D(dGlobalPosX, dGlobalPosX, vCameraPos.Z);
            m_vNormalGlobal = new Vector3D(dGlobalNX, dGlobalNY, 0);

            // Calcolo piano in riferimento locale
            m_vCentreLocal = viewer360View.PointGlob2Loc(m_vCentreGlobal);
            m_vNormalGlobal = viewer360View.VectorGlob2Loc(m_vNormalGlobal);
        }

        static public void RemovePlane()
        {
            m_bIdDefined = false;
        }

        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

        static public void Test3()
        {

            // Creare un punto nello spazio mondo
            Point3D pointWorld = new Point3D(viewer360View.MyCam.Position.X, viewer360View.MyCam.Position.Y - 10, viewer360View.MyCam.Position.Z);


           // Convertire le coordinate del punto da mondo a schermo utilizzando Viewport3DHelper
            Point pointScreen = Viewport3DHelper.Point3DtoPoint2D(viewer360View.vp, pointWorld);
            double dDist = CameraDist(pointWorld, viewer360View.MyCam);

            
        }

        static public double CameraDist(Point3D pointWorld, PerspectiveCamera myCam)
        {

            // Calcolare il vettore dalla telecamera al punto
            Vector3D vectorToCamera = pointWorld - myCam.Position;

            // Calcolare il prodotto scalare tra il vettore di direzione della telecamera e il vettore al punto
            double dotProduct = Vector3D.DotProduct(myCam.LookDirection, vectorToCamera);

            return dotProduct;

        }

    }

 //++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
}
