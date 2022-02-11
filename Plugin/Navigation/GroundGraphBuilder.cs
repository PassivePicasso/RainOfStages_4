using DataStructures.ViliWonka.KDTree;
using RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using static PassivePicasso.RainOfStages.Plugin.Utilities.HullHelper;
using static PassivePicasso.RainOfStages.Plugin.Utilities.PhysicsHelper;
using static RoR2.Navigation.NodeGraph;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class GroundGraphBuilder : GraphBuilder
    {
        public static System.Reflection.FieldInfo nodeGraphAssetField =
            typeof(SceneInfo).GetField($"groundNodesAsset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public float nodeSeparation = 8;
        public float marginFromUp = 0.5f;

        [SerializeField, HideInInspector] private float lastMargin;
        [SerializeField, HideInInspector] private Vector3 lastTargetNormal;

        protected override void OnBuild()
        {
            var addedIndices = new HashSet<int>();
            var blackList = new HashSet<int>();
            var nodes = new List<Node>();
            var links = new List<Link>();
            var pointTree = new KDTree();
            var query = new KDQuery();

            var probes = FindObjectsOfType<NavigationProbe>()
                .Where(np => np.isActiveAndEnabled)
                .ToArray();
            var staticNodes = StaticNode.StaticNodes;
            for (int i = 0; i < staticNodes.Count; i++)
            {
                var staticNode = staticNodes[i];
                var position = SurfacePosition(staticNode.position);
                Node item = new Node
                {
                    position = position,
                    flags = staticNode.nodeFlags,
                    forbiddenHulls = staticNode.forbiddenHulls,
                    linkListIndex = new LinkListIndex { index = links.Count, size = (uint)staticNode.HardLinks.Length }
                };
                nodes.Add(item);
            }

            Profiler.BeginSample("Get Points");
            var groundNodes = probes.SelectMany(p => p.GroundNodes).ToArray();
            int mapSize = nodes.Count + groundNodes.Length;
            var expandedProbes = new NavigationProbe[mapSize];
            var nodePoints = new Vector3[mapSize];

            for (int i = 0; i < staticNodes.Count; i++)
            {
                expandedProbes[i] = probes.OrderBy(probe => Vector3.Distance(staticNodes[i].position, probe.transform.position)).First();
                nodePoints[i] = staticNodes[i].position;
            }

            int index = 0;
            for (int i = 0; i < probes.Length; i++)
            {
                for (int k = 0; k < probes[i].GroundNodes.Count; k++)
                {
                    expandedProbes[index] = probes[i];
                    nodePoints[index] = probes[i].GroundNodes[k].position;
                    index++;
                }
            }

            nodes.AddRange(groundNodes);
            Profiler.BeginSample("Update KDTree");
            pointTree.Build(nodePoints, 64);
            Profiler.EndSample();

            Profiler.EndSample();

            var probeNodes = new List<(Node, NavigationProbe)>();
            Profiler.BeginSample("Evaluate separation against KDTree");

            for (int i = 0; i < nodes.Count; i++)
                if (pointTree.RootNode != null)
                {
                    if (i < staticNodes.Count)
                    {
                        addedIndices.Add(i);
                        continue;
                    }
                    resultsIndices.Clear();
                    Profiler.BeginSample("Query point");
                    query.Radius(pointTree, nodePoints[i], expandedProbes[i].nodeSeparation, resultsIndices, whitelist: addedIndices);
                    Profiler.EndSample();
                    if (resultsIndices.Count > 0)
                    {
                        continue;
                    }
                    addedIndices.Add(i);
                }
            Profiler.EndSample();


            Profiler.BeginSample("Build Final Data Sets");
            nodes = addedIndices.Select(i => nodes[i]).ToList();
            expandedProbes = addedIndices.Select(i => expandedProbes[i]).ToArray();
            pointTree.Build(nodes.Select(n => n.position).ToArray());
            Profiler.EndSample();

            LinkNodes(nodes, links, expandedProbes, pointTree, query, staticNodes);
            Apply(nodeGraphAssetField, $"{gameObject.scene.name}_GroundNodeGraph.asset", nodes, links);
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

        public void LinkNodes(List<Node> nodes, List<Link> links, NavigationProbe[] probes, KDTree pointTree, KDQuery query, List<StaticNode> staticNodes)
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
                query.Radius(pointTree, nodes[i].position, probes[i].nodeSeparation * 2.5f, resultsIndices);
                resultsIndices.Remove(i);
                foreach (var nni in resultsIndices)
                {
                    var otherNode = nodes[nni];

                    if (nni < staticNodes.Count && (!staticNodes[nni].allowDynamicConnections || !staticNodes[nni].allowInboundConnections))
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
                    links.Add(new Link
                    {
                        distanceScore = (otherNode.position - node.position).magnitude,
                        nodeIndexA = new NodeIndex(i),
                        nodeIndexB = new NodeIndex(nni),
                        hullMask = (int)linkMask,
                        jumpHullMask = (int)linkMask,
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

        private bool CheckHull(Node node, Node otherNode, float maxDist, Vector3 direction, HullDef hull)
        {
            float height = hull.height;
            float radius = hull.radius;
            float offset = radius * 1.5f;
            var offsetVector = Vector3.up * offset;
            var heightOffsetVector = Vector3.up * height;
            var testStart = node.position + offsetVector;
            var testStop = otherNode.position + offsetVector;
            var isValid = true;
            for (float tf = 0; tf <= 1f; tf += 1f / 5f)
            {
                var testPosition = Vector3.Lerp(testStart, testStop, tf);
                if (Physics.RaycastNonAlloc(testPosition, Vector3.down, hitArray, offset * 1.2f) == 0)
                {
                    isValid = false;
                    break;
                }
            }

            if (isValid && Physics.CapsuleCastNonAlloc(testStart, testStart + heightOffsetVector - offsetVector, radius, direction, hitArray, maxDist) > 0)
                isValid = false;

            return isValid;
        }

    }
}