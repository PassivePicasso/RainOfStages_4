using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [System.Serializable]
    public struct TriangleCollection : IEnumerable<Triangle>
    {
        private static readonly Dictionary<int, int> vertexRemap = new Dictionary<int, int>();
        private static readonly Dictionary<(int low, int high), (int left, int right)> lookup = new Dictionary<(int low, int high), (int left, int right)>();

        [SerializeField]
        private List<Triangle> triangles;
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

        public int Count => triangles.Count;

        public IEnumerable<Triangle> WithNormal(Vector3 normal, float margin)
        {
            return triangles.Where(t => Vector3.Dot(t.Plane.normal, normal) > (1 - margin));
        }

        public Vector3 PointInside(Triangle triangle)
        {
            return PointInside(vertices[triangle.IndexA], vertices[triangle.IndexB], vertices[triangle.IndexC]);
        }

        public Vector3 PointInside(Triangle triangle, float a, float b)
        {
            return PointInside(vertices[triangle.IndexA], vertices[triangle.IndexB], vertices[triangle.IndexC], a, b);
        }

        public Vector3 PointInside(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC)
        {
            var a = Random.value;
            var b = Random.value;
            Vector3 result = PointInside(vertexA, vertexB, vertexC, a, b);
            return result;
        }

        public Vector3 PointInside(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC, float a, float b)
        {
            Vector3 va = vertexA;
            Vector3 vb = vertexB;
            Vector3 vc = vertexC;
            var modA = (1 - Mathf.Sqrt(a)) * va;
            var modB = (Mathf.Sqrt(a) * (1 - b)) * vb;
            var modC = (b * Mathf.Sqrt(a)) * vc;

            var result = modA + modB + modC;
            return result;
        }
        public Vector3 PointInsideNaive(Vector3 vertexA, Vector3 vertexB, Vector3 vertexC, float ta, float tb)
        {
            var ab = Vector3.Lerp(vertexA, vertexB, ta);
            var ac = Vector3.Lerp(vertexA, vertexC, ta);
            var abac = Vector3.Lerp(ab, ac, tb);
            return abac;
        }

        public bool ContainsDuplicate((Vector3 a, Vector3 b, Vector3 c) triangle)
        {
            var matches = vertices.Where(v => v == triangle.a || v == triangle.b || v == triangle.c).Any();

            return matches;
        }
        public (Vector3 a, Vector3 b, Vector3 c) Vertices(Triangle triangle) => (vertices[triangle.IndexA], vertices[triangle.IndexB], vertices[triangle.IndexC]);

        public TriangleCollection(Vector3[] vertices, int[] indices)
        {
            this.triangles = new List<Triangle>();
            this.vertices = vertices;
            this.indices = indices;
            ConstructTriangles();
        }

        public TriangleCollection GetDetached(IEnumerable<Triangle> triangles)
        {
            int i = 0;
            vertexRemap.Clear();
            var indices = new List<int>();
            var vertices = new List<Vector3>();
            foreach (var triangle in triangles)
            {
                var (a, b, c) = Vertices(triangle);
                Remap(triangle.IndexA, indices, vertices, a);
                Remap(triangle.IndexB, indices, vertices, b);
                Remap(triangle.IndexC, indices, vertices, c);
            }

            return new TriangleCollection(vertices.ToArray(), indices.ToArray());
        }

        void Remap(int index, List<int> indices, List<Vector3> vertices, Vector3 vertex)
        {
            if (vertexRemap.ContainsKey(index))
            {
                indices.Add(vertexRemap[index]);
            }
            else
            {
                var i = vertexRemap[index] = vertices.Count;
                indices.Add(i);
                vertices.Add(vertex);
            }
        }

        public Mesh ToMesh()
        {
            var linkMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            linkMesh.vertices = vertices;
            linkMesh.triangles = indices;

            return linkMesh;
        }


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
            lookup.Clear();
            for (int i = 0; i < triangles.Count; i++)
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

        public (float ab, float bc, float ca) EdgeLengths(Triangle triangle)
        {
            var a = vertices[triangle.IndexA];
            var b = vertices[triangle.IndexB];
            var c = vertices[triangle.IndexC];

            return (Vector3.Distance(a, b), Vector3.Distance(b, c), Vector3.Distance(c, a));
        }

        public IEnumerator<Triangle> GetEnumerator() => triangles.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => triangles.GetEnumerator();
    }
}