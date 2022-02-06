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

        public MeshFilter[] meshFilters;
        public float nodeSeparation = 8;
        public float linkDistance = 16;

        [SerializeField, HideInInspector] private float lastMargin;
        [SerializeField, HideInInspector] private Vector3 lastTargetNormal;

        [SerializeField, HideInInspector]
        private TriangleCollection TriangleCollection;

        public override void Build()
        {
            var nodes = new List<Node>();
            var links = new List<Link>();
            var pointTree = new KDTree();
            pointTree.SetCount(1000);
            pointTree.Rebuild();
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
                var walkableTris = TriangleCollection.WithNormal(Vector3.up, 0.4f).OrderByDescending(tri => (int)TriangleCollection.Area(tri)).ToList();
                Profiler.EndSample();

                if (walkableTris.Any())
                    GenerateNodeData(filter.transform, nodePoints, nodes, walkableTris, pointTree, query, probes);
            }

            LinkNodes(nodes, links, pointTree, query, staticNodes);
            Apply(nodeGraphAssetField, $"{gameObject.scene.name}_GroundNodeGraph.asset", nodes, links);
        }

        private void GenerateNodeData(Transform transform, List<Vector3> nodePoints, List<Node> nodes, List<Triangle> walkableTris, KDTree pointTree, KDQuery query, NavigationProbe[] probes)
        {
            foreach (var walkable in walkableTris)
            {
                Profiler.BeginSample("Prepare Triangle Data");
                var vertices = TriangleCollection.Vertices(walkable);
                var edgeLengths = TriangleCollection.EdgeLengths(walkable);

                Profiler.EndSample();

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
                        TryPoint(position, transform, pointTree, query, probes, nodePoints, nodes);
                    }
                }
            }

        }

        void TryPoint(Vector3 position, Transform transform, KDTree pointTree, KDQuery query, NavigationProbe[] probes, List<Vector3> nodePoints, List<Node> nodes)
        {
            position = transform.TransformPoint(position);
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
                    Profiler.EndSample();
                    return;
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
                return;

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
             && FootprintFitsPosition(position, (float)qRadius, QueenHull.height, 3f))
                mask = AllHullsMask;
            else
            if (Physics.OverlapCapsuleNonAlloc(testPosition + gOffset, position + GolemHeightOffset - gOffset, gRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)gRadius, GolemHull.height, 1f))
                mask = AllHullsMask ^ HullMask.BeetleQueen;
            else
            if (Physics.OverlapCapsuleNonAlloc(testPosition + hOffset, position + HumanHeightOffset - hOffset, hRadius, colliders, LayerIndex.enemyBody.collisionMask) == 0
             && FootprintFitsPosition(position, (float)hRadius, HumanHull.height, 0.2f))
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
            var resultsIndices = new List<int>();
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
                            hullMask = (int)(AllHullsMask ^ (destinationNode.forbiddenHulls | staticNode.forbiddenHulls)),
                            jumpHullMask = (int)(AllHullsMask ^ (destinationNode.forbiddenHulls | staticNode.forbiddenHulls)),
                        });
                    }
                    if (!staticNode.allowDynamicConnections || !staticNode.allowOutboundConnections)
                        continue;
                }

                resultsIndices.Clear();
                //Find nodes within link range
                query.Radius(pointTree, nodes[i].position, linkDistance, resultsIndices);
                resultsIndices.Remove(i);
                foreach (var nni in resultsIndices)
                {
                    var otherNode = nodes[nni];

                    if (nni < staticNodes.Count && (!staticNodes[nni].allowDynamicConnections || !staticNodes[nni].allowInboundConnections))
                        continue;

                    var maxDist = Vector3.Distance(node.position, otherNode.position);
                    Vector3 direction = (otherNode.position - node.position).normalized;

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
                        {
                            links.Add(new Link
                            {
                                distanceScore = (otherNode.position - node.position).magnitude,
                                nodeIndexA = new NodeIndex(i),
                                nodeIndexB = new NodeIndex(nni),
                                hullMask = (int)hullMask,
                                jumpHullMask = (int)hullMask,
                                minJumpHeight = 0,
                                maxSlope = 90,
                                gateIndex = (byte)otherNode.gateIndex
                            });
                        }
                    }
                    //if (CheckHull(node, otherNode, maxDist, direction, HumanHull))
                    //{
                    //    var mask = (AllHullsMask ^ (otherNode.forbiddenHulls | node.forbiddenHulls));
                    //    links.Add(new Link
                    //    {
                    //        distanceScore = (otherNode.position - node.position).magnitude,
                    //        nodeIndexA = new NodeIndex(i),
                    //        nodeIndexB = new NodeIndex(nni),
                    //        hullMask = (int)mask,
                    //        jumpHullMask = (int)mask,
                    //        minJumpHeight = 0,
                    //        maxSlope = 90,
                    //        gateIndex = (byte)otherNode.gateIndex
                    //    });
                    //}
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

        private bool CheckHull(Node node, Node otherNode, float maxDist, Vector3 direction, HullDef hull)
        {
            float height = hull.height;
            float radius = hull.radius;
            var testStart = node.position + Vector3.up * height;
            var testStop = otherNode.position + Vector3.up * height;
            var isValid = true;
            for (float tf = 0; tf <= 1f; tf += 1f / 5f)
            {
                var testPosition = Vector3.Lerp(testStart, testStop, tf);
                if (Physics.RaycastNonAlloc(testPosition, Vector3.down, hitArray, height * 1.5f) == 0)
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid && Physics.SphereCastNonAlloc(node.position + (Vector3.up * (radius + 0.5f)), radius, direction, hitArray, maxDist) > 0)
                isValid = false;

            if (isValid && Physics.SphereCastNonAlloc(otherNode.position + (Vector3.up * (radius + 0.5f)), radius, -direction, hitArray, maxDist) > 0)
                isValid = false;
            return isValid;
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
    }
}