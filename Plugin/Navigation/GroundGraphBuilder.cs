#if UNITY_EDITOR
using UnityEditor;
#endif
using DataStructures.ViliWonka.KDTree;
using RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
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
        public bool rebuild;
        public MeshFilter[] meshFilters;
        public int pointCount = 1000;
        public Vector3 TargetNormal;
        public float Margin;
        public float nodeSeparation;
        public float linkDistance;
        public float floorForgiveness = 0f;
        public bool surfaceNodePositions;

        [SerializeField, HideInInspector] private float lastMargin;
        [SerializeField, HideInInspector] private Vector3 lastTargetNormal;

        [SerializeField, HideInInspector]
        private TriangleCollection TriangleCollection;

        private void OnDrawGizmos()
        {
            if (Event.current.type == EventType.MouseUp)
            {
                rebuild = true;
            }
        }

        private void Update()
        {
            if (seed == -1)
            {
                Random.InitState((int)Time.realtimeSinceStartup);
            }
            else
            {
                Random.InitState(seed);
            }
            if (rebuild)
            {
                var nodes = new List<Node>();
                var links = new List<Link>();
                rebuild = false;
                var pointTree = new KDTree();
                var query = new KDQuery();
                pointTree.SetCount(pointCount);
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
                        GenerateNodeData(filter.transform, nodePoints, nodes, walkableTris, pointTree, query);
                }

                LinkNodes(nodes, links, pointTree, query, staticNodes);
                SaveGraph(nodes, links);
            }
            if (lastMargin != Margin || lastTargetNormal != TargetNormal)
            {
                lastMargin = Margin;
                lastTargetNormal = TargetNormal;
                rebuild = true;
            }
        }

        private void GenerateNodeData(Transform transform, List<Vector3> nodePoints, List<Node> nodes, List<Triangle> walkableTris, KDTree pointTree, KDQuery query)
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
            if (!surfaceNodePositions) return position;
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
            int nextLinkSetIndex = 0;
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
                    {
                        linkCount = (uint)Mathf.Max(0, links.Count - nextLinkSetIndex);
                        node.linkListIndex = new LinkListIndex { index = nextLinkSetIndex, size = linkCount };
                        nodes[i] = node;
                        nextLinkSetIndex += (int)linkCount;
                        continue;
                    }
                }

                resultsIndices.Clear();
                //Find nodes within link range
                query.Radius(pointTree, nodes[i].position, linkDistance, resultsIndices);
                resultsIndices.Remove(i);
                int skipped = 0;
                foreach (var nni in resultsIndices)
                {
                    var a = nodes[i];
                    var b = nodes[nni];

                    if (nni < staticNodes.Count)
                    {
                        var staticNode = staticNodes[nni];
                        if (!staticNode.allowDynamicConnections || !staticNode.allowInboundConnections)
                        {
                            skipped++;
                            continue;
                        }
                    }

                    var maxDist = Vector3.Distance(a.position, b.position);
                    Vector3 direction = (b.position - a.position).normalized;

                    var testStart = a.position + Vector3.up * HumanHeight;
                    var testStop = b.position + Vector3.up * HumanHeight;
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

                    var mask = (AllHulls ^ (b.forbiddenHulls | a.forbiddenHulls));
                    links.Add(new Link
                    {
                        distanceScore = Mathf.Sqrt((b.position - a.position).sqrMagnitude),
                        nodeIndexA = new NodeIndex(i),
                        nodeIndexB = new NodeIndex(nni),
                        hullMask = (int)mask,
                        jumpHullMask = (int)mask,
                        minJumpHeight = Mathf.Abs(a.position.y - b.position.y) * 1.25f,
                        maxSlope = 90,
                        gateIndex = (byte)b.gateIndex
                    });

                    nodes[i] = a;
                    nodes[nni] = b;
                }

                linkCount = (uint)Mathf.Max(0, links.Count - nextLinkSetIndex);
                node.linkListIndex = new LinkListIndex { index = nextLinkSetIndex, size = linkCount };

                if (linkCount == 0)
                {
                    node.flags |= NodeFlags.NoCharacterSpawn;
                }
                nodes[i] = node;
                nextLinkSetIndex += (int)linkCount;
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
        private static void SaveGraph(List<Node> nodes, List<Link> links)
        {
            Profiler.BeginSample("Save graph changes");
            var sceneInfo = FindObjectOfType<SceneInfo>();
            var activeScene = SceneManager.GetActiveScene();
            var scenePath = activeScene.path;
            scenePath = System.IO.Path.GetDirectoryName(scenePath);
            var graphName = $"{activeScene.name}_GroundNodeGraph.asset";
#if UNITY_EDITOR
            var nodeGraphPath = System.IO.Path.Combine(scenePath, activeScene.name, graphName);
            var nodeGraph = AssetDatabase.LoadAssetAtPath<NodeGraph>(nodeGraphPath);
#else
            var nodeGraph = (NodeGraph)nodeGraphAssetField.GetValue(sceneInfo);
#endif
            var isNew = false;
            if (!nodeGraph)
            {
                nodeGraph = ScriptableObject.CreateInstance<NodeGraph>();
                nodeGraph.name = graphName;
                isNew = true;
            }

            NodesField.SetValue(nodeGraph, nodes.ToArray());
            LinksField.SetValue(nodeGraph, links.ToArray());
            nodeGraphAssetField.SetValue(sceneInfo, nodeGraph);

#if UNITY_EDITOR
            if (isNew)
            {
                if (!AssetDatabase.IsValidFolder(System.IO.Path.Combine(scenePath, activeScene.name)))
                    AssetDatabase.CreateFolder(scenePath, activeScene.name);

                AssetDatabase.CreateAsset(nodeGraph, nodeGraphPath);
                AssetDatabase.Refresh();
            }
            else
            {
                EditorUtility.SetDirty(nodeGraph);
                var so = new SerializedObject(nodeGraph);
                so.ApplyModifiedProperties();
            }
#endif
            Profiler.EndSample();
        }
    }
}