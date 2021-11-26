using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [System.Serializable]
    public class TriangleCollection
    {
        private Dictionary<(int low, int high), (int left, int right)> lookup;

        [SerializeField]
        private List<Triangle> triangles = new List<Triangle>();
        [SerializeField]
        private Vector3[] vertices;
        [SerializeField]
        private int[] indices;

        public Triangle this[int index]
        {
            get
            {
                return triangles[index];
            }
        }

        public IEnumerable<Triangle> WithNormal(Vector3 normal, float margin)
        {
            return triangles.Where(t => Vector3.Dot(t.Plane.normal, normal) > (1 - margin));
        }

        public Vector3 PointInside(Triangle triangle)
        {
            var vertexA = vertices[triangle.IndexA];
            var vertexB = vertices[triangle.IndexB];
            var vertexC = vertices[triangle.IndexC];
            var a = Random.value;
            var b = Random.value;
            var modA = (1 - Mathf.Sqrt(a)) * vertexA;
            var modB = (Mathf.Sqrt(a) * (1 - b)) * vertexB;
            var modC = (b * Mathf.Sqrt(a)) * vertexC;

            var result = modA + modB + modC;
            return result;
        }


        public TriangleCollection(Vector3[] vertices, int[] indices) => Update(vertices, indices);

        public void Update(Vector3[] vertices, int[] indices)
        {
            this.vertices = vertices;
            this.indices = indices;

            ConstructTriangles();
        }

        (int, int) Index(int a, int b) => (Mathf.Min(a, b), Mathf.Max(a, b));
        void ConstructTriangles()
        {
            Profiler.BeginSample("Construct Triangles");
            for (int i = 0; i < indices.Length; i += 3)
            {
                triangles.Add(new Triangle
                {
                    IndexA = indices[i + 0],
                    IndexB = indices[i + 1],
                    IndexC = indices[i + 2],
                    Plane = new Plane(vertices[indices[i + 0]],
                                      vertices[indices[i + 1]],
                                      vertices[indices[i + 2]])
                });
            }
            Profiler.EndSample();
        }

        public void Weld()
        {
            Profiler.BeginSample("Weld Triangles");
            MeshWelder.Weld(ref vertices, ref indices);
            Profiler.EndSample();
        }

        public void NeighborizeTriangles()
        {
            Profiler.BeginSample("Neighborize Triangles");
            lookup = new Dictionary<(int low, int high), (int left, int right)>();
            for (int i = 0; i < triangles.Count; i ++)
            {
                var tri = triangles[i];
                var ab = Index(tri.IndexA, tri.IndexB);
                var ac = Index(tri.IndexA, tri.IndexC);
                var cb = Index(tri.IndexC, tri.IndexB);
                if (!lookup.ContainsKey(ab)) lookup[ab] = (-1, -1);
                if (!lookup.ContainsKey(ac)) lookup[ac] = (-1, -1);
                if (!lookup.ContainsKey(cb)) lookup[cb] = (-1, -1);
                var iab = lookup[ab];
                var iac = lookup[ac];
                var icb = lookup[cb];
                if (iab.left == -1) iab.left = i;
                else iab.right = i;
                if (iac.left == -1) iac.left = i;
                else iac.right = i;
                if (icb.left == -1) icb.left = i;
                else icb.right = i;
                lookup[ab] = iab;
                lookup[ac] = iac;
                lookup[cb] = icb;
            }
            foreach (var edge in lookup.Values)
            {
                if (edge.left < 0 || edge.right < 0) continue;
                triangles[edge.left].AssignNeighbor(triangles[edge.right], edge.right);
                triangles[edge.right].AssignNeighbor(triangles[edge.left], edge.left);
            }
            Profiler.EndSample();
        }

        //linePnt - point the line passes through
        //lineDir - unit vector in direction of line, either direction works
        //pnt - the point to find nearest on line for
        public static Vector3 NearestPointOnLine(Vector3 linePnt, Vector3 lineDir, Vector3 pnt)
        {
            lineDir.Normalize();//this needs to be a unit vector
            var v = pnt - linePnt;
            var d = Vector3.Dot(v, lineDir);
            return linePnt + lineDir * d;
        }

        public float Area(Triangle triangle)
        {
            var a = vertices[triangle.IndexA];
            var b = vertices[triangle.IndexB];
            var c = vertices[triangle.IndexC];
            var aOnBase = NearestPointOnLine(b, c, a);
            var height = Vector3.Distance(a, aOnBase);
            var baseLength = Vector3.Distance(b, c);
            var area = (baseLength * height) / 2;
            return area;
        }

    }
}