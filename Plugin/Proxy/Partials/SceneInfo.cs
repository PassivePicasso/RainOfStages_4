using global::RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Proxy
{
    [ExecuteAlways]
    public partial class SceneInfo : global::RoR2.SceneInfo
    {
        static FieldInfo nodesField = typeof(NodeGraph).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo linksField = typeof(NodeGraph).GetField("links", BindingFlags.NonPublic | BindingFlags.Instance);

        [SerializeField]
        public bool DebugNoCeiling;
        [SerializeField]
        public bool DebugTeleporterOk;
        [SerializeField]
        public bool DebugLinks;
        public float DebugLinkVerticalOffset = 3.525f;
        public float arrowSize = 1f;
        public float arrowOffset = .51f;
        public bool percentageOffset = true;

        [SerializeField]
        public bool DebugNodes;


        [SerializeField]
        public bool DebugAirLinks;
        [SerializeField]
        public bool DebugAirNodes;
        public Color HumanColor = Color.green;
        public Color GolemColor = Color.blue;
        public Color QueenColor = Color.red;
        public Color LinkMultiplier = new Color(.75f, .75f, .75f, 1f);
        public Color NoCeilingColor = Color.cyan;
        public Color TeleporterOkColor = Color.yellow;

        private Dictionary<HullMask, Color> colormap;

#if UNITY_EDITOR

        Material GetDebugMaterial(bool nodes = false)
        {
            var sceneView = SceneView.lastActiveSceneView;
            Material material = null;
            switch (sceneView.cameraMode.drawMode)
            {
                case DrawCameraMode.Textured:
                case DrawCameraMode.Wireframe:
                case DrawCameraMode.TexturedWire:
                    if (nodes)
                        material = AssetDatabase.FindAssets("t:Material NodeMaterial").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First();
                    else
                        material = AssetDatabase.FindAssets("t:Material LinkMaterial").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First();
                    //Hidden/ProBuilder/VertexPicker
                    break;
                case DrawCameraMode.Overdraw:
                    material = new Material(Shader.Find("Hidden/UI/Overdraw"));
                    break;
            }
            material.SetPass(0);

            return material;
        }
        void OnRenderObject()
        {
            var material = GetDebugMaterial();
            if (!material) return;
            var masks = new[] { HullMask.BeetleQueen, HullMask.Golem, HullMask.Human };
            if (colormap == null)
                colormap = new Dictionary<HullMask, Color> {
                    { HullMask.Human, HumanColor },
                    { HullMask.Golem, GolemColor },
                    { HullMask.BeetleQueen, QueenColor },
                };
            else
            {
                colormap[HullMask.Human] = HumanColor;
                colormap[HullMask.Golem] = GolemColor;
                colormap[HullMask.BeetleQueen] = QueenColor;
            }
            var so = new SerializedObject(this);
            var groundNodeGraph = (NodeGraph)so.FindProperty("groundNodesAsset").objectReferenceValue;
            var airNodeGraph = (NodeGraph)so.FindProperty("airNodesAsset").objectReferenceValue;

            // Set transformation matrix for drawing to
            // match our transform
            // Draw lines
            if (airNodeGraph && (DebugAirLinks || DebugAirNodes))
            {
                var airNodes = nodesField.GetValue(airNodeGraph) as NodeGraph.Node[];
                if (DebugAirLinks)
                {
                    var airLinks = linksField.GetValue(airNodeGraph) as NodeGraph.Link[];
                    GL.PushMatrix();
                    GL.Begin(GL.LINES);
                    foreach (var link in airLinks)
                    {
                        Vector3 nodeAPos = airNodes[link.nodeIndexA.nodeIndex].position;
                        Vector3 nodeBPos = airNodes[link.nodeIndexB.nodeIndex].position;
                        material.color = HumanColor;
                        GL.Color(HumanColor);
                        if (((HullMask)link.hullMask).HasFlag(HullMask.Golem))
                        {
                            material.color = GolemColor;
                            GL.Color(GolemColor);

                        }
                        if (((HullMask)link.hullMask).HasFlag(HullMask.BeetleQueen))
                        {
                            material.color = QueenColor;
                            GL.Color(QueenColor);
                        }
                        if (((HullMask)link.hullMask).HasFlag(HullMask.BeetleQueen) && QueenColor.a < 0.05f)
                            continue;
                        if (!((HullMask)link.hullMask).HasFlag(HullMask.BeetleQueen) && ((HullMask)link.hullMask).HasFlag(HullMask.Golem) && GolemColor.a < 0.05f)
                            continue;
                        if (!((HullMask)link.hullMask).HasFlag(HullMask.Golem) && ((HullMask)link.hullMask).HasFlag(HullMask.Human) && HumanColor.a < 0.05f)
                            continue;
                        GL.Vertex3(nodeAPos.x, nodeAPos.y, nodeAPos.z);
                        GL.Vertex3(nodeBPos.x, nodeBPos.y, nodeBPos.z);
                    }
                    GL.End();
                    GL.PopMatrix();
                }
                if (DebugAirNodes)
                {
                    GetDebugMaterial(true);
                    GL.PushMatrix();
                    GL.Begin(GL.TRIANGLES);
                    foreach (var node in airNodes)
                    {
                        var position = node.position;
                        try
                        {
                            foreach (var mask in masks)
                                if (!node.forbiddenHulls.HasFlag(mask))
                                {
                                    GL.Color(colormap[mask]);

                                    for (int i = 0; i < cubeTriangles.Length; i += 3)
                                    {
                                        var a = cubeVertices[cubeTriangles[i + 0]] + position;
                                        var b = cubeVertices[cubeTriangles[i + 1]] + position;
                                        var c = cubeVertices[cubeTriangles[i + 2]] + position;
                                        GL.Vertex3(a.x, a.y, a.z);
                                        GL.Vertex3(b.x, b.y, b.z);
                                        GL.Vertex3(c.x, c.y, c.z);
                                    }

                                    position += Vector3.up;
                                }
                        }
                        catch { }
                    }
                    GL.End();
                    GL.PopMatrix();
                }
            }
            if (!groundNodeGraph) return;
            var groundNodes = nodesField.GetValue(groundNodeGraph) as NodeGraph.Node[];

            GetDebugMaterial(true);

            foreach (var node in groundNodes)
            {
                GL.PushMatrix();
                GL.Begin(GL.LINES);
                if (DebugNoCeiling && node.flags.HasFlag(NodeFlags.NoCeiling))
                {
                    GL.Color(NoCeilingColor);
                    var up = node.position + (Vector3.up * 5f);
                    GL.Vertex3(node.position.x + 0.51f, node.position.y, node.position.z);
                    GL.Vertex3(up.x + 0.51f, up.y, up.z);
                }
                if (DebugTeleporterOk && node.flags.HasFlag(NodeFlags.TeleporterOK))
                {
                    GL.Color(TeleporterOkColor);
                    var offsetPosition = node.position + (Vector3.up * 5);
                    GL.Vertex3(node.position.x - 0.51f, node.position.y, node.position.z);
                    GL.Vertex3(offsetPosition.x - 0.51f, offsetPosition.y, offsetPosition.z);
                }
                GL.End();
                GL.PopMatrix();
                if (DebugNodes)
                {
                    GL.PushMatrix();
                    GL.Begin(GL.TRIANGLES);
                    var position = node.position + Vector3.up * 0.5f;
                    try
                    {
                        foreach (var mask in masks)
                            if (!node.forbiddenHulls.HasFlag(mask))
                            {
                                GL.Color(colormap[mask]);

                                for (int i = 0; i < cubeTriangles.Length; i += 3)
                                {
                                    var a = cubeVertices[cubeTriangles[i + 0]] + position - (Vector3.right * 0.5f) - (Vector3.forward * 0.5f) - (Vector3.up * 0.5f);
                                    var b = cubeVertices[cubeTriangles[i + 1]] + position - (Vector3.right * 0.5f) - (Vector3.forward * 0.5f) - (Vector3.up * 0.5f);
                                    var c = cubeVertices[cubeTriangles[i + 2]] + position - (Vector3.right * 0.5f) - (Vector3.forward * 0.5f) - (Vector3.up * 0.5f);
                                    GL.Vertex3(a.x, a.y, a.z);
                                    GL.Vertex3(b.x, b.y, b.z);
                                    GL.Vertex3(c.x, c.y, c.z);
                                }

                                position += Vector3.up;
                            }
                    }
                    catch { }
                    GL.End();
                    GL.PopMatrix();
                }
                //SceneView.currentDrawingSceneView.camera.world
            }

            if (DebugLinks)
            {
                GetDebugMaterial(false);
                GL.PushMatrix();
                GL.Begin(GL.LINES);
                var groundLinks = linksField.GetValue(groundNodeGraph) as NodeGraph.Link[];
                foreach (var link in groundLinks)
                {
                    foreach (var mask in masks)
                        if (((HullMask)link.hullMask).HasFlag(mask))
                        {
                            try
                            {
                                var color = colormap[mask];
                                GL.Color(color * LinkMultiplier);

                                NodeGraph.Node nodeA = groundNodes[link.nodeIndexA.nodeIndex];
                                NodeGraph.Node nodeB = groundNodes[link.nodeIndexB.nodeIndex];
                                Vector3 nodeAPos = nodeA.position;
                                Vector3 nodeBPos = nodeB.position;
                                var displacement = nodeBPos - nodeAPos;
                                var linkDirection = displacement.normalized;

                                const float arrowHeight = 1f;
                                var nodeAModA = nodeAPos + (linkDirection * 1) + Vector3.up * arrowHeight;
                                var nodeAModB = nodeAPos + (linkDirection * 2);

                                GL.Vertex3(nodeAPos.x, nodeAPos.y, nodeAPos.z);
                                GL.Vertex3(nodeAPos.x, nodeAPos.y + DebugLinkVerticalOffset, nodeAPos.z);

                                GL.Vertex3(nodeAPos.x, nodeAPos.y + DebugLinkVerticalOffset, nodeAPos.z);
                                GL.Vertex3(nodeBPos.x, nodeBPos.y + DebugLinkVerticalOffset, nodeBPos.z);
                            }
                            catch { }
                            break;
                        }
                }
                GL.End();
                GL.Begin(GL.TRIANGLES);
                foreach (var link in groundLinks)
                {
                    foreach (var mask in masks)
                        if (((HullMask)link.hullMask).HasFlag(mask))
                        {
                            try
                            {
                                var color = colormap[mask];
                                GL.Color(color * LinkMultiplier);

                                NodeGraph.Node nodeA = groundNodes[link.nodeIndexA.nodeIndex];
                                NodeGraph.Node nodeB = groundNodes[link.nodeIndexB.nodeIndex];
                                Vector3 nodeAPos = nodeA.position;
                                Vector3 nodeBPos = nodeB.position;
                                var displacement = nodeBPos - nodeAPos;
                                var linkDirection = displacement.normalized;

                                float halfArrowWidth = arrowSize / 2;

                                var offset = percentageOffset ? Mathf.Clamp(arrowOffset, 0, 1) * displacement.magnitude : arrowOffset;
                                var nodeAModA = nodeAPos + (linkDirection * offset);
                                var nodeAModB = nodeAPos + (linkDirection * (offset + arrowSize));

                                var linkCross = Vector3.Cross(linkDirection, Vector3.up).normalized;

                                var a = nodeAModA + (linkCross * halfArrowWidth);
                                var b = nodeAModA - (linkCross * halfArrowWidth);

                                GL.Vertex3(a.x, a.y + DebugLinkVerticalOffset, a.z);
                                GL.Vertex3(b.x, b.y + DebugLinkVerticalOffset, b.z);
                                GL.Vertex3(nodeAModB.x, nodeAModB.y + DebugLinkVerticalOffset, nodeAModB.z);
                            }
                            catch { }
                            break;
                        }
                }
                GL.End();
                GL.PopMatrix();
            }

        }


        private static readonly Vector3[] cubeVertices = new[]
        {
                    new Vector3 (0, 0, 0),
                    new Vector3 (1, 0, 0),
                    new Vector3 (1, 1, 0),
                    new Vector3 (0, 1, 0),
                    new Vector3 (0, 1, 1),
                    new Vector3 (1, 1, 1),
                    new Vector3 (1, 0, 1),
                    new Vector3 (0, 0, 1)
        };
        private static readonly int[] cubeTriangles = new[]
        {
            0, 2, 1,
            0, 3, 2,
            2, 3, 4,
            2, 4, 5,
            1, 2, 5,
            1, 5, 6,
            0, 7, 4,
            0, 4, 3,
            5, 4, 7,
            5, 7, 6,
            0, 6, 7,
            0, 1, 6
        };
#endif
    }
}
