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
    public class AirGraphBuilder : GraphBuilder
    {
        public static System.Reflection.FieldInfo nodeGraphAssetField =
            typeof(SceneInfo).GetField($"airNodesAsset",
                                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public int seed;
        public float nodeSeparation = 8;
        //public float linkDistance = 16;
        public int passes;
        private int pointsPerLeaf;

        public List<NavigationProbe> Probes = new List<NavigationProbe>();

        IEnumerable<GameObject> FindCollidersOnLayer(int layer)
        {
            var goArray = FindObjectsOfType<Collider>();
            for (var i = 0; i < goArray.Length; i++)
                if (goArray[i].gameObject.layer == layer)
                    yield return goArray[i].gameObject;
        }

        protected override void OnBuild()
        {
            if (!FindCollidersOnLayer(LayerIndex.world.intVal).Any())
                return;

            Probes = new List<NavigationProbe>(GetComponentsInChildren<NavigationProbe>());
            InitializeSeed(seed);
            var nodePositions = new List<Vector3>();
            Profiler.BeginSample("Acquire Node Positions");
            var nodeArea = Mathf.PI * (nodeSeparation * nodeSeparation);
            for (int p = 0; p < passes; p++)
            {
                foreach (var probe in Probes)
                {
                    var probeArea = Mathf.PI * (probe.distance * probe.distance);
                    var relativeArea = probeArea / nodeArea;
                    int maxCount = (int)relativeArea * 10;
                    for (int i = 0; i < maxCount; i++)
                    {
                        if (!TryGetPoint(probe, out var point))
                            continue;

                        nodePositions.Add(point);
                    }
                }
            }
            Profiler.EndSample();

            LinkGlobalNodesAlternate(nodePositions);
        }

        protected bool TryGetPoint(NavigationProbe probe, out Vector3 position)
        {
            position = Random.insideUnitSphere * probe.distance;
            position = probe.transform.TransformPoint(position);
            var dir = (position - probe.transform.position).normalized;
            var dist = Vector3.Distance(probe.transform.position, position);

            //Line of sight to probe check
            if (Physics.RaycastNonAlloc(probe.transform.position, dir, hitArray, dist, LayerIndex.enemyBody.collisionMask) > 0)
            {
                return false;
            }

            //Too close check
            int overlaps = Physics.OverlapSphereNonAlloc(position, HumanHeight * 1.5f, colliders, LayerIndex.enemyBody.collisionMask);
            if (overlaps > 0)
                return false;

            //too far check
            overlaps = Physics.OverlapSphereNonAlloc(position, QueenHeight * 1.5f, colliders, LayerIndex.world.mask);
            if (overlaps <= 0)
                return false;

            return true;
        }


        public void LinkGlobalNodesAlternate(List<Vector3> nodePoints)
        {
            var pointTree = new KDTree();
            var query = new KDQuery(2048);
            var addedIndices = new HashSet<int>();
            var blackList = new HashSet<int>();
            var resultsIndices = new List<int>();
            var nodes = new List<Node>();
            var links = new List<Link>();
            int nextLinkSetIndex = 0;

            Profiler.BeginSample("Load Global Nodes");
            Profiler.EndSample();

            Profiler.BeginSample("Construct Global KDTree");
            pointTree.Build(nodePoints, pointsPerLeaf);
            Profiler.EndSample();

            Profiler.BeginSample("Evaluate Global Nodes");
            for (int i = 0; i < nodePoints.Count; i++)
            {
                var position = nodePoints[i];

                Profiler.BeginSample("Find nodes within separation");
                resultsIndices.Clear();
                query.Radius(pointTree, position, nodeSeparation, resultsIndices, whitelist: addedIndices);
                Profiler.EndSample();
                if (resultsIndices.Any())
                    continue;

                Profiler.BeginSample("Overlap Check Global Nodes");
                var mask = AllHullsMask;
                var queenCapsule = QueenCapsule(position + (Vector3.down * (QueenHeight / 2)));
                var golemCapsule = GolemCapsule(position + (Vector3.down * (GolemHeight / 2)));
                var humanCapsule = HumanCapsule(position + (Vector3.down * (HumanHeight / 2)));

                if (Physics.OverlapCapsuleNonAlloc(queenCapsule.bottom, queenCapsule.top, QueenHull.radius, colliders, LayerIndex.enemyBody.collisionMask) == 0)
                    mask = HullMask.None;
                else if (Physics.OverlapCapsuleNonAlloc(golemCapsule.bottom, golemCapsule.top, GolemHull.radius, colliders, LayerIndex.enemyBody.collisionMask) == 0)
                    mask = HullMask.BeetleQueen;
                else if (Physics.OverlapCapsuleNonAlloc(humanCapsule.bottom, humanCapsule.top, HumanHull.radius, colliders, LayerIndex.enemyBody.collisionMask) == 0)
                    mask = HullMask.BeetleQueen | HullMask.Golem;
                Profiler.EndSample();

                if (mask == AllHullsMask)
                    continue;

                var upHit = Physics.RaycastNonAlloc(new Ray(position, Vector3.up), hitArray, 50, LayerIndex.enemyBody.collisionMask) > 0;
                addedIndices.Add(i);

                //no ceiling check
                nodes.Add(new Node
                {
                    forbiddenHulls = mask,
                    //no shrines or chests in air
                    flags = (NodeFlags.NoShrineSpawn | NodeFlags.NoChestSpawn)
                          //apply no ceiling based upon up hit
                          | (!upHit ? NodeFlags.NoCeiling : NodeFlags.None),
                    position = position,

                });
            }
            Profiler.EndSample();

            Profiler.BeginSample("Update Global KDTree");
            pointTree.Build(nodes.Select(n => n.position).ToArray(), pointsPerLeaf);

            Profiler.EndSample();

            Profiler.BeginSample("Construct Node Links");
            for (int i = 0; i < nodes.Count; i++)
            {
                var originNode = nodes[i];
                blackList.Clear();
                blackList.Add(i);
                resultsIndices.Clear();
                nextLinkSetIndex = links.Count;

                //Find nodes within link range
                Profiler.BeginSample("Query for nodes in linkDistance");
                query.Radius(pointTree, nodes[i].position, nodeSeparation * 2.5f, resultsIndices, whitelist: null, blackList);
                Profiler.EndSample();
                Profiler.BeginSample("TryAddLink");
                foreach (var nni in resultsIndices)
                {
                    var destinationNode = nodes[nni];
                    var distance = Vector3.Distance(originNode.position, destinationNode.position);
                    var direction = (destinationNode.position - originNode.position).normalized;

                    //construct Hull Traversal mask
                    var mask = HullMask.None;
                    var queenCapsule = QueenCapsule(originNode.position);
                    var golemCapsule = GolemCapsule(originNode.position);
                    var humanCapsule = HumanCapsule(originNode.position);

                    if (Physics.CapsuleCastNonAlloc(queenCapsule.bottom, queenCapsule.top, QueenHull.radius, direction, hitArray, distance, LayerIndex.enemyBody.collisionMask) == 0)
                        mask = AllHullsMask;
                    else
                    if (Physics.CapsuleCastNonAlloc(golemCapsule.bottom, golemCapsule.top, GolemHull.radius, direction, hitArray, distance, LayerIndex.enemyBody.collisionMask) == 0)
                        mask = AllHullsMask ^ HullMask.BeetleQueen;
                    else
                    if (Physics.CapsuleCastNonAlloc(humanCapsule.bottom, humanCapsule.top, HumanHull.radius, direction, hitArray, distance, LayerIndex.enemyBody.collisionMask) == 0)
                        mask = HullMask.Human;

                    if (mask == HullMask.None)
                        continue;

                    links.Add(new Link
                    {
                        distanceScore = (destinationNode.position - originNode.position).magnitude,
                        nodeIndexA = new NodeIndex(i),
                        nodeIndexB = new NodeIndex(nni),
                        hullMask = (int)(mask),
                        jumpHullMask = (int)HullMask.None,
                        maxSlope = 90
                    });
                }
                Profiler.EndSample();

                originNode.linkListIndex = new LinkListIndex { index = nextLinkSetIndex, size = (uint)(links.Count - nextLinkSetIndex) };
                nodes[i] = originNode;
            }
            Profiler.EndSample();

            Apply(nodeGraphAssetField, $"{gameObject.scene.name}_AirNodeGraph.asset", nodes, links);
        }
    }
}