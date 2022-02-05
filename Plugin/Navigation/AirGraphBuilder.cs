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
    public class AirGraphBuilder : GraphBuilder
    {
        public static System.Reflection.FieldInfo nodeGraphAssetField =
            typeof(SceneInfo).GetField($"airNodesAsset",
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        public List<NavigationProbe> Probes = new List<NavigationProbe>();
        IEnumerable<GameObject> FindCollidersOnLayer(int layer)
        {
            var goArray = FindObjectsOfType<Collider>();
            for (var i = 0; i < goArray.Length; i++)
                if (goArray[i].gameObject.layer == layer)
                    yield return goArray[i].gameObject;
        }

        public override void Build()
        {
            if (!FindCollidersOnLayer(LayerIndex.world.intVal).Any())
                return;

            Probes = new List<NavigationProbe>(GetComponentsInChildren<NavigationProbe>());
            var updateLinks = false;
            foreach (var probe in Probes)
            {
                if (!rebuild && !probe.isDirty) continue;
                try
                {
                    updateLinks = true;
                    InitializeSeed(probe.seed);

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

                        if (!TryGetPoint(probe, query, pointTree, out var point))
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
            if (updateLinks)
                LinkGlobalNodes();
        }

        protected bool TryGetPoint(NavigationProbe probe, KDQuery query, KDTree pointTree, out Vector3 position)
        {
            position = Random.insideUnitSphere * probe.distance;
            var np = probe.transform.TransformPoint(position);
            var dir = (np - probe.transform.position).normalized;
            var dist = Vector3.Distance(probe.transform.position, np);

            if (pointTree.Count > 0)
            {
                resultsIndices.Clear();
                query.Radius(pointTree, np, probe.nodeSeparation, resultsIndices);
                //Nodes in separation radius
                if (resultsIndices.Any())
                    return false;
            }

            //Line of sight to probe check
            if (Physics.RaycastNonAlloc(probe.transform.position, dir, hitArray, dist, LayerIndex.enemyBody.collisionMask) > 0)
            {
                return false;
            }

            //Too close check
            int overlaps = Physics.OverlapSphereNonAlloc(np, HumanHeight, colliders, LayerIndex.enemyBody.collisionMask);
            if (overlaps > 0)
                return false;

            //too far check
            overlaps = Physics.OverlapSphereNonAlloc(np, QueenHeight + 2, colliders, LayerIndex.world.mask);
            if (overlaps <= 0)
                return false;

            return true;
        }

        protected bool TryAddLink(ref Node a, ref Node b, int nodeAIndex, int nodeBIndex, List<Link> links)
        {
            Profiler.BeginSample("TryAddLink");
            var maxDist = Vector3.Distance(a.position, b.position);
            Vector3 direction = (b.position - a.position).normalized;

            var mask = HullMask.None;

            //construct Hull Traversal mask
            var humanCapsule = HumanCapsule(a.position + (Vector3.down * HumanHeight / 2));
            var golemCapsule = GolemCapsule(a.position + (Vector3.down * GolemHeight / 2));
            var queenCapsule = QueenCapsule(a.position + (Vector3.down * QueenHeight / 2));

            if (Physics.CapsuleCastNonAlloc(queenCapsule.top, queenCapsule.bottom, QueenHull.radius * 1.5f, direction, hitArray, maxDist, LayerIndex.enemyBody.collisionMask) == 0)
                mask = AllHulls;
            else
            if (Physics.CapsuleCastNonAlloc(golemCapsule.top, golemCapsule.bottom, GolemHull.radius * 1.5f, direction, hitArray, maxDist, LayerIndex.enemyBody.collisionMask) == 0)
                mask = AllHulls ^ HullMask.BeetleQueen;
            else
            if (Physics.CapsuleCastNonAlloc(humanCapsule.top, humanCapsule.bottom, HumanHull.radius * 1.5f, direction, hitArray, maxDist, LayerIndex.enemyBody.collisionMask) == 0)
                mask = HullMask.Human;

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
            Profiler.EndSample();

            return true;
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

                //no ceiling check
                var upHit = Physics.RaycastNonAlloc(new Ray(position, Vector3.up), hitArray, 50, LayerIndex.enemyBody.collisionMask) > 0;

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

            Apply(nodeGraphAssetField, $"{gameObject.scene.name}_AirNodeGraph.asset", nodes, links);
        }
    }
}