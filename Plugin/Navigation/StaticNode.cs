using RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class StaticNode : MonoBehaviour
    {
        public static readonly List<StaticNode> StaticNodes = new List<StaticNode>();

        public Color staticNodeColor = Color.green;

        public string nodeName;
        [EnumMask(typeof(HullMask))]
        public HullMask forbiddenHulls;
        [EnumMask(typeof(NodeFlags))]
        public NodeFlags nodeFlags;
        public Vector3 nodePosition;
        public bool overrideDistanceScore;
        public float distanceScore;
        public bool relativePosition = true;
        public bool worldSpacePosition = true;
        public bool allowDynamicConnections = true;
        public bool allowOutboundConnections = true;
        public bool allowInboundConnections = true;
        public StaticNode[] HardLinks;

        public Vector3 position
        {
            get
            {
                if (worldSpacePosition)
                    return nodePosition;
                else if (relativePosition)
                    return transform.position + nodePosition;
                else
                    return transform.position;
            }
            set
            {
                if (worldSpacePosition)
                    nodePosition = value;
                else if (relativePosition)
                    nodePosition = value - transform.position;
                else
                    transform.position = value;
            }
        }

        private void OnEnable()
        {
            StaticNodes.Add(this);
        }
        private void OnDisable()
        {
            StaticNodes.Remove(this);
        }
        private void OnDestroy()
        {
            StaticNodes.Remove(this);
        }
    }
}