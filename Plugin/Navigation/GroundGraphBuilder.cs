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
    internal class GroundGraphBuilder : MonoBehaviour
    {
        public static System.Reflection.FieldInfo NodesField =
            typeof(NodeGraph).GetField("nodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static System.Reflection.FieldInfo LinksField =
            typeof(NodeGraph).GetField("links", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static System.Reflection.FieldInfo nodeGraphAssetField =
            typeof(SceneInfo).GetField($"groundNodesAsset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        protected readonly HullDef HumanHull = HullDef.Find(HullClassification.Human);
        protected readonly HullDef GolemHull = HullDef.Find(HullClassification.Golem);
        protected readonly HullDef QueenHull = HullDef.Find(HullClassification.BeetleQueen);

        private HullDef[] HullDefinitions;
        protected float HumanHeight => HumanHull.height;
        protected float GolemHeight => GolemHull.height;
        protected float QueenHeight => QueenHull.height;
        protected Vector3 HumanHeightOffset => Vector3.up * HumanHeight;
        protected Vector3 GolemHeightOffset => Vector3.up * GolemHeight;
        protected Vector3 QueenHeightOffset => Vector3.up * QueenHeight;

        //construct Hull Traversal mask
        protected (Vector3 bottom, Vector3 top) HumanCapsule(Vector3 nodePosition) => (bottom: nodePosition, top: nodePosition + HumanHeightOffset);
        protected (Vector3 bottom, Vector3 top) GolemCapsule(Vector3 nodePosition) => (bottom: nodePosition, top: nodePosition + GolemHeightOffset);
        protected (Vector3 bottom, Vector3 top) QueenCapsule(Vector3 nodePosition) => (bottom: nodePosition, top: nodePosition + QueenHeightOffset);

        protected static readonly Collider[] colliders = new Collider[128];
        protected static readonly RaycastHit[] hitArray = new RaycastHit[128];
        protected readonly List<int> resultsIndices = new List<int>();

        public MeshFilter[] meshFilters;
        public float Margin;
        public Vector3 TargetNormal;
        public int pointCount = 1000;
        public float nodeSeparation;
        public float linkDistance;
        public float lift;
        public bool rebuild;

        [SerializeField, HideInInspector] private float lastMargin;
        [SerializeField, HideInInspector] private Vector3 lastTargetNormal;

        [SerializeField, HideInInspector]
        private TriangleCollection TriangleCollection;

        private void Update()
        {
            if (HullDefinitions == null) HullDefinitions = new HullDef[] { HumanHull, GolemHull, QueenHull };
            if (rebuild)
            {
                var nodes = new List<Node>();
                var links = new List<Link>();
                rebuild = false;
                var pointTree = new KDTree();
                var query = new KDQuery();
                pointTree.SetCount(pointCount);
                var nodePoints = new List<Vector3>();

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

                LinkNodes(nodes, links, pointTree, query);
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

                    Profiler.BeginSample("Evaluate separation against kdtree");
                    if (pointTree.RootNode != null)
                    {
                        resultsIndices.Clear();
                        Profiler.BeginSample("Query point");
                        query.Radius(pointTree, position, nodeSeparation, resultsIndices);
                        Profiler.EndSample();
                        if (resultsIndices.Any())
                        {
                            //int nv = resultsIndices[0];
                            //var nudgedPosition = Vector3.Lerp(nodePoints[nv], position, 0.25f);
                            //var postModTest = nudgedPosition + (Vector3.up * lift);
                            //if (Physics.OverlapSphereNonAlloc(postModTest + (HumanHeightOffset / 2), HumanHeight / 2, colliders, LayerIndex.world.mask) == 0)
                            //{
                            //    var hits = Physics.RaycastNonAlloc(postModTest, Vector3.down, hitArray, HumanHeight);
                            //    if (hits > 0)
                            //        nudgedPosition = hitArray[0].point;

                            //    var node = nodes[nv];

                            //    node.position = nudgedPosition;
                            //    nodePoints[nv] = nudgedPosition;

                            //    nodes[nv] = node;

                            //    pointTree.Build(nodePoints);
                            //}
                            failures++;
                            Profiler.EndSample();
                            continue;
                        }
                    }
                    Profiler.EndSample();

                    Profiler.BeginSample("Evaluate random position fit");
                    var testOffset = (Vector3.up * lift);
                    var testPosition = position + testOffset;
                    HullMask mask = HullMask.None;

                    if (Physics.OverlapSphereNonAlloc(testPosition + (HumanHeightOffset / 2), HumanHeight / 2, colliders, LayerIndex.world.mask) == 0)
                    {
                        if (FootprintFitsPosition(position, HumanHull.radius, HumanHull.height, 6))
                            mask |= HullMask.Human;
                        if (Physics.OverlapSphereNonAlloc(testPosition + (GolemHeightOffset / 2), GolemHeight / 2, colliders, LayerIndex.world.mask) == 0)
                        {
                            if (FootprintFitsPosition(position, GolemHull.radius, GolemHull.height, 6))
                                mask |= HullMask.Golem;

                            if (Physics.OverlapSphereNonAlloc(testPosition + (QueenHeightOffset / 2), QueenHeight / 2, colliders, LayerIndex.world.mask) == 0)
                                if (FootprintFitsPosition(position, QueenHull.radius, QueenHull.height, 6))
                                    mask |= HullMask.BeetleQueen;
                        }
                    }
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
                        forbiddenHulls = (HullMask.Human | HullMask.Golem | HullMask.BeetleQueen) ^ mask,
                        flags = flags,
                        position = position
                    });
                    Profiler.EndSample();

                    Profiler.BeginSample("Update kdtree");
                    pointTree.Build(nodePoints);
                    Profiler.EndSample();
                }
            }
        }

        public void LinkNodes(List<Node> nodes, List<Link> links, KDTree pointTree, KDQuery query)
        {
            var resultsIndices = new List<int>();
            int nextLinkSetIndex = 0;

            Profiler.BeginSample("Construct node links");
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                resultsIndices.Clear();
                //Find nodes within link range
                query.Radius(pointTree, nodes[i].position, linkDistance, resultsIndices);
                resultsIndices.Remove(i);
                int skipped = 0;
                foreach (var nni in resultsIndices)
                {
                    var a = nodes[i];
                    var b = nodes[nni];

                    var allowedHulls = (HullMask.Human | HullMask.Golem | HullMask.BeetleQueen);
                    allowedHulls ^= a.forbiddenHulls | b.forbiddenHulls;

                    var testOffset = (Vector3.up * lift);
                    var testPositionA = a.position + testOffset;
                    var testPositionB = b.position + testOffset;
                    if (Physics.Linecast(testPositionA, testPositionB, LayerIndex.world.mask))
                    {
                        skipped++;
                        continue;
                    }

                    links.Add(new Link
                    {
                        distanceScore = Mathf.Sqrt((b.position - a.position).sqrMagnitude),
                        nodeIndexA = new NodeIndex(i),
                        nodeIndexB = new NodeIndex(nni),
                        hullMask = (int)allowedHulls,
                        jumpHullMask = (int)allowedHulls,
                        maxSlope = 90,
                        gateIndex = (byte)b.gateIndex
                    });

                    nodes[i] = a;
                    nodes[nni] = b;
                }
                uint linkCount = (uint)(resultsIndices.Count - skipped);
                node.linkListIndex = new LinkListIndex { index = nextLinkSetIndex, size = linkCount };
                nodes[i] = node;
                nextLinkSetIndex += (int)linkCount;
            }
            Profiler.EndSample();
        }

        public bool FootprintFitsPosition(Vector3 position, float radius, float height, int steps = 8)
        {
            float degrees = 360f / (float)steps;
            var referenceOriginA = Vector3.forward * radius;
            for (int index = 0; index < steps; ++index)
            {
                var rotation = Quaternion.AngleAxis(degrees * (float)index, Vector3.up);
                var stepOffset = rotation * referenceOriginA;
                var ray = new Ray(position + stepOffset + (Vector3.up * height), Vector3.down);
                if (Physics.RaycastNonAlloc(ray, hitArray, height * 1.25f, LayerIndex.world.mask) == 0)
                    return false;
            }
            return true;
        }

        bool TestTeleporterOK(Vector3 position)
        {
            float radius = 15f;
            if (Physics.OverlapSphereNonAlloc(position + Vector3.up * (radius + 1), radius, colliders) > 0)
                return false;

            return FootprintFitsPosition(position, 15f, 7f);
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