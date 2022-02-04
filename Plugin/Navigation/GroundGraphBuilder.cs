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

        public int seed = -1;
        public MeshFilter[] meshFilters;
        public int pointCount = 1000;
        public Vector3 TargetNormal = Vector3.up;
        public float Margin = 0.4f;
        public float nodeSeparation = 8;
        public float linkDistance = 16;
        public float floorForgiveness = 1f;

        [SerializeField, HideInInspector] private float lastMargin;
        [SerializeField, HideInInspector] private Vector3 lastTargetNormal;

        [SerializeField, HideInInspector]
        private TriangleCollection TriangleCollection;

        public override void Build()
        {
            InitializeSeed(seed);
            var nodes = new List<Node>();
            var links = new List<Link>();
            var pointTree = new KDTree();
            var query = new KDQuery();
            var nodePoints = new List<Vector3>();

            var probes = FindObjectsOfType<NavigationProbe>()
                .Where(np => np.isActiveAndEnabled)
                .ToArray();
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
                    linkListIndex = new LinkListIndex { index = links.Count, size = (uint)staticNode.HardLinks.Length }
                };
                nodes.Add(item);
            }

            foreach (var filter in meshFilters)
            {
                var mesh = filter.sharedMesh;
                TriangleCollection = new TriangleCollection(mesh.vertices, mesh.triangles);

                Profiler.BeginSample("Find walkable triangles");
                var walkableTris = TriangleCollection.WithNormal(TargetNormal, Margin).OrderByDescending(tri => (int)TriangleCollection.Area(tri)).ThenBy(tri => Random.value).ToList();
                Profiler.EndSample();

                if (walkableTris.Any())
                    GenerateNodeData(filter.transform, nodePoints, nodes, walkableTris, pointTree, query, probes);
            }

            LinkNodes(nodes, links, pointTree, query, staticNodes);
            Apply(nodeGraphAssetField, $"{gameObject.scene.name}_GroundNodeGraph.asset", nodes, links);
        }

        private void GenerateNodeData(Transform transform, List<Vector3> nodePoints, List<Node> nodes, List<Triangle> walkableTris, KDTree pointTree, KDQuery query, NavigationProbe[] probes)
        {
            int failures = 0;
            var nodeArea = Mathf.PI * (nodeSeparation * nodeSeparation);
            while (nodePoints.Count < pointCount && failures < pointCount * 4 && walkableTris.Count > 0)
            {
                var triangle = walkableTris[0];
                walkableTris.RemoveAt(0);
                var area = Mathf.Max(1, TriangleCollection.Area(triangle) / nodeArea);
                for (int i = 0; i < area; i++)
                {
                    Profiler.BeginSample("Select random position in triangle");
                    var position = transform.TransformPoint(TriangleCollection.PointInside(triangle));
                    Profiler.EndSample();
                    position = SurfacePosition(position);

                    Profiler.BeginSample("Evaluate separation against kdtree");
                    if (pointTree.RootNode != null)
                    {
                        resultsIndices.Clear();
                        Profiler.BeginSample("Query point");
                        query.Radius(pointTree, position, nodeSeparation, resultsIndices);
                        Profiler.EndSample();
                        if (resultsIndices.Any())
                        {
                            failures++;
                            Profiler.EndSample();
                            continue;
                        }
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample("Line of Sight to any probe");
                    var validPosition = true;
                    for (int j = 0; j < probes.Length; j++)
                    {
                        var probe = probes[j];
                        var probePosition = probe.transform.position;
                        var distanceToProbe = Vector3.Distance(probePosition, position);
                        var direction = (probePosition - (position + HumanHeightOffset / 2)).normalized;
                        if (distanceToProbe < probe.distance)
                            if (Physics.RaycastNonAlloc(position, direction, hitArray, distanceToProbe) == 0)
                                if (Physics.RaycastNonAlloc(probePosition, -direction, hitArray, distanceToProbe) == 0)
                                    break;

                        if (j == probes.Length - 1)
                            validPosition = false;
                    }
                    Profiler.EndSample();
                    if (!validPosition)
                    {
                        failures++;
                        continue;
                    }

                    Profiler.BeginSample("Evaluate random position fit");

                    var testPosition = position + Vector3.up;
                    var mask = HullMask.None;

                    if (Physics.OverlapSphereNonAlloc(testPosition + (QueenHeightOffset / 2), QueenHeight / 2, colliders, LayerIndex.world.mask) == 0
                     && FootprintFitsPosition(position, QueenHull.radius, QueenHull.height))
                        mask = AllHulls;
                    else
                    if (Physics.OverlapSphereNonAlloc(testPosition + (GolemHeightOffset / 2), GolemHeight / 2, colliders, LayerIndex.world.mask) == 0
                     && FootprintFitsPosition(position, GolemHull.radius, GolemHull.height))
                        mask = AllHulls ^ HullMask.BeetleQueen;
                    else
                    if (Physics.OverlapSphereNonAlloc(testPosition + (HumanHeightOffset / 2), HumanHeight / 2, colliders, LayerIndex.world.mask) == 0
                     && FootprintFitsPosition(position, HumanHull.radius, HumanHull.height))
                        mask = HullMask.Human;

                    Profiler.EndSample();

                    if (!mask.HasFlag(HullMask.Human))
                    {
                        failures++;
                        continue;
                    }

                    var teleporterOk = TestTeleporterOK(position);
                    var upHit = Physics.RaycastNonAlloc(new Ray(position, Vector3.up), hitArray, 50, LayerIndex.world.mask) > 0;
                    var flags = (teleporterOk ? NodeFlags.TeleporterOK : NodeFlags.None) | (!upHit ? NodeFlags.NoCeiling : NodeFlags.None);


                    Profiler.BeginSample("Store point");
                    nodePoints.Add(position);
                    nodes.Add(new Node
                    {
                        flags = flags,
                        position = position,
                        forbiddenHulls = AllHulls ^ mask,
                    });
                    Profiler.EndSample();

                    Profiler.BeginSample("Update kdtree");
                    pointTree.Build(nodePoints);
                    Profiler.EndSample();
                }
            }
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
            var resultsIndices = new List<int>();
            uint linkCount = 0;
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
                        links.Add(new Link
                        {
                            nodeIndexA = new NodeIndex(staticNodes.IndexOf(staticNode)),
                            nodeIndexB = new NodeIndex(staticNodes.IndexOf(destinationNode)),
                            distanceScore = staticNode.distanceScore,
                            hullMask = (int)(AllHulls ^ (destinationNode.forbiddenHulls | staticNode.forbiddenHulls)),
                            jumpHullMask = (int)(AllHulls ^ (destinationNode.forbiddenHulls | staticNode.forbiddenHulls)),
                        });
                    }
                    if (!staticNode.allowDynamicConnections || !staticNode.allowOutboundConnections)
                        continue;
                }

                resultsIndices.Clear();
                //Find nodes within link range
                query.Radius(pointTree, nodes[i].position, linkDistance, resultsIndices);
                resultsIndices.Remove(i);
                int skipped = 0;
                foreach (var nni in resultsIndices)
                {
                    var otherNode = nodes[nni];

                    if (nni < staticNodes.Count)
                    {
                        var staticNode = staticNodes[nni];
                        if (!staticNode.allowDynamicConnections || !staticNode.allowInboundConnections)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    var maxDist = Vector3.Distance(node.position, otherNode.position);
                    Vector3 direction = (otherNode.position - node.position).normalized;

                    var testStart = node.position + Vector3.up * HumanHeight;
                    var testStop = otherNode.position + Vector3.up * HumanHeight;
                    var isValid = true;
                    for (float tf = 0; tf <= 1f; tf += 1f / 5f)
                    {
                        var testPosition = Vector3.Lerp(testStart, testStop, tf);
                        if (Physics.RaycastNonAlloc(testPosition, Vector3.down, hitArray, HumanHeight * 2 + 1) == 0)
                        {
                            isValid = false;
                            break;
                        }
                    }
                    if (Physics.SphereCastNonAlloc(testStart, HumanHull.radius, direction, hitArray, maxDist) > 0)
                        isValid = false;

                    if (Physics.SphereCastNonAlloc(testStop, HumanHull.radius, -direction, hitArray, maxDist) > 0)
                        isValid = false;

                    if (!isValid)
                    {
                        skipped++;
                        continue;
                    }

                    var mask = (AllHulls ^ (otherNode.forbiddenHulls | node.forbiddenHulls));
                    links.Add(new Link
                    {
                        distanceScore = Mathf.Sqrt((otherNode.position - node.position).sqrMagnitude),
                        nodeIndexA = new NodeIndex(i),
                        nodeIndexB = new NodeIndex(nni),
                        hullMask = (int)mask,
                        jumpHullMask = (int)mask,
                        minJumpHeight = 0,
                        maxSlope = 90,
                        gateIndex = (byte)otherNode.gateIndex
                    });
                }
            }

            int linkIndex = 0;
            links = links.OrderBy(l => l.nodeIndexA.nodeIndex).ToList();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                uint size = (uint)links.Count(l => l.nodeIndexA.nodeIndex == i);
                node.linkListIndex = new LinkListIndex { index = linkIndex, size = size };
                linkIndex += (int)size;
                nodes[i] = node;
            }
            Profiler.EndSample();
        }

        public bool FootprintFitsPosition(Vector3 position, float radius, float height)
        {
            int steps = footprintSteps;
            float degrees = 360f / (float)steps;
            var direction = Vector3.forward * radius;
            var rotation = Quaternion.AngleAxis(degrees, Vector3.up);
            for (int index = 0; index < steps; ++index)
            {
                direction = rotation * direction;
                var ray = new Ray(position + direction + (Vector3.up * height * 0.5F), Vector3.down);
                if (Physics.RaycastNonAlloc(ray, hitArray, height + (1 + floorForgiveness), LayerIndex.world.mask) == 0)
                    return false;
            }
            return true;
        }

        bool TestTeleporterOK(Vector3 position)
        {
            float radius = 15f;
            if (Physics.OverlapSphereNonAlloc(position + Vector3.up * (radius + 1), radius, colliders) > 0)
                return false;

            return FootprintFitsPosition(position, 10, 7f);
        }
    }
}