using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    public static class Extensions
    {

        public static IEnumerable<Triangle> Contains(this IEnumerable<Triangle> triangles, Vector3[] vertices, Vector3 position)
        {
            return triangles.Where(triangle =>
            {
                var pointOnPlane = triangle.Plane.ClosestPointOnPlane(position);
                var inTriangle = PointInTriangle(pointOnPlane, vertices[triangle.IndexA], vertices[triangle.IndexB], vertices[triangle.IndexC]);
                return inTriangle;
            });
        }


        public static bool PointInTriangle(Vector3 P, params Vector3[] TriangleVectors)
        {
            Vector3 A = TriangleVectors[0], B = TriangleVectors[1], C = TriangleVectors[2];
            if (SameSide(P, A, B, C) && SameSide(P, B, A, C) && SameSide(P, C, A, B))
            {
                Vector3 vc1 = Vector3.Cross(A - B, A - C);
                if (Mathf.Abs(Vector3.Dot(A - P, vc1)) <= .01f)
                    return true;
            }

            return false;
        }

        public static bool SameSide(Vector3 p1, Vector3 p2, Vector3 A, Vector3 B)
        {
            Vector3 cp1 = Vector3.Cross(B - A, p1 - A);
            Vector3 cp2 = Vector3.Cross(B - A, p2 - A);
            if (Vector3.Dot(cp1, cp2) >= 0) return true;
            return false;
        }

    }
}