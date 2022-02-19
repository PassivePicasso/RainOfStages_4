using RoR2.Navigation;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer
{
    [System.Serializable]
    public struct DebugSettings
    {
        public enum Graph { Ground, Air}
        public Graph DebugGraph;
        public NodeFlags DebugFlags;
        public bool ShowGraphTools;

        public bool DebugNodes;
        public bool DebugLinks;

        public bool ProbeLineOfSightOverlay;
        public bool ShowSettings;

        public float VerticalOffset;

        public Color HumanColor;
        public Color GolemColor;
        public Color QueenColor;
        public Color NoCeilingColor;
        public Color TeleporterOkColor;
        public Color NoCharacterSpawnColor;
        public Color NoShrineSpawnColor;
        public Color NoChestSpawnColor;

        public int HullMask;
    }
}