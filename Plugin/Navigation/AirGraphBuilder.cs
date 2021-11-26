using DataStructures.ViliWonka.KDTree;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static RoR2.Navigation.NodeGraph;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class AirGraphBuilder : GraphBuilder<AirNavigationProbe>
    {
        protected override Vector3 TryGetPoint(AirNavigationProbe probe, KDQuery query, KDTree pointTree, out bool failed)
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

        protected override bool TryAddLink(Node a, Node b, int nodeAIndex, int nodeBIndex, List<Link> links)
        {
            var maxDist = Vector3.Distance(a.position, b.position);
            Vector3 direction = (b.position - a.position).normalized;

            var mask = HullMask.None;

            //construct Hull Traversal mask
            var humanCapsule = HumanCapsule(a.position + (Vector3.down * HumanHeight / 2));
            var golemCapsule = GolemCapsule(a.position + (Vector3.down * HumanHeight / 2));
            var queenCapsule = QueenCapsule(a.position + (Vector3.down * HumanHeight / 2));

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
            a.forbiddenHulls = (HullMask.Human | HullMask.Golem | HullMask.BeetleQueen) ^ mask;
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
    }
}