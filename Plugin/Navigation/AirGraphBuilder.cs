using DataStructures.ViliWonka.KDTree;
using RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using static RoR2.Navigation.NodeGraph;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class AirGraphBuilder : GraphBuilder
    {
        public static System.Reflection.FieldInfo nodeGraphAssetField =
            typeof(SceneInfo).GetField($"airNodesAsset",
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        public List<NavigationProbe> Probes = new List<NavigationProbe>();

        private Material gizmoMaterial;
        private void Start() => Load();
        private void Update() => Load();
        protected Vector3 TryGetPoint(NavigationProbe probe, KDQuery query, KDTree pointTree, out bool failed)
        {
            var point = Random.insideUnitSphere * probe.distance;
            var np = probe.transform.TransformPoint(point);
            var dir = (np - probe.transform.position).normalized;
            var dist = Vector3.Distance(probe.transform.position, np);
            if (Physics.RaycastNonAlloc(probe.transform.position, dir, hitArray, dist, LayerIndex.world.mask) > 0)
            {
                failed = true;
                return Vector3.zero;
            }

            if (pointTree.Count > 0)
            {
                resultsIndices.Clear();
                query.Radius(pointTree, point, probe.nodeSeparation, resultsIndices);
                if (resultsIndices.Any())
                {
                    failed = true;
                    return Vector3.zero;
                }
            }

            failed = false;
            return point;
        }

        protected bool TryAddLink(ref Node a, ref Node b, int nodeAIndex, int nodeBIndex, List<Link> links)
        {
            var maxDist = Vector3.Distance(a.position, b.position);
            Vector3 direction = (b.position - a.position).normalized;

            var mask = HullMask.None;

            //construct Hull Traversal mask
            var humanCapsule = HumanCapsule(a.position + (Vector3.down * HumanHeight / 2));
            var golemCapsule = GolemCapsule(a.position + (Vector3.down * GolemHeight / 2));
            var queenCapsule = QueenCapsule(a.position + (Vector3.down * QueenHeight / 2));

            if (Physics.CapsuleCastNonAlloc(humanCapsule.top, humanCapsule.bottom, HumanHull.radius, direction, hitArray, maxDist, LayerIndex.world.mask) == 0)
            {
                mask |= HullMask.Human;
                if (Physics.CapsuleCastNonAlloc(golemCapsule.top, golemCapsule.bottom, GolemHull.radius, direction, hitArray, maxDist, LayerIndex.world.mask) == 0)
                {
                    mask |= HullMask.Golem;
                    if (Physics.CapsuleCastNonAlloc(queenCapsule.top, queenCapsule.bottom, QueenHull.radius, direction, hitArray, maxDist, LayerIndex.world.mask) == 0)
                        mask |= HullMask.BeetleQueen;
                }
            }

            if (mask == HullMask.None) return false;

            //Set node forbiddenHulls
            b.forbiddenHulls = (HullMask.Human | HullMask.Golem | HullMask.BeetleQueen) ^ mask;

            links.Add(new Link
            {
                distanceScore = Mathf.Sqrt((b.position - a.position).sqrMagnitude),
                nodeIndexA = new NodeIndex(nodeAIndex),
                nodeIndexB = new NodeIndex(nodeBIndex),
                hullMask = (int)mask,
                jumpHullMask = (int)mask,
                maxSlope = 90
            });

            return true;
        }
        void Load()
        {
            Probes = new List<NavigationProbe>(GetComponentsInChildren<NavigationProbe>());
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
                    var query = new KDQuery(probe.targetPointCount);
                    var fails = 0;
                    var localTarget = probe.targetPointCount;
                    probe.nodePositions.Clear();
                    Profiler.BeginSample("Create Probe Nodes");
                    while (probe.nodePositions.Count <= localTarget)
                    {
                        if (fails > probe.pointPasses)
                        {
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

                    if (!TryAddLink(ref a, ref b, i, nni, links))
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

            Profiler.BeginSample("Save Graph Changes");
            var sceneInfo = FindObjectOfType<SceneInfo>();
            var activeScene = SceneManager.GetActiveScene();
            var scenePath = activeScene.path;
            scenePath = System.IO.Path.GetDirectoryName(scenePath);
            var graphName = $"{activeScene.name}_airNodeGraph.asset";
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
    }
}