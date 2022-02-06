using PassivePicasso.RainOfStages.Plugin.Navigation;
using PassivePicasso.RainOfStages.Plugin.Utility;
using RoR2;
using RoR2.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PassivePicasso.RainOfStages.Designer
{
    [CustomEditor(typeof(SceneInfo), true)]
    public class SceneInfoEditor : Editor
    {
        static FieldInfo nodesField = typeof(NodeGraph).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo linksField = typeof(NodeGraph).GetField("links", BindingFlags.NonPublic | BindingFlags.Instance);
        enum LinkMeshType { line, arrow }

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
        private static readonly HullMask[] masks = new[] { HullMask.BeetleQueen, HullMask.Golem, HullMask.Human };

        public static bool DebugNoCeiling;
        public static bool DebugTeleporterOk;

        public static bool DebugAirLinks;
        public static bool DebugAirNodes;

        public static bool DebugGroundNodes;
        public static bool DebugGroundLinks;

        public static float VerticalOffset = 3.525f;
        public static float arrowSize = 1f;
        public static float arrowOffset = .51f;
        public static bool percentageOffset = true;

        public static Color HumanColor = Color.green;
        public static Color GolemColor = Color.blue;
        public static Color QueenColor = Color.red;
        public static Color NoCeilingColor = Color.cyan;
        public static Color TeleporterOkColor = Color.yellow;

        #region private fields
        private static Dictionary<HullMask, Color> colormap;
        private static Material linkMaterial;
        private static Material nodeMaterial;
        private static NodeGraph groundNodeGraph;
        private static NodeGraph airNodeGraph;
        private static NodeGraph.Node[] airNodes;
        private static NodeGraph.Node[] groundNodes;
        private static NodeGraph.Link[] airLinks;
        private static NodeGraph.Link[] groundLinks;

        private static Mesh airLinkLineMesh;
        private static Mesh airLinkArrowMesh;
        private static Mesh airNodeMesh;

        private static Mesh groundLinkLineMesh;
        private static Mesh groundLinkArrowMesh;
        private static Mesh groundNodeMesh;

        private static Mesh teleporterOkMesh;
        private static Mesh noCeilingMesh;
        private static bool regenerateMesh;
        private static bool repaint;
        #endregion


        public override void OnInspectorGUI()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "approximateMapBoundMesh", "groundNodeGroup", "airNodeGroup", "railNodeGroup");

            LoadDebugValues();
            repaint |= CheckedField(() => DebugNoCeiling = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugNoCeiling)), DebugNoCeiling));
            if (DebugNoCeiling)
                regenerateMesh |= CheckedField(() => NoCeilingColor = EditorGUILayout.ColorField(NoCeilingColor), ObjectNames.NicifyVariableName(nameof(NoCeilingColor)));

            repaint |= CheckedField(() => DebugTeleporterOk = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugTeleporterOk)), DebugTeleporterOk));
            if (DebugTeleporterOk)
                regenerateMesh |= CheckedField(() => TeleporterOkColor = EditorGUILayout.ColorField(TeleporterOkColor), ObjectNames.NicifyVariableName(nameof(TeleporterOkColor)));

            repaint |= CheckedField(() => DebugGroundNodes = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugGroundNodes)), DebugGroundNodes));
            repaint |= CheckedField(() => DebugGroundLinks = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugGroundLinks)), DebugGroundLinks));
            if (DebugGroundLinks)
            {
                regenerateMesh |= CheckedField(() => VerticalOffset = EditorGUILayout.Slider(ObjectNames.NicifyVariableName(nameof(VerticalOffset)), VerticalOffset, 0, 20));
                regenerateMesh |= CheckedField(() => arrowSize = EditorGUILayout.Slider(ObjectNames.NicifyVariableName(nameof(arrowSize)), arrowSize, 1, 10));
                regenerateMesh |= CheckedField(() => arrowOffset = EditorGUILayout.Slider(ObjectNames.NicifyVariableName(nameof(arrowOffset)), arrowOffset, 0, percentageOffset ? 1 : 20));
                repaint |= CheckedField(() => percentageOffset = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(percentageOffset)), percentageOffset));

            }

            repaint |= CheckedField(() => DebugAirNodes = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugAirNodes)), DebugAirNodes));
            repaint |= CheckedField(() => DebugAirLinks = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugAirLinks)), DebugAirLinks));

            if (DebugGroundLinks || DebugGroundNodes || DebugAirLinks || DebugAirNodes)
            {
                regenerateMesh |= CheckedField(() => HumanColor = EditorGUILayout.ColorField(ObjectNames.NicifyVariableName(nameof(HumanColor)), HumanColor));
                regenerateMesh |= CheckedField(() => GolemColor = EditorGUILayout.ColorField(ObjectNames.NicifyVariableName(nameof(GolemColor)), GolemColor));
                regenerateMesh |= CheckedField(() => QueenColor = EditorGUILayout.ColorField(ObjectNames.NicifyVariableName(nameof(QueenColor)), QueenColor));
            }
            repaint |= regenerateMesh;

            SaveDebugValues();

        }

        [InitializeOnLoadMethod]
        static void InitializeDebugDrawer()
        {
            linkMaterial = GetDebugMaterial();
            nodeMaterial = GetDebugMaterial(true);
            LoadDebugValues();
            OnBuilt();
            Camera.onPreCull -= Draw;
            Camera.onPreCull += Draw;
            EditorApplication.update -= UpdateMeshCheck;
            EditorApplication.update += UpdateMeshCheck;
            GraphBuilder.OnBuilt -= OnBuilt;
            GraphBuilder.OnBuilt += OnBuilt;
        }

        private static void OnBuilt()
        {
            regenerateMesh = true;
            repaint = true;
        }

        private static void Draw(Camera camera)
        {
            if (DebugTeleporterOk && teleporterOkMesh) Graphics.DrawMesh(teleporterOkMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugNoCeiling && noCeilingMesh) Graphics.DrawMesh(noCeilingMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugGroundNodes && groundNodeMesh) Graphics.DrawMesh(groundNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugAirNodes && airNodeMesh) Graphics.DrawMesh(airNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugAirLinks)
            {
                if (airLinkLineMesh)
                    Graphics.DrawMesh(airLinkLineMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
                if (airLinkArrowMesh)
                    Graphics.DrawMesh(airLinkArrowMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            }
            if (DebugGroundLinks)
            {
                if (groundLinkLineMesh)
                    Graphics.DrawMesh(groundLinkLineMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
                if (groundLinkArrowMesh)
                    Graphics.DrawMesh(groundLinkArrowMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            }
        }

        private static void UpdateMeshCheck()
        {
            if (repaint)
            {
                var sceneInfo = FindObjectOfType<SceneInfo>();
                var serializedObject = new SerializedObject(sceneInfo);
                groundNodeGraph = (NodeGraph)serializedObject.FindProperty("groundNodesAsset").objectReferenceValue;
                if (groundNodeGraph)
                {
                    groundNodes = nodesField.GetValue(groundNodeGraph) as NodeGraph.Node[];
                    groundLinks = linksField.GetValue(groundNodeGraph) as NodeGraph.Link[];
                }

                airNodeGraph = (NodeGraph)serializedObject.FindProperty("airNodesAsset").objectReferenceValue;
                if (airNodeGraph)
                {
                    airNodes = nodesField.GetValue(airNodeGraph) as NodeGraph.Node[];
                    airLinks = linksField.GetValue(airNodeGraph) as NodeGraph.Link[];
                }
                RegenerateMeshes();

                SceneView.lastActiveSceneView.Repaint();
                repaint = false;
            }
        }

        private static void SaveDebugValues()
        {
            EditorPrefs.SetBool(GUIDName(nameof(DebugNoCeiling)), DebugNoCeiling);
            EditorPrefs.SetBool(GUIDName(nameof(DebugTeleporterOk)), DebugTeleporterOk);
            EditorPrefs.SetBool(GUIDName(nameof(DebugAirLinks)), DebugAirLinks);
            EditorPrefs.SetBool(GUIDName(nameof(DebugAirNodes)), DebugAirNodes);
            EditorPrefs.SetBool(GUIDName(nameof(DebugGroundNodes)), DebugGroundNodes);
            EditorPrefs.SetBool(GUIDName(nameof(DebugGroundLinks)), DebugGroundLinks);

            EditorPrefs.SetString(GUIDName(nameof(NoCeilingColor)), JsonUtility.ToJson(NoCeilingColor));
            EditorPrefs.SetString(GUIDName(nameof(TeleporterOkColor)), JsonUtility.ToJson(TeleporterOkColor));
            EditorPrefs.SetString(GUIDName(nameof(HumanColor)), JsonUtility.ToJson(HumanColor));
            EditorPrefs.SetString(GUIDName(nameof(GolemColor)), JsonUtility.ToJson(GolemColor));
            EditorPrefs.SetString(GUIDName(nameof(QueenColor)), JsonUtility.ToJson(QueenColor));
        }

        private static void LoadDebugValues()
        {
            if (EditorPrefs.HasKey(GUIDName(nameof(DebugNoCeiling)))) DebugNoCeiling = EditorPrefs.GetBool(GUIDName(nameof(DebugNoCeiling)));
            if (EditorPrefs.HasKey(GUIDName(nameof(DebugTeleporterOk)))) DebugTeleporterOk = EditorPrefs.GetBool(GUIDName(nameof(DebugTeleporterOk)));
            if (EditorPrefs.HasKey(GUIDName(nameof(DebugAirLinks)))) DebugAirLinks = EditorPrefs.GetBool(GUIDName(nameof(DebugAirLinks)));
            if (EditorPrefs.HasKey(GUIDName(nameof(DebugAirNodes)))) DebugAirNodes = EditorPrefs.GetBool(GUIDName(nameof(DebugAirNodes)));
            if (EditorPrefs.HasKey(GUIDName(nameof(DebugGroundNodes)))) DebugGroundNodes = EditorPrefs.GetBool(GUIDName(nameof(DebugGroundNodes)));
            if (EditorPrefs.HasKey(GUIDName(nameof(DebugGroundLinks)))) DebugGroundLinks = EditorPrefs.GetBool(GUIDName(nameof(DebugGroundLinks)));

            if (EditorPrefs.HasKey(GUIDName(nameof(NoCeilingColor)))) NoCeilingColor = JsonUtility.FromJson<Color>(EditorPrefs.GetString(GUIDName(nameof(NoCeilingColor))));
            if (EditorPrefs.HasKey(GUIDName(nameof(TeleporterOkColor)))) TeleporterOkColor = JsonUtility.FromJson<Color>(EditorPrefs.GetString(GUIDName(nameof(TeleporterOkColor))));
            if (EditorPrefs.HasKey(GUIDName(nameof(HumanColor)))) HumanColor = JsonUtility.FromJson<Color>(EditorPrefs.GetString(GUIDName(nameof(HumanColor))));
            if (EditorPrefs.HasKey(GUIDName(nameof(GolemColor)))) GolemColor = JsonUtility.FromJson<Color>(EditorPrefs.GetString(GUIDName(nameof(GolemColor))));
            if (EditorPrefs.HasKey(GUIDName(nameof(QueenColor)))) QueenColor = JsonUtility.FromJson<Color>(EditorPrefs.GetString(GUIDName(nameof(QueenColor))));
        }

        private static void RegenerateMeshes()
        {
            regenerateMesh = false;
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

            if (groundNodeGraph)
            {
                groundLinkLineMesh = GenerateLinkMesh(LinkMeshType.line, groundNodes, groundLinks, VerticalOffset);
                groundLinkArrowMesh = GenerateLinkMesh(LinkMeshType.arrow, groundNodes, groundLinks, VerticalOffset);
                groundNodeMesh = GenerateNodeMesh(groundNodes);
                teleporterOkMesh = GenerateNodeExtraMesh(groundNodes, Vector3.right, TeleporterOkColor, node => node.flags.HasFlag(NodeFlags.TeleporterOK));
                noCeilingMesh = GenerateNodeExtraMesh(groundNodes, Vector3.left, NoCeilingColor, node => node.flags.HasFlag(NodeFlags.NoCeiling));
            }

            if (airNodeGraph)
            {
                airLinkLineMesh = GenerateLinkMesh(LinkMeshType.line, airNodes, airLinks, 0);
                airLinkArrowMesh = GenerateLinkMesh(LinkMeshType.arrow, airNodes, airLinks, 0);
                airNodeMesh = GenerateNodeMesh(airNodes);
            }

        }

        bool CheckedField(Action drawField, string label = null)
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (!string.IsNullOrEmpty(label))
                    EditorGUILayout.LabelField(label);
                drawField();
            }
            return EditorGUI.EndChangeCheck();
        }

        private static Mesh GenerateNodeExtraMesh(NodeGraph.Node[] nodes, Vector3 offset, Color color, Predicate<NodeGraph.Node> predicate)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color>();

            void Add(Vector3 a, Color vc)
            {
                indices.Add(vertices.Count);
                vertices.Add(a);
                colors.Add(vc);
            }
            void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color vc) { Add(a, vc); Add(b, vc); Add(c, vc); }

            foreach (var node in nodes)
                if (predicate(node))
                    for (int i = 0; i < cubeTriangles.Length; i += 3)
                        AddTriangle(
                            cubeVertices[cubeTriangles[i + 0]] + node.position + offset,
                            cubeVertices[cubeTriangles[i + 1]] + node.position + offset,
                            cubeVertices[cubeTriangles[i + 2]] + node.position + offset,
                            color);

            return GetMesh(vertices, indices, colors, MeshTopology.Triangles);
        }
        private static Mesh GenerateNodeMesh(NodeGraph.Node[] nodes)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            var colors = new List<Color>();

            void Add(Vector3 a, Color vc)
            {
                indices.Add(vertices.Count);
                vertices.Add(a);
                colors.Add(vc);
            }
            void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color vc) { Add(a, vc); Add(b, vc); Add(c, vc); }

            foreach (var node in nodes)
            {
                var position = node.position;
                try
                {
                    foreach (var mask in masks)
                        if (!node.forbiddenHulls.HasFlag(mask))
                        {
                            for (int i = 0; i < cubeTriangles.Length; i += 3)
                                AddTriangle(
                                    cubeVertices[cubeTriangles[i + 0]] + position,
                                    cubeVertices[cubeTriangles[i + 1]] + position,
                                    cubeVertices[cubeTriangles[i + 2]] + position,
                                    colormap[mask]);

                            position += Vector3.up;
                        }
                }
                catch { }
            }

            return GetMesh(vertices, indices, colors, MeshTopology.Triangles);
        }
        private static Mesh GenerateLinkMesh(LinkMeshType linkMeshType, NodeGraph.Node[] nodes, NodeGraph.Link[] links, float verticalOffset)
        {
            if (linkMeshType != LinkMeshType.arrow && linkMeshType != LinkMeshType.line)
                throw new System.ArgumentException(nameof(linkMeshType));

            var vertices = new List<Vector3>();
            var edges = new List<int>();
            var triangles = new List<int>();
            var colors = new List<Color>();
            void Add(Vector3 a, Color vc, List<int> subMeshIndices)
            {
                subMeshIndices.Add(vertices.Count);
                vertices.Add(a);
                colors.Add(vc);
            }
            void AddLine(Vector3 a, Vector3 b, Color vc)
            {
                Add(a, vc, edges);
                Add(b, vc, edges);
            }
            void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color vc)
            {
                Add(a, vc, triangles);
                Add(b, vc, triangles);
                Add(c, vc, triangles);
            }
            foreach (var link in links)
            {
                for (int i = 0; i < masks.Length; i++)
                {
                    var mask = masks[i];
                    if (((HullMask)link.hullMask).HasFlag(mask))
                    {
                        var color = colormap[mask];
                        NodeGraph.Node nodeA = nodes[link.nodeIndexA.nodeIndex];
                        NodeGraph.Node nodeB = nodes[link.nodeIndexB.nodeIndex];
                        Vector3 nodeAPos = nodeA.position;
                        Vector3 nodeBPos = nodeB.position;
                        var displacement = nodeBPos - nodeAPos;
                        var linkDirection = displacement.normalized;

                        const float arrowHeight = 1f;
                        var nodeAModA = nodeAPos + (linkDirection * 1) + Vector3.up * arrowHeight;
                        var nodeAModB = nodeAPos + (linkDirection * 2);
                        switch (linkMeshType)
                        {
                            case LinkMeshType.line:
                                if (verticalOffset > 0)
                                    AddLine(new Vector3(nodeAPos.x, nodeAPos.y, nodeAPos.z),
                                            new Vector3(nodeAPos.x, nodeAPos.y + verticalOffset, nodeAPos.z), color);

                                AddLine(new Vector3(nodeAPos.x, nodeAPos.y + verticalOffset, nodeAPos.z),
                                        new Vector3(nodeBPos.x, nodeBPos.y + verticalOffset, nodeBPos.z), color);
                                break;
                            case LinkMeshType.arrow:
                                float halfArrowWidth = arrowSize / 2;

                                var offset = percentageOffset ? Mathf.Clamp(arrowOffset, 0, 1) * displacement.magnitude : arrowOffset;
                                nodeAModA = nodeAPos + (linkDirection * offset);
                                nodeAModB = nodeAPos + (linkDirection * (offset + arrowSize));

                                var linkCross = Vector3.Cross(linkDirection, Vector3.up).normalized;

                                var a = nodeAModA + (linkCross * halfArrowWidth);
                                var b = nodeAModA - (linkCross * halfArrowWidth);

                                AddTriangle(new Vector3(a.x, a.y + verticalOffset, a.z),
                                            new Vector3(b.x, b.y + verticalOffset, b.z),
                                            new Vector3(nodeAModB.x, nodeAModB.y + verticalOffset, nodeAModB.z), color);
                                break;
                        }
                        break;
                    }
                }
            }

            if (linkMeshType == LinkMeshType.line)
                return GetMesh(vertices, edges, colors, MeshTopology.Lines);
            else
            if (linkMeshType == LinkMeshType.arrow)
                return GetMesh(vertices, triangles, colors, MeshTopology.Triangles);

            return null;
        }
        private static Mesh GetMesh(List<Vector3> linkEnds, List<int> linkEdges, List<Color> colors, MeshTopology topology)
        {
            var linkMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            linkMesh.SetVertices(linkEnds);
            linkMesh.SetIndices(linkEdges.ToArray(), topology, 0);
            linkMesh.SetColors(colors);

            return linkMesh;
        }

        private static Material GetDebugMaterial(bool nodes = false)
        {
            Material material = null;

            var sceneView = SceneView.lastActiveSceneView;

            if (!sceneView)
                sceneView = SceneView.currentDrawingSceneView;

            if (!sceneView)
                material = AssetDatabase.LoadAssetAtPath<Material>(PathHelper.RoSPath("RoSShared", "Materials", "NodeMaterial.mat"));
            else
                switch (sceneView.cameraMode.drawMode)
                {
                    case DrawCameraMode.Textured:
                    case DrawCameraMode.Wireframe:
                    case DrawCameraMode.TexturedWire:
                        if (nodes)
                            material = AssetDatabase.LoadAssetAtPath<Material>(PathHelper.RoSPath("RoSShared", "Materials", "NodeMaterial.mat"));
                        else
                            material = AssetDatabase.LoadAssetAtPath<Material>(PathHelper.RoSPath("RoSShared", "Materials", "LinkMaterial.mat"));
                        break;
                    case DrawCameraMode.Overdraw:
                        material = new Material(Shader.Find("Hidden/UI/Overdraw"));
                        break;
                }

            return material;
        }

        static string GUIDName(string value)
        {
            value = $"ThunderKit_RoS_{value}";

            using (var md5 = MD5.Create())
            {
                byte[] shortNameBytes = Encoding.UTF8.GetBytes(value);
                var shortNameHash = md5.ComputeHash(shortNameBytes);
                var guid = new Guid(shortNameHash);
                var cleanedGuid = guid.ToString().ToLower().Replace("-", "");
                return cleanedGuid;
            }
        }
    }
}
