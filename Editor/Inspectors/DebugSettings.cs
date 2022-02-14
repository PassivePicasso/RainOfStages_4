using RoR2;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer
{
    [System.Serializable]
    public struct DebugSettings
    {
        public bool DebugNoCeiling;
        public bool DebugTeleporterOk;

        public bool DebugAirLinks;
        public bool DebugAirNodes;

        public bool DebugGroundNodes;
        public bool DebugGroundLinks;

        public float VerticalOffset;
        public float arrowSize;
        public float arrowOffset;
        public bool percentageOffset;

        public Color HumanColor;
        public Color GolemColor;
        public Color QueenColor;
        public Color NoCeilingColor;
        public Color TeleporterOkColor;

        public int HullMask;
    }
}