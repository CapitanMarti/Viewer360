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
//        static public int m_iSide = -1;
        static public string m_sWallName = "";
        static public bool m_bPlaneDefined = false;
        static public Vector3D m_vCentreGlobal;
        static public Vector3D m_vNormalGlobal;
        static public Vector3D m_vTangGlobal;
        static public Vector3D m_vCentreLocal;
        static public Vector3D m_vNormalLocal;
        static public Viewer360View viewer360View = null;
        static public Viewport3D vp;
        static public Point3D[] m_aFace3DPointLoc;
        static public Point3D[] m_aFace3DPointGlob;
        static public void Init(Viewer360View viewer)
        {
            m_vCentreGlobal = new Vector3D();
            m_vNormalGlobal = new Vector3D();
            m_vTangGlobal = new Vector3D(2, 2, 2); // Valore nullo
            m_vCentreLocal = new Vector3D();
            m_vNormalLocal = new Vector3D();
            m_bPlaneDefined = false;
            viewer360View = viewer;
            m_aFace3DPointLoc = null;
            m_aFace3DPointGlob = null;
            //            vp = viewport;
        }

        static public void SetPlane(double dGlobalPosX, double dGlobalPosY, double dGlobalNX, double dGlobalNY, string sWallName)
        {
            m_bPlaneDefined = true;
            m_aFace3DPointLoc = new Point3D[4];
            m_aFace3DPointGlob = new Point3D[4];

            Vector3D vCameraPos = SharingHelper.GetCameraPos();
            m_vCentreGlobal = new Vector3D(dGlobalPosX, dGlobalPosY, vCameraPos.Z);
            m_vNormalGlobal = new Vector3D(dGlobalNX, dGlobalNY, 0);
//            m_iSide = iSide;
            m_sWallName = sWallName;

            //++++++++++++++++++++++++++++++++
            // m_vNormalGlobal = new Vector3D(0, 1, 0); // AM
            //++++++++++++++++++++++++++++++++

            // Calcolo piano in riferimento locale
            m_vCentreLocal = viewer360View.PointGlob2Loc(m_vCentreGlobal);
            m_vNormalLocal = viewer360View.VectorGlob2Loc(m_vNormalGlobal);
        }

        static public void ComputeTangAxes()
        {
            if (m_aFace3DPointGlob != null)
            {
                m_vTangGlobal = new Vector3D(m_aFace3DPointGlob[1].X- m_aFace3DPointGlob[0].X, m_aFace3DPointGlob[1].Y - m_aFace3DPointGlob[0].Y, 0); // Valore nullo
                m_vTangGlobal.Normalize();
            }

        }

        static public void RemovePlane()
        {
            m_bPlaneDefined = false;
            m_aFace3DPointLoc = null;
            m_aFace3DPointGlob = null;
            m_vTangGlobal = new Vector3D(2, 2, 2); // Valore nullo
        }

        static public Point3D? GetIntersection(Ray3D oRay)
        {
            oRay.Direction/=oRay.Direction.Length;
//+++++++++++++++++++++++++
//            oRay.Direction /= -oRay.Direction.Length;
//            oRay.Direction = new Vector3D(oRay.Direction.X, oRay.Direction.Y, -oRay.Direction.Z);
//+++++++++++++++++++++++++
            double dDen = Vector3D.DotProduct(oRay.Direction, m_vNormalLocal);
            if (Math.Abs(dDen) > 1e-3)
            {
                Vector3D vDelta = new Vector3D(m_vCentreLocal.X - oRay.Origin.X, m_vCentreLocal.Y - oRay.Origin.Y, m_vCentreLocal.Z - oRay.Origin.Z);
                double s = Vector3D.DotProduct(vDelta, m_vNormalLocal) / dDen;
                Point3D vResult = new Point3D(oRay.Origin.X+s* oRay.Direction.X, oRay.Origin.Y + s * oRay.Direction.Y, oRay.Origin.Z + s * oRay.Direction.Z);

                return vResult;

            }
            else
                return null;

        }

        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++

        public static bool Point2DtoPoint3D(this Viewport3D viewport, Point pointIn, out Point3D pointNear, out Point3D pointFar)
        {
            pointNear = new Point3D();
            pointFar = new Point3D();

            var pointIn3D = new Point3D(pointIn.X, pointIn.Y, 0);
            var matrixViewport = Viewport3DHelper.GetViewportTransform(viewport);
            var matrixCamera = Viewport3DHelper.GetCameraTransform(viewport);

            if (!matrixViewport.HasInverse)
            {
                return false;
            }

            if (!matrixCamera.HasInverse)
            {
                return false;
            }

            matrixViewport.Invert();
            matrixCamera.Invert();

            var pointNormalized = matrixViewport.Transform(pointIn3D);
            pointNormalized.Z = 0.01;
            pointNear = matrixCamera.Transform(pointNormalized);
            pointNormalized.Z = 0.99;
            pointFar = matrixCamera.Transform(pointNormalized);

            return true;
        }


        static public double CameraDist(Point3D pointWorld, PerspectiveCamera myCam)
        {

            // Calcolare il vettore dalla telecamera al punto
            Vector3D vectorToCamera = pointWorld - myCam.Position;

            // Calcolare il prodotto scalare tra il vettore di direzione della telecamera e il vettore al punto
            //            double dotProduct = Vector3D.DotProduct(myCam.LookDirection, vectorToCamera);
            double dotProduct = myCam.LookDirection.X * vectorToCamera.X + myCam.LookDirection.Y * vectorToCamera.Y - myCam.LookDirection.Z * vectorToCamera.Z;  // LA Z della camera è ribaltata!!

            return dotProduct;

        }

    }

 //++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
}
