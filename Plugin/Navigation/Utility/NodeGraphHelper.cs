using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
using static RoR2.Navigation.NodeGraph;

namespace PassivePicasso.RainOfStages.Plugin.Utilities
{
    public static class NodeGraphHelper
    {
        public static System.Reflection.FieldInfo NodesField =
            typeof(NodeGraph).GetField("nodes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static System.Reflection.FieldInfo LinksField =
            typeof(NodeGraph).GetField("links", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        public static void SetNodes(this NodeGraph nodeGraph, IEnumerable<Node> nodes)
        {
            NodesField.SetValue(nodeGraph, nodes.ToArray());
        }

        public static void SetLinks(this NodeGraph nodeGraph, IEnumerable<Link> links)
        {
            LinksField.SetValue(nodeGraph, links.ToArray());
        }
    }
}
