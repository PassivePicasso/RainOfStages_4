using DataStructures.ViliWonka.KDTree;
using RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using static RoR2.Navigation.NodeGraph;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class GroundGraphBuilder : GraphBuilder
    {
        public static System.Reflection.FieldInfo nodeGraphAssetField =
            typeof(SceneInfo).GetField($"groundNodesAsset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private int footprintSteps = 6;

        public float marginFromUp = 0.5f;

        [SerializeField, HideInInspector] private float lastMargin;
        [SerializeField, HideInInspector] private Vector3 lastTargetNormal;

        [SerializeField, HideInInspector]
        private TriangleCollection TriangleCollection;
        [SerializeField, HideInInspector] public Mesh mesh;// { get; private set; }
        private MeshFilter[] meshFilters;

        private void Update()
        {
            var update = false;
            for (int i = 0; i < NavigationProbe.ActiveProbes.Count; i++)
            {
                if (NavigationProbe.ActiveProbes[i].IsDirty)
                    update = true;
                NavigationProbe.ActiveProbes[i].IsDirty = false;
            }
            if (update)
                UpdateTriangleCollections();
        }

        protected override void OnBuild()
        {
            var nodes = new List<Node>(1500);
            var links = new List<Link>(3000);
            var pointTree = new KDTree();
            pointTree.SetCount(1000);
            pointTree.Rebuild();
            var query = new KDQuery();
            var nodePoints = new List<Vector3>();

            var staticNodes = StaticNode.StaticNodes;
            for (int i = 0; i < staticNodes.Count; i++)
            {
                var staticNode = staticNodes[i];
                var position = SurfacePosition(staticNode.position);
                nodePoints.Add(position);
                Node item = new Node
                {
                    position = position,
                    flags = staticNode.nodeFlags,
                    forbiddenHulls = staticNode.forbiddenHulls,
                };
                nodes.Add(item);
            }
            UpdateTriangleCollections();

            foreach (var walkable in TriangleCollection)
            {
                var vertices = TriangleCollection.Vertices(walkable);
                var edgeLengths = TriangleCollection.EdgeLengths(walkable);

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
                        var position = TriangleCollection.PointInsideNaive(edge.order.a, edge.order.b, edge.order.c, a, b);
                        TryPoint(position, pointTree, query, nodePoints, nodes);
                    }
                }
            }


            LinkNodes(nodes, links, pointTree, query, staticNodes);
            Apply(nodeGraphAssetField, $"{gameObject.scene.name}_GroundNodeGraph.asset", nodes, links);
        }

        void TryPoint(Vector3 position, KDTree pointTree, KDQuery query, List<Vector3> nodePoints, List<Node> nodes)
        {

            Profiler.BeginSample("Evaluate separation against kdtree");
            if (pointTree.RootNode != null)
            {
                resultsIndices.Clear();
                Profiler.BeginSample("Query point");
                query.Radius(pointTree, position, nodeSeparation, resultsIndices);
                Profiler.EndSample();
                if (resultsIndices.Any())
                {
                    Profiler.EndSample();
                    return;
                }
            }
            Profiler.EndSample();

            position = SurfacePosition(position);

            Profiler.BeginSample("Line of Sight to any probe");
            var failed = true;
            for (int probeIndex = 0; probeIndex < NavigationProbe.ActiveProbes.Count; probeIndex++)
            {
                var probe = NavigationProbe.ActiveProbes[probeIndex];
                var probePosition = probe.transform.position;
                var distanceToProbe = Vector3.Distance(probePosition, position);
                var direction = (probePosition - (position + HumanHeightOffset / 2)).normalized;
                if (distanceToProbe < probe.distance)
                    if (Physics.RaycastNonAlloc(position, direction, hitArray, distanceToProbe) == 0)
                        if (Physics.RaycastNonAlloc(probePosition, -direction, hitArray, distanceToProbe) == 0)
                        {
                            failed = false;
                            break;
                        }
            }
            Profiler.EndSample();
            if (failed)
                return;

            Profiler.BeginSample("Evaluate position fit");

            var qRadius = QueenHull.radius * 1.35f;
            var gRadius = GolemHull.radius * 1.5f;
            var hRadius = HumanHull.radius * 1.5f;
            var qOffset = qRadius * Vector3.up;
            var gOffset = gRadius * Vector3.up;
            var hOffset = hRadius * Vector3.up;
            var testPosition = position + Vector3.up;
            var mask = HullMask.None;

            if (Physics.OverlapCapsuleNonAlloc(testPosition + qOffset, position + QueenHeightOffset - qOffset, qRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)qRadius, QueenHull.height, qRadius))
                mask = AllHullsMask;
            else
            if (Physics.OverlapCapsuleNonAlloc(testPosition + gOffset, position + GolemHeightOffset - gOffset, gRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)gRadius, GolemHull.height, gRadius))
                mask = AllHullsMask ^ HullMask.BeetleQueen;
            else
            if (Physics.OverlapCapsuleNonAlloc(testPosition + hOffset, position + HumanHeightOffset - hOffset, hRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)hRadius, HumanHull.height, gRadius))
                mask = HullMask.Human;

            Profiler.EndSample();

            if (!mask.HasFlag(HullMask.Human))
                return;

            var teleporterOk = TestTeleporterOK(position);
            var upHit = Physics.RaycastNonAlloc(new Ray(position, Vector3.up), hitArray, 50, LayerIndex.enemyBody.collisionMask) > 0;
            var flags = (teleporterOk ? NodeFlags.TeleporterOK : NodeFlags.None) | (!upHit ? NodeFlags.NoCeiling : NodeFlags.None);

            Profiler.BeginSample("Store point");
            nodePoints.Add(position);
            nodes.Add(new Node
            {
                flags = flags,
                position = position,
                forbiddenHulls = AllHullsMask ^ mask,
            });
            Profiler.EndSample();

            Profiler.BeginSample("Update kdtree");
            pointTree.Build(nodePoints);
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

        public void LinkNodes(List<Node> nodes, List<Link> links, KDTree pointTree, KDQuery query, List<StaticNode> staticNodes)
        {
            var resultsIndices = new List<int>(nodes.Count);
            Profiler.BeginSample("Construct node links");
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (i < staticNodes.Count)
                {
                    var staticNode = staticNodes[i];
                    for (int j = 0; j < staticNode.HardLinks.Length; j++)
                    {
                        var destinationNode = staticNode.HardLinks[j];
                        Profiler.BeginSample("Create Link");
                        links.Add(new Link
                        {
                            nodeIndexA = new NodeIndex(staticNodes.IndexOf(staticNode)),
                            nodeIndexB = new NodeIndex(staticNodes.IndexOf(destinationNode)),
                            distanceScore = staticNode.overrideDistanceScore ? staticNode.distanceScore : (destinationNode.position - staticNode.position).magnitude,
                            hullMask = (int)(AllHullsMask ^ (destinationNode.forbiddenHulls | staticNode.forbiddenHulls)),
                            jumpHullMask = (int)HullMask.None,
                        });
                        Profiler.EndSample();
                    }
                    if (!staticNode.allowDynamicOutboundConnections)
                        continue;
                }

                Profiler.BeginSample("Find Nodes within link range");
                resultsIndices.Clear();
                //Find nodes within link range
                query.Radius(pointTree, nodes[i].position, nodeSeparation * LinkDistanceMultiplier, resultsIndices);
                resultsIndices.Remove(i);
                Profiler.EndSample();
                Profiler.BeginSample("Evaluate nodes in range for connectivity");
                foreach (var nni in resultsIndices)
                {
                    var otherNode = nodes[nni];

                    if (nni < staticNodes.Count && !staticNodes[nni].allowDynamicInboundConnections)
                        continue;

                    var maxDist = Vector3.Distance(node.position, otherNode.position);
                    Vector3 direction = (otherNode.position - node.position).normalized;
                    var forbiddenLinkHulls = AllHullsMask;
                    foreach (var hullMask in HullMasks)
                    {
                        HullDef hull = default;
                        switch (hullMask)
                        {
                            case HullMask.BeetleQueen:
                                hull = QueenHull;
                                break;
                            case HullMask.Golem:
                                hull = GolemHull;
                                break;
                            case HullMask.Human:
                                hull = HumanHull;
                                break;
                        }
                        bool isValid = CheckHull(node, otherNode, maxDist, direction, hull);
                        if (isValid)
                            forbiddenLinkHulls ^= hullMask;
                    }
                    var linkMask = (AllHullsMask ^ (forbiddenLinkHulls | otherNode.forbiddenHulls));
                    if (linkMask == HullMask.None)
                        continue;
                    Profiler.BeginSample("Create Link");
                    links.Add(new Link
                    {
                        distanceScore = (otherNode.position - node.position).magnitude,
                        nodeIndexA = new NodeIndex(i),
                        nodeIndexB = new NodeIndex(nni),
                        hullMask = (int)linkMask,
                        jumpHullMask = (int)HullMask.None,
                        minJumpHeight = 0,
                        maxSlope = 90,
                        gateIndex = (byte)otherNode.gateIndex
                    });
                    Profiler.EndSample();
                }
                Profiler.EndSample();

            }

            int linkIndex = 0;
            links = links.OrderBy(l => l.nodeIndexA.nodeIndex).ToList();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                uint size = (uint)links.Count(l => l.nodeIndexA.nodeIndex == i);
                if (size == 0 && i >= staticNodes.Count)
                {
                    node.flags = NodeFlags.NoCharacterSpawn | NodeFlags.NoChestSpawn | NodeFlags.NoShrineSpawn;
                    node.forbiddenHulls = AllHullsMask;
                }
                node.linkListIndex = new LinkListIndex { index = linkIndex, size = size };
                linkIndex += (int)size;
                nodes[i] = node;
            }
            Profiler.EndSample();
        }

        private bool CheckHull(Node node, Node otherNode, float maxDist, Vector3 direction, HullDef hull)
        {
            Profiler.BeginSample("Sample Hull");
            try
            {
                float height = hull.height;
                float radius = hull.radius;
                float offset = radius * 1.5f;
                var offsetVector = Vector3.up * (offset + 2);
                var heightOffsetVector = Vector3.up * height;
                var testStart = node.position + offsetVector;
                var testStop = otherNode.position + offsetVector;
                var isValid = true;

                //if (isValid && Physics.CapsuleCastNonAlloc(testStart, testStart + heightOffsetVector - offsetVector, radius, direction, hitArray, maxDist, LayerIndex.enemyBody.collisionMask) > 0)
                //    isValid = false;

                if (isValid)
                    for (float tf = 0; tf <= 1f; tf += 1f / 5f)
                    {
                        var testPosition = Vector3.Lerp(testStart, testStop, tf);
                        if (Physics.SphereCastNonAlloc(testPosition, radius, Vector3.down, hitArray, offset + 3, LayerIndex.enemyBody.collisionMask) == 0)
                        {
                            isValid = false;
                            break;
                        }
                    }

                return isValid;
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        public bool FootprintFitsPosition(Vector3 position, float radius, float height, float forgiveness)
        {
            int steps = footprintSteps;
            float degrees = 360f / (float)steps;
            var direction = Vector3.forward * radius;
            var rotation = Quaternion.AngleAxis(degrees, Vector3.up);
            for (int index = 0; index < steps; ++index)
            {
                direction = rotation * direction;
                var origin = position + direction + (Vector3.up * height);
                if (Physics.RaycastNonAlloc(origin, Vector3.down, hitArray, height + forgiveness, LayerIndex.enemyBody.collisionMask) == 0)
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

        public void UpdateTriangleCollections()
        {
            Profiler.BeginSample("Find walkable triangles");

            Profiler.BeginSample("Collect MeshFilters");
            var probes = NavigationProbe.ActiveProbes;
            var filters = probes.Where(p => p.meshFilters != null).SelectMany(p => p.meshFilters).ToArray(); ;
            meshFilters = filters.Distinct().ToArray();
            Profiler.EndSample();

            Profiler.BeginSample("Collect Vertices and Indices");

            var combines = meshFilters.Select(mf => new CombineInstance { mesh = mf.sharedMesh, transform = mf.transform.localToWorldMatrix }).ToArray();
            var allMesh = new Mesh();
            allMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            allMesh.CombineMeshes(combines);
            var vertices = new List<Vector3>();
            allMesh.GetVertices(vertices);
            var indices = allMesh.GetIndices(0);

            Profiler.EndSample();


            Profiler.BeginSample("Construct TriangleCollection");
            TriangleCollection = new TriangleCollection(vertices, indices);
            Profiler.EndSample();

            var upwardTriangles = TriangleCollection.WithNormal(Vector3.up, marginFromUp);
            var upTrisWithLOS = upwardTriangles.Where(triangle =>
            {
                var triVerts = TriangleCollection.Vertices(triangle);
                foreach (var probe in NavigationProbe.ActiveProbes)
                {
                    var seen = TestVertex(triVerts.a, probe.transform.position, probe.distance)
                            || TestVertex(triVerts.b, probe.transform.position, probe.distance)
                            || TestVertex(triVerts.c, probe.transform.position, probe.distance);
                    if (seen)
                        return true;
                }
                return false;
            });

            Profiler.BeginSample("Detach Found Triangles");
            TriangleCollection = TriangleCollection.GetDetached(upTrisWithLOS);
            Profiler.EndSample();

            Profiler.BeginSample("Create Preview Mesh");
            mesh = TriangleCollection.ToMesh();
            mesh.name = $"(Vector3.Dot(up, faceNormal) > 1-{marginFromUp})";
            Profiler.EndSample();
            DestroyImmediate(allMesh);
            Profiler.EndSample();
        }

        bool TestVertex(Vector3 vertex, Vector3 probePosition, float maxDistance)
        {
            float distance = Vector3.Distance(vertex, probePosition);
            if (distance < maxDistance)
                if (Physics.RaycastNonAlloc(probePosition, (vertex - probePosition).normalized, hitArray, distance - 0.1f, LayerIndex.enemyBody.collisionMask) == 0)
                    return true;
            return false;
        }
    }
}