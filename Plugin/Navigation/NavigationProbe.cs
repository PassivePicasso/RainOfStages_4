using DataStructures.ViliWonka.KDTree;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using static RoR2.Navigation.NodeGraph;
using static PassivePicasso.RainOfStages.Plugin.Utilities.HullHelper;
using static PassivePicasso.RainOfStages.Plugin.Utilities.PhysicsHelper;
using RoR2.Navigation;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class NavigationProbe : MonoBehaviour
    {
        private static readonly List<int> resultsIndices = new List<int>();
        public Color navigationProbeColor = Color.green;

        public float distance = 15;
        private float lastDistance = float.MinValue;
        public float nodeSeparation = 8;
        private float lastnodeSeparation = 8;
        public int passes;
        public float marginFromUp = 0.5f;
        public bool drawDebug = false;
        public bool isDirty = false;

        [SerializeField]
        private MeshFilter[] meshFilters;
        [SerializeField, HideInInspector]
        public TriangleCollection[] triangleCollections;
        public Mesh[] meshes;
        public List<Node> GroundNodes;
        #region Unity Messages
        void Update()
        {
            if (triangleCollections == null || transform.hasChanged || lastDistance != distance || isDirty)
            {
                int hits = Physics.OverlapSphereNonAlloc(transform.position, distance, colliders);
                if (hits > 0)
                {
                    meshFilters = colliders.Where((collider, i) => collider && i < hits)
                        .SelectMany(c => c.GetComponentsInChildren<MeshFilter>())
                        .Where(mf => mf.gameObject.layer == LayerIndex.world.intVal)
                        .ToArray();
                    UpdateTriangleCollections();
                    LoadGroundPoints();
                }
                isDirty = false;
                transform.hasChanged = false;
                lastDistance = distance;
            }
        }
        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = navigationProbeColor;
            Gizmos.DrawCube(transform.position, Vector3.one);

            if (!drawDebug) return;

            Gizmos.color = new Color(navigationProbeColor.r, navigationProbeColor.g, navigationProbeColor.b, 0.25f);
            //Gizmos.DrawSphere(transform.position, distance);
        }

        #endregion

        #region Ground Graph

        public void LoadGroundPoints()
        {
            GroundNodes = new List<Node>();
            var nodePoints = new List<Vector3>();

            for (int i = 0; i < triangleCollections.Length; i++)
                for (int j = 0; j < triangleCollections[i].Count; j++)
                {
                    var vertices = triangleCollections[i].Vertices(triangleCollections[i][j]);
                    var edgeLengths = triangleCollections[i].EdgeLengths(triangleCollections[i][j]);

                    (float length, float adjacentLength, (Vector3 a, Vector3 b, Vector3 c) order) edge;
                    if (edgeLengths.ab > edgeLengths.bc && edgeLengths.ab > edgeLengths.ca)
                        edge = (edgeLengths.ab, edgeLengths.ca, (a: vertices.c, b: vertices.a, c: vertices.b));

                    else if (edgeLengths.bc > edgeLengths.ca && edgeLengths.bc > edgeLengths.ab)
                        edge = (edgeLengths.bc, edgeLengths.ab, (a: vertices.a, b: vertices.b, c: vertices.c));

                    else
                        edge = (edgeLengths.ca, edgeLengths.bc, (a: vertices.b, b: vertices.c, c: vertices.a));

                    float aStepSize = 1f / (edge.length < nodeSeparation ? 3f : edge.length / nodeSeparation);
                    float bStepSize = 1f / (edge.adjacentLength < nodeSeparation ? 3f : edge.adjacentLength / nodeSeparation);

                    for (float a = aStepSize; a < 1f; a += aStepSize)
                    {
                        for (float b = bStepSize; b < 1f; b += bStepSize)
                        {
                            var position = triangleCollections[i].PointInsideNaive(edge.order.a, edge.order.b, edge.order.c, a, b);
                            position = SurfacePosition(position);
                            nodePoints.Add(position);
                        }
                    }
                }

            foreach (var position in nodePoints)
                TryPoint(position);
        }

        void TryPoint(Vector3 position)
        {
            Profiler.BeginSample("Evaluate position fit");

            var qRadius = QueenHull.radius * 1.25f;
            var gRadius = GolemHull.radius * 1.25f;
            var hRadius = HumanHull.radius * 1.25f;
            var qOffset = qRadius * Vector3.up;
            var gOffset = gRadius * Vector3.up;
            var hOffset = hRadius * Vector3.up;
            var testPosition = position + Vector3.up;
            var mask = HullMask.None;

            if (Physics.OverlapCapsuleNonAlloc(testPosition + qOffset, position + QueenHeightOffset - qOffset, qRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)qRadius, QueenHull.height, QueenHull.radius / 2))
                mask = AllHullsMask;
            else
            if (Physics.OverlapCapsuleNonAlloc(testPosition + gOffset, position + GolemHeightOffset - gOffset, gRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)gRadius, GolemHull.height, GolemHull.radius / 2))
                mask = AllHullsMask ^ HullMask.BeetleQueen;
            else
            if (Physics.OverlapCapsuleNonAlloc(testPosition + hOffset, position + HumanHeightOffset - hOffset, hRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)hRadius, HumanHull.height, HumanHull.radius / 2))
                mask = HullMask.Human;

            Profiler.EndSample();

            if (!mask.HasFlag(HullMask.Human))
                return;

            var teleporterOk = TestTeleporterOK(position);
            var upHit = Physics.RaycastNonAlloc(new Ray(position, Vector3.up), hitArray, 50, LayerIndex.enemyBody.collisionMask) > 0;
            var flags = (teleporterOk ? NodeFlags.TeleporterOK : NodeFlags.None) | (!upHit ? NodeFlags.NoCeiling : NodeFlags.None);

            Profiler.BeginSample("Store point");
            GroundNodes.Add(new Node
            {
                flags = flags,
                position = position,
                forbiddenHulls = AllHullsMask ^ mask,
            });
            Profiler.EndSample();
        }

        public void UpdateTriangleCollections()
        {
            Profiler.BeginSample("Find walkable triangles");
            triangleCollections = new TriangleCollection[meshFilters.Length];
            meshes = new Mesh[meshFilters.Length];
            var probes = FindObjectsOfType<NavigationProbe>();
            for (int i = 0; i < meshFilters.Length; i++)
            {
                var filter = meshFilters[i];
                var mesh = filter.sharedMesh;
                var borderProbes = probes.Where(probe => probe.meshFilters.Contains(filter)).ToArray();
                var collection = new TriangleCollection(mesh.vertices.Select(v => filter.transform.TransformPoint(v)).ToArray(), mesh.triangles);
                var upwardTriangles = collection.WithNormal(Vector3.up, marginFromUp);
                var upTrisWithLOS = upwardTriangles.Where(triangle =>
                {
                    try
                    {
                        Profiler.BeginSample("Prepare Triangle Data");
                        var vertices = collection.Vertices(triangle);
                        if (TestVertex(vertices.a, transform.position, distance)
                          || TestVertex(vertices.b, transform.position, distance)
                          || TestVertex(vertices.c, transform.position, distance))
                            return true;
                    }
                    finally
                    {
                        Profiler.EndSample();
                    }
                    return false;
                });
                collection = collection.GetDetached(upTrisWithLOS);

                triangleCollections[i] = collection;
                meshes[i] = collection.ToMesh();
                var colors = new Color[meshes[i].vertices.Length];
                for (int j = 0; j < meshes[i].vertices.Length; j++)
                    colors[j] = new Color(0, 1, 1, 0.25f);
                meshes[i].colors = colors;
                meshes[i].name = $"{mesh.name} (Vector3.Dot(up, faceNormal) > 1-{marginFromUp})";
            }
            Profiler.EndSample();
        }
        private Vector3 SurfacePosition(Vector3 position)
        {
            Profiler.BeginSample("Surfacing Position");
            if (Physics.RaycastNonAlloc(position, Vector3.up, hitArray, QueenHull.height, LayerIndex.world.mask) == 0)
            {
                if (Physics.RaycastNonAlloc(position + QueenHeightOffset, Vector3.down, hitArray, QueenHull.height, LayerIndex.world.mask) > 0)
                    position = hitArray[0].point;
            }
            else
            if (Physics.RaycastNonAlloc(position, Vector3.up, hitArray, GolemHull.height, LayerIndex.world.mask) == 0)
            {
                if (Physics.RaycastNonAlloc(position + GolemHeightOffset, Vector3.down, hitArray, GolemHull.height, LayerIndex.world.mask) > 0)
                    position = hitArray[0].point;
            }
            else
            if (Physics.RaycastNonAlloc(position, Vector3.up, hitArray, HumanHull.height, LayerIndex.world.mask) == 0)
            {
                if (Physics.RaycastNonAlloc(position + HumanHeightOffset, Vector3.down, hitArray, HumanHull.height, LayerIndex.world.mask) > 0)
                    position = hitArray[0].point;
            }
            Profiler.EndSample();
            return position;
        }

        public bool FootprintFitsPosition(Vector3 position, float radius, float height, float forgiveness)
        {
            int steps = 6;
            float degrees = 360f / (float)steps;
            var direction = Vector3.forward * radius;
            var rotation = Quaternion.AngleAxis(degrees, Vector3.up);
            for (int index = 0; index < steps; ++index)
            {
                direction = rotation * direction;
                var ray = new Ray(position + direction + (Vector3.up * height), Vector3.down);
                if (Physics.RaycastNonAlloc(ray, hitArray, height + forgiveness, LayerIndex.enemyBody.collisionMask) == 0)
                    return false;
            }
            return true;
        }

        bool TestTeleporterOK(Vector3 position)
        {
            float radius = 15f;
            if (Physics.OverlapSphereNonAlloc(position + Vector3.up * (radius + 1), radius, colliders) > 0)
                return false;

            return FootprintFitsPosition(position, 10, 7f, 2);
        }

        bool TestVertex(Vector3 vertex, Vector3 probePosition, float maxDistance)
        {
            float distance = Vector3.Distance(vertex, probePosition);
            if (distance < maxDistance)
                if (Physics.RaycastNonAlloc(probePosition, (vertex - probePosition).normalized, hitArray, distance - 0.1f, LayerIndex.enemyBody.collisionMask) == 0)
                    return true;
            return false;
        }
        #endregion
    }
}