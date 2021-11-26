using DataStructures.ViliWonka.KDTree;
using RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RoR2.Navigation.NodeGraph;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public abstract class GraphBuilder<T> : MonoBehaviour where T : NavigationProbe
    {
        public static System.Reflection.FieldInfo NodesField =
            typeof(NodeGraph).GetField("nodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static System.Reflection.FieldInfo LinksField =
            typeof(NodeGraph).GetField("links", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static System.Reflection.FieldInfo nodeGraphAssetField =
            typeof(SceneInfo).GetField($"{graphPrefix.ToLower()}NodesAsset",
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private static string graphPrefix => typeof(T).Name.Replace(nameof(NavigationProbe), "");

        private static readonly Vector3[] cubeVertices = new[]
        {
                    new Vector3 (0, 0, 0) - Vector3.one * 0.5f,
                    new Vector3 (1, 0, 0) - Vector3.one * 0.5f,
                    new Vector3 (1, 1, 0) - Vector3.one * 0.5f,
                    new Vector3 (0, 1, 0) - Vector3.one * 0.5f,
                    new Vector3 (0, 1, 1) - Vector3.one * 0.5f,
                    new Vector3 (1, 1, 1) - Vector3.one * 0.5f,
                    new Vector3 (1, 0, 1) - Vector3.one * 0.5f,
                    new Vector3 (0, 0, 1) - Vector3.one * 0.5f
        };
        private static readonly int[] cubeTriangles = new[]
        {
            0, 2, 1, 0, 3, 2,
            2, 3, 4, 2, 4, 5,
            1, 2, 5, 1, 5, 6,
            0, 7, 4, 0, 4, 3,
            5, 4, 7, 5, 7, 6,
            0, 6, 7, 0, 1, 6
        };
        protected static readonly Collider[] colliders = new Collider[128];
        protected static readonly RaycastHit[] hitArray = new RaycastHit[128];


        protected readonly HullDef HumanHull = HullDef.Find(HullClassification.Human);
        protected readonly HullDef GolemHull = HullDef.Find(HullClassification.Golem);
        protected readonly HullDef QueenHull = HullDef.Find(HullClassification.BeetleQueen);
        protected float HumanHeight => HumanHull.height;
        protected float GolemHeight => GolemHull.height;
        protected float QueenHeight => QueenHull.height;
        protected Vector3 HumanHeightOffset => Vector3.up * HumanHeight;
        protected Vector3 GolemHeightOffset => Vector3.up * GolemHeight;
        protected Vector3 QueenHeightOffset => Vector3.up * QueenHeight;
        protected (Vector3 bottom, Vector3 top) HumanCapsule(Vector3 center) => (bottom: center, top: center + HumanHeightOffset);
        protected (Vector3 bottom, Vector3 top) GolemCapsule(Vector3 center) => (bottom: center, top: center + GolemHeightOffset);
        protected (Vector3 bottom, Vector3 top) QueenCapsule(Vector3 center) => (bottom: center, top: center + QueenHeightOffset);

        public List<T> Probes = new List<T>();

        private Material gizmoMaterial;
        protected readonly List<int> resultsIndices = new List<int>();

        private void Start() => Load();
        private void Update() => Load();

#if UNITY_EDITOR
        void OnRenderObject()
        {
            if (!Selection.gameObjects.Contains(gameObject)) return;
            if (!gizmoMaterial) gizmoMaterial = new Material(Shader.Find("VR/SpatialMapping/Wireframe"));
            gizmoMaterial.SetPass(0);
            foreach (var probe in Probes)
            {
                GL.PushMatrix();
                GL.MultMatrix(probe.transform.localToWorldMatrix);
                GL.Begin(GL.TRIANGLES);
                try
                {
                    GL.Color(Color.green);
                    for (int i = 0; i < cubeTriangles.Length; i += 3)
                    {
                        var a = cubeVertices[cubeTriangles[i + 0]] * 2;
                        var b = cubeVertices[cubeTriangles[i + 1]] * 2;
                        var c = cubeVertices[cubeTriangles[i + 2]] * 2;
                        GL.Vertex3(a.x, a.y, a.z);
                        GL.Vertex3(b.x, b.y, b.z);
                        GL.Vertex3(c.x, c.y, c.z);
                    }
                }
                catch { }
                GL.End();
                GL.PopMatrix();
            }
        }
#endif
        void Load()
        {
            Probes = new List<T>(GetComponentsInChildren<T>());
            bool rebuild = false;
            foreach (var probe in Probes)
            {
                if (!probe.isDirty) continue;
                try
                {
                    rebuild = true;
                    if (probe.seed == -1)
                        Random.InitState((int)Time.realtimeSinceStartup);
                    else
                        Random.InitState(probe.seed);
                    var pointTree = new KDTree(16);
                    var query = new KDQuery(2048);
                    var fails = 0;
                    var maxFails = probe.targetPointCount / 2;
                    var localTarget = probe.targetPointCount;
                    probe.nodePositions.Clear();
                    Profiler.BeginSample("Create Probe Nodes");
                    while (probe.nodePositions.Count <= localTarget)
                    {
                        if (maxFails == 0) break;
                        if (fails > probe.pointPasses)
                        {
                            maxFails--;
                            localTarget--;
                            localTarget = Mathf.Clamp(localTarget, probe.targetPointCount / 2, probe.targetPointCount);
                            fails = 0;
                        }

                        var point = TryGetPoint(probe, query, pointTree, out bool failed);
                        if (failed)
                        {
                            fails++;
                            continue;
                        }

                        probe.nodePositions.Add(point);
                        pointTree.Build(probe.nodePositions);
                    }
                    Profiler.EndSample();
                }
                finally
                {
                    probe.isDirty = false;
                }
            }
            if (rebuild)
                LinkGlobalNodes();
        }

        protected abstract Vector3 TryGetPoint(T probe, KDQuery query, KDTree pointTree, out bool failed);
        public void LinkGlobalNodes()
        {
            var pointTree = new KDTree();
            var query = new KDQuery(2048);
            var resultsIndices = new List<int>();
            Profiler.BeginSample("Load Global Nodes");

            var nodePoints = Probes.SelectMany(probe => probe.nodePositions.Select(p => probe.transform.TransformPoint(p))).ToList();
            var nodeProbes = Probes.SelectMany(probe => probe.nodePositions.Select(p => probe)).ToList();

            Profiler.EndSample();
            var nodes = new List<Node>();
            var links = new List<Link>();
            int nextLinkSetIndex = 0;
            Profiler.BeginSample("Construct Global KDTree");
            pointTree.Build(nodePoints, 32);
            Profiler.EndSample();
            void Remove(ref int ix)
            {
                Profiler.BeginSample("Remove Node and Reconstruct KDTree");
                nodePoints.RemoveAt(ix);
                nodeProbes.RemoveAt(ix);
                ix--;
                pointTree.Build(nodePoints, 64);
                Profiler.EndSample();
            }
            void RemoveAll(List<int> indicies)
            {
                Profiler.BeginSample("Remove Node and Reconstruct KDTree");

                for (int i = 0; i < indicies.Count; i++)
                {
                    nodePoints.RemoveAt(indicies[i] - i);
                    nodeProbes.RemoveAt(indicies[i] - i);
                }
                pointTree.Build(nodePoints, 32);
                Profiler.EndSample();
            }
            Profiler.BeginSample("Evaluate Global Nodes");
            for (int i = 0; i < nodePoints.Count; i++)
            {
                var position = nodePoints[i];
                var probe = nodeProbes[i];

                //if nodes are within separation range of current node, remove other nodes in range
                resultsIndices.Clear();
                query.Radius(pointTree, position, probe.nodeSeparation, resultsIndices);
                resultsIndices.Remove(i);
                if (resultsIndices.Any())
                {
                    resultsIndices.Sort();
                    RemoveAll(resultsIndices);
                }

                //if no nodes are within link range of current node, destroy this node
                resultsIndices.Clear();
                query.Radius(pointTree, position, probe.linkDistance, resultsIndices);
                resultsIndices.Remove(i);
                if (!resultsIndices.Any())
                {
                    Remove(ref i);
                    continue;
                }

                // for all nodes within link range of current node, verify line of sight exists between both nodes
                //var canReach = false;
                //foreach (var index in resultsIndices)
                //{
                //    var otherPosition = nodePoints[index];
                //    var direction = (position - otherPosition).normalized;
                //    var distance = Vector3.Distance(position, otherPosition);
                //    if (Physics.RaycastNonAlloc(otherPosition, direction, hitArray, distance, LayerIndex.world.mask) <= 0)
                //    {
                //        canReach = true;
                //        break;
                //    }
                //}
                //if (!canReach)
                //{
                //    Remove(ref i);
                //    continue;
                //}
                //no ceiling check
                var upHit = Physics.RaycastNonAlloc(new Ray(position, Vector3.up), hitArray, 50, LayerIndex.world.mask) > 0;

                //Too close check
                var overlaps = Physics.OverlapSphereNonAlloc(position, probe.minimumSurfaceDistance, colliders, LayerIndex.world.mask);
                if (overlaps > 0)
                {
                    Remove(ref i);
                    continue;
                }
                //too far check
                var withinDistance = Physics.OverlapSphereNonAlloc(position, probe.maximumSurfaceDistance, colliders, LayerIndex.world.mask);
                if (withinDistance <= 0)
                {
                    Remove(ref i);
                    continue;
                }
                nodes.Add(new Node
                {
                    //no shrines or chests in air
                    flags = (NodeFlags.NoShrineSpawn | NodeFlags.NoChestSpawn)
                          //apply no ceiling based upon up hit
                          | (!upHit ? NodeFlags.NoCeiling : NodeFlags.None),
                    position = position,

                });
            }
            Profiler.EndSample();


            Profiler.BeginSample("Construct Node Links");
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var probe = nodeProbes[i];
                resultsIndices.Clear();
                //Find nodes within link range
                query.Radius(pointTree, nodes[i].position, probe.linkDistance, resultsIndices);
                resultsIndices.Remove(i);
                int skipped = 0;
                foreach (var nni in resultsIndices)
                {
                    var a = nodes[i];
                    var b = nodes[nni];

                    if (!TryAddLink(a, b, i, nni, links))
                    {
                        skipped++;
                        continue;
                    }

                    nodes[i] = a;
                    nodes[nni] = b;
                }
                uint linkCount = (uint)(resultsIndices.Count - skipped);
                node.linkListIndex = new LinkListIndex { index = nextLinkSetIndex, size = linkCount };
                nodes[i] = node;
                nextLinkSetIndex += (int)linkCount;
            }
            Profiler.EndSample();

            for(int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                node = PostProcessNode(node);
                nodes[i] = node;
            }

            Profiler.BeginSample("Save Graph Changes");
            var sceneInfo = FindObjectOfType<SceneInfo>();
            var activeScene = SceneManager.GetActiveScene();
            var scenePath = activeScene.path;
            scenePath = System.IO.Path.GetDirectoryName(scenePath);
            var graphName = $"{activeScene.name}_{graphPrefix}NodeGraph.asset";
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

        protected virtual Node PostProcessNode(Node node)
        {
            return new Node
            {
                position = node.position,
                flags = node.flags,
                forbiddenHulls = node.forbiddenHulls,
                gateIndex = node.gateIndex,
                lineOfSightMask = node.lineOfSightMask,
                linkListIndex = node.linkListIndex
            };
        }

        protected abstract bool TryAddLink(Node a, Node b, int nodeAIndex, int nodeBIndex, List<Link> links);

    }
}