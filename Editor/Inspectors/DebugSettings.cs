using RoR2;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer
{
    [System.Serializable]
    public struct DebugSettings
    {
        public bool ShowGraphTools;
        public bool DebugNoCeiling;
        public bool DebugTeleporterOk;

        public bool DebugAirLinks;
        public bool DebugAirNodes;

        public bool DebugGroundNodes;
        public bool DebugGroundLinks;

        public bool ProbeLineOfSightOverlay;
        public bool ShowSettings;

        public float VerticalOffset;
        public float ArrowSize;
        public float ArrowOffset;
        public bool PercentageOffset;

        public Color HumanColor;
        public Color GolemColor;
        public Color QueenColor;
        public Color NoCeilingColor;
        public Color TeleporterOkColor;

        public int HullMask;
    }
}