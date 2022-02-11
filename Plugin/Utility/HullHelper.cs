using RoR2;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Utilities
{
    public static class HullHelper
    {
        public static readonly HullMask AllHullsMask = (HullMask.Human | HullMask.Golem | HullMask.BeetleQueen);
        public static readonly HullMask[] HullMasks = new[] { HullMask.Human, HullMask.Golem, HullMask.BeetleQueen };

        public static readonly HullDef HumanHull = HullDef.Find(HullClassification.Human);
        public static readonly HullDef GolemHull = HullDef.Find(HullClassification.Golem);
        public static readonly HullDef QueenHull = HullDef.Find(HullClassification.BeetleQueen);
        private static HullDef[] allHulls;
        public static HullDef[] AllHulls => allHulls ?? (allHulls = new HullDef[] { HumanHull, GolemHull, QueenHull });
        public static float HumanHeight => HumanHull.height;
        public static float GolemHeight => GolemHull.height;
        public static float QueenHeight => QueenHull.height;
        public static Vector3 HumanHeightOffset => Vector3.up * HumanHeight;
        public static Vector3 GolemHeightOffset => Vector3.up * GolemHeight;
        public static Vector3 QueenHeightOffset => Vector3.up * QueenHeight;
        public static (Vector3 bottom, Vector3 top) HumanCapsule(Vector3 position) => (bottom: position, top: position + HumanHeightOffset);
        public static (Vector3 bottom, Vector3 top) GolemCapsule(Vector3 position) => (bottom: position, top: position + GolemHeightOffset);
        public static (Vector3 bottom, Vector3 top) QueenCapsule(Vector3 position) => (bottom: position, top: position + QueenHeightOffset);
    }
}
