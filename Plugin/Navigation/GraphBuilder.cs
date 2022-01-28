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
    public abstract class GraphBuilder : MonoBehaviour
    {
        protected const HullMask AllHulls = (HullMask.Human | HullMask.Golem | HullMask.BeetleQueen);

        public static System.Reflection.FieldInfo NodesField =
            typeof(NodeGraph).GetField("nodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static System.Reflection.FieldInfo LinksField =
            typeof(NodeGraph).GetField("links", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        protected static readonly Vector3[] cubeVertices = new[]
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
        protected static readonly int[] cubeTriangles = new[]
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
        protected (Vector3 bottom, Vector3 top) HumanCapsule(Vector3 position) => (bottom: position, top: position + HumanHeightOffset);
        protected (Vector3 bottom, Vector3 top) GolemCapsule(Vector3 position) => (bottom: position, top: position + GolemHeightOffset);
        protected (Vector3 bottom, Vector3 top) QueenCapsule(Vector3 position) => (bottom: position, top: position + QueenHeightOffset);

        protected readonly List<int> resultsIndices = new List<int>();

    }
}