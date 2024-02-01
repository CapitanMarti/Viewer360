using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Viewer360.Model
{
    /// <summary>
    /// Helper class for geometric operations
    /// Source: http://www.codeproject.com/Articles/24727/WPF-D-Part-of-n
    /// </summary>
    public static class GeometryHelper
    {
        /// <summary>
        /// Get (x,y,z) coordinates for given angles and radius
        /// </summary>
        /// <param name="theta">Theta angle</param>
        /// <param name="phi">Phi angle</param>
        /// <param name="radius">Radius</param>
        /// <returns>Coordinates</returns>
        public static Point3D GetPosition(double theta, double phi, double radius)
        {
            double x = radius * Math.Sin(theta) * Math.Sin(phi);
            double z = radius * Math.Cos(phi);
            double y = radius * Math.Cos(theta) * Math.Sin(phi);

            return new Point3D(x, y, z);
        }

        /// <summary>
        /// Get normal vector for given angles
        /// </summary>
        /// <param name="theta">Theta angle</param>
        /// <param name="phi">Phi angle</param>
        /// <returns></returns>
        public static Vector3D GetNormal(double theta, double phi)
        {
            return (Vector3D)GetPosition(theta, phi, 1.0);
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        /// <param name="degrees">Value in degrees</param>
        /// <returns>Value in radians</returns>
        public static double Deg2Rad(double degrees)
        {
            return (degrees / 180.0) * Math.PI;
        }

        /// <summary>
        /// Get texture coordinates for given angles
        /// </summary>
        /// <param name="theta">Theta angle</param>
        /// <param name="phi">Phi angle</param>
        /// <returns></returns>
        public static Point GetTextureCoordinate(double theta, double phi)
        {
            Point p = new Point(theta / (2 * Math.PI), phi / (Math.PI));
            return p;
        }

        /// <summary>
        /// Create a tessellated sphere mesh
        /// </summary>
        /// <param name="tDiv">Theta divisions</param>
        /// <param name="pDiv">Phi divisions</param>
        /// <param name="radius">Radius</param>
        /// <returns>Sphere mesh</returns>
        public static MeshGeometry3D CreateSphereMesh(int tDiv, int pDiv, double radius)
        {
            double dt = Deg2Rad(360.0) / tDiv;
            double dp = Deg2Rad(180.0) / pDiv;

            MeshGeometry3D mesh = new MeshGeometry3D();

            // Calculate points with normals and texture coordinates
            for (int pi = 0; pi <= pDiv; pi++)
            {
                double phi = pi * dp;

                for (int ti = 0; ti <= tDiv; ti++)
                {
                    double theta = ti * dt;

                    mesh.Positions.Add(GetPosition(theta, phi, radius));
                    mesh.Normals.Add(GetNormal(theta, phi));
                    mesh.TextureCoordinates.Add(GetTextureCoordinate(theta, phi));
                }
            }

            // Calculate triangles
            for (int pi = 0; pi < pDiv; pi++)
            {
                for (int ti = 0; ti < tDiv; ti++)
                {
                    int x0 = ti;
                    int x1 = (ti + 1);
                    int y0 = pi * (tDiv + 1);
                    int y1 = (pi + 1) * (tDiv + 1);

                    mesh.TriangleIndices.Add(x0 + y0);
                    mesh.TriangleIndices.Add(x0 + y1);
                    mesh.TriangleIndices.Add(x1 + y0);

                    mesh.TriangleIndices.Add(x1 + y0);
                    mesh.TriangleIndices.Add(x0 + y1);
                    mesh.TriangleIndices.Add(x1 + y1);
                }
            }

            mesh.Freeze();
            return mesh;
        }

        static void ModifyPlaneMeshRatio(int nSizeX, int nSizeY)
        {

        }


        public static MeshGeometry3D CreatePlaneMesh(double dSizeX, double dSizeY,Camera cam, Vector3D vAt, Vector3D vUp, Vector3D vTras)
        //*******************************************************************************************************************
        //
        //     ^
        //     |
        //     |   1 ------- 2
        //     |   |       / |
        //     |   |      /  |
        // vUp |   |     /   |
        //     |   |    /    |
        //     |   |   /     |
        //     |   |  /      |
        //     |   | /       |
        //     |   0 ------- 3
        //      ------ vCameraTras -------->
        //   
        //  Nota: (vTras,vAt,vUp) formano una terna ortogonale destrorsa
        //   
        //*******************************************************************************************************************

        {
            OrthographicCamera oCam;
            Vector3D vGridCentre=new Vector3D(0,0,0);
            Vector3D vCameraUp = new Vector3D(0, 1, 0);
            Vector3D vCameraTras = new Vector3D(1, 0, 0);
            Vector3D vNormal = new Vector3D(0, 0, 1);


            if (cam != null)
            {
                oCam = (cam as OrthographicCamera);
                /*
                vOriginalPhotoAt.Normalize();

                Vector3D vAt2D= new Vector3D(vOriginalPhotoAt.X, vOriginalPhotoAt.Y, 0);
                vAt2D.Normalize();

                vCameraTras = new Vector3D(-vAt2D.Y, vAt2D.X, 0);

                vCameraUp = Vector3D.CrossProduct(vCameraTras, vOriginalPhotoAt);
                vCameraUp.Normalize();

                if(vCameraUp.Z < 0)
                {
                    vCameraUp = -vCameraUp;
                    vCameraTras = -vCameraTras;
                }
                */

                //++++++++++++++++++++++++++++
                //Vector3D vTmp= Vector3D.CrossProduct(vOriginalPhotoAt,vCameraUp);
                Vector3D vTmp= Vector3D.CrossProduct(vAt,vUp);
                //+++++++++++++++++++

                vGridCentre = vAt;
                vNormal = vAt;

                oCam = (cam as OrthographicCamera);
                oCam.LookDirection = vAt;
                oCam.UpDirection = vUp;
                oCam.Position = new Point3D(0, 0, 0);

            }

            MeshGeometry3D mesh = new MeshGeometry3D();

            double dX = 1;
            double dY = dSizeY / dSizeX;

            Vector3D v0 = vGridCentre - dX * vTras - dY * vUp;
            Vector3D v1 = vGridCentre - dX * vTras + dY * vUp;
            Vector3D v2 = vGridCentre + dX * vTras + dY * vUp;
            Vector3D v3 = vGridCentre + dX * vTras - dY * vUp;
            /*
                    mesh.Positions.Add(new Point3D(-dX, -dY, 0));
                    mesh.Positions.Add(new Point3D(-dX, dY, 0));
                    mesh.Positions.Add(new Point3D(dX, dY, 0));
                    mesh.Positions.Add(new Point3D(dX, -dY, 0));

                    mesh.Normals.Add(new Vector3D(0, 0, 1));
                    mesh.Normals.Add(new Vector3D(0, 0, 1));
                    mesh.Normals.Add(new Vector3D(0, 0, 1));
                    mesh.Normals.Add(new Vector3D(0, 0, 1));
            */
            mesh.Positions.Add(new Point3D(v0.X, v0.Y, v0.Z));
            mesh.Positions.Add(new Point3D(v1.X, v1.Y, v1.Z));
            mesh.Positions.Add(new Point3D(v2.X, v2.Y, v2.Z));
            mesh.Positions.Add(new Point3D(v3.X, v3.Y, v3.Z));

            mesh.Normals.Add(vNormal);
            mesh.Normals.Add(vNormal);
            mesh.Normals.Add(vNormal);
            mesh.Normals.Add(vNormal);

            mesh.TextureCoordinates.Add(new Point(0, dSizeY));
            mesh.TextureCoordinates.Add(new Point(0, 0));
            mesh.TextureCoordinates.Add(new Point(dSizeX, 0));
            mesh.TextureCoordinates.Add(new Point(dSizeX, dSizeY));

            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(1);

            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(2);

            mesh.Freeze();
            return mesh;
        }

    }
}
