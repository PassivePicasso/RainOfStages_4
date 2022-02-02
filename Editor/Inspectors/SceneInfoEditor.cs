using RoR2;
using RoR2.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

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


        public bool DebugNoCeiling;
        public bool DebugTeleporterOk;

        public bool DebugAirLinks;
        public bool DebugAirNodes;

        public bool DebugGroundNodes;
        public bool DebugGroundLinks;

        public float VerticalOffset = 3.525f;
        public float arrowSize = 1f;
        public float arrowOffset = .51f;
        public bool percentageOffset = true;

        public Color HumanColor = Color.green;
        public Color GolemColor = Color.blue;
        public Color QueenColor = Color.red;
        public Color NoCeilingColor = Color.cyan;
        public Color TeleporterOkColor = Color.yellow;

        #region private fields
        private Dictionary<HullMask, Color> colormap;
        private Material linkMaterial;
        private Material nodeMaterial;
        private NodeGraph groundNodeGraph;
        private NodeGraph airNodeGraph;
        private NodeGraph.Node[] airNodes;
        private NodeGraph.Node[] groundNodes;
        private NodeGraph.Link[] airLinks;
        private NodeGraph.Link[] groundLinks;

        private Mesh airLinkLineMesh;
        private Mesh airNodeMesh;

        private Mesh groundLinkLineMesh;
        private Mesh groundLinkArrowMesh;
        private Mesh groundNodeMesh;

        private Mesh teleporterOkMesh;
        private Mesh noCeilingMesh;
        #endregion
        private void OnEnable()
        {
            linkMaterial = GetDebugMaterial();
            nodeMaterial = GetDebugMaterial(true);

            groundNodeGraph = (NodeGraph)serializedObject.FindProperty("groundNodesAsset").objectReferenceValue;
            groundNodes = nodesField.GetValue(groundNodeGraph) as NodeGraph.Node[];
            groundLinks = linksField.GetValue(groundNodeGraph) as NodeGraph.Link[];

            airNodeGraph = (NodeGraph)serializedObject.FindProperty("airNodesAsset").objectReferenceValue;
            airNodes = nodesField.GetValue(airNodeGraph) as NodeGraph.Node[];
            airLinks = linksField.GetValue(airNodeGraph) as NodeGraph.Link[];
            RegenerateMeshes();

            Camera.onPreCull -= Draw;
            Camera.onPreCull += Draw;
        }

        private void OnDisable()
        {
            Camera.onPreCull -= Draw;
        }

        public override bool RequiresConstantRepaint()
        {
            return true;
        }
        public override void OnInspectorGUI()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "approximateMapBoundMesh", "groundNodeGroup", "airNodeGroup", "railNodeGroup");

            var regenerateMesh = false;
            CheckedField(() => DebugNoCeiling = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugNoCeiling)), DebugNoCeiling));
            if (DebugNoCeiling)
                regenerateMesh |= CheckedField(() => NoCeilingColor = EditorGUILayout.ColorField(NoCeilingColor), ObjectNames.NicifyVariableName(nameof(NoCeilingColor)));

            CheckedField(() => DebugTeleporterOk = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugTeleporterOk)), DebugTeleporterOk));
            if (DebugTeleporterOk)
                regenerateMesh |= CheckedField(() => TeleporterOkColor = EditorGUILayout.ColorField(TeleporterOkColor), ObjectNames.NicifyVariableName(nameof(TeleporterOkColor)));

            CheckedField(() => DebugGroundNodes = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugGroundNodes)), DebugGroundNodes));
            CheckedField(() => DebugGroundLinks = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugGroundLinks)), DebugGroundLinks));
            if (DebugGroundLinks)
            {
                regenerateMesh |= CheckedField(() => VerticalOffset = EditorGUILayout.Slider(ObjectNames.NicifyVariableName(nameof(VerticalOffset)), VerticalOffset, 0, 20));
                regenerateMesh |= CheckedField(() => arrowSize = EditorGUILayout.Slider(ObjectNames.NicifyVariableName(nameof(arrowSize)), arrowSize, 1, 10));
                regenerateMesh |= CheckedField(() => arrowOffset = EditorGUILayout.Slider(ObjectNames.NicifyVariableName(nameof(arrowOffset)), arrowOffset, 0, percentageOffset ? 1 : 20));
                CheckedField(() => percentageOffset = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(percentageOffset)), percentageOffset));

            }

            CheckedField(() => DebugAirNodes = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugAirNodes)), DebugAirNodes));
            CheckedField(() => DebugAirLinks = EditorGUILayout.Toggle(ObjectNames.NicifyVariableName(nameof(DebugAirLinks)), DebugAirLinks));

            if (DebugGroundLinks || DebugGroundNodes || DebugAirLinks || DebugAirNodes)
            {
                regenerateMesh |= CheckedField(() => HumanColor = EditorGUILayout.ColorField(ObjectNames.NicifyVariableName(nameof(HumanColor)), HumanColor));
                regenerateMesh |= CheckedField(() => GolemColor = EditorGUILayout.ColorField(ObjectNames.NicifyVariableName(nameof(GolemColor)), GolemColor));
                regenerateMesh |= CheckedField(() => QueenColor = EditorGUILayout.ColorField(ObjectNames.NicifyVariableName(nameof(QueenColor)), QueenColor));
            }

            if (regenerateMesh)
                RegenerateMeshes();
        }

        private void RegenerateMeshes()
        {
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

            groundLinkLineMesh = GenerateGroundLinkMesh(LinkMeshType.line, groundNodes, groundLinks);
            groundLinkArrowMesh = GenerateGroundLinkMesh(LinkMeshType.arrow, groundNodes, groundLinks);
            groundNodeMesh = GenerateNodeMesh(groundNodes);

            airLinkLineMesh = GenerateAirLinkMesh(airNodes, airLinks);
            airNodeMesh = GenerateNodeMesh(airNodes);

            teleporterOkMesh = GenerateNodeExtraMesh(groundNodes, Vector3.right, TeleporterOkColor, node => node.flags.HasFlag(NodeFlags.TeleporterOK));
            noCeilingMesh = GenerateNodeExtraMesh(groundNodes, Vector3.left, NoCeilingColor, node => node.flags.HasFlag(NodeFlags.NoCeiling));
        }

        private void Draw(Camera camera)
        {
            if (DebugTeleporterOk) Graphics.DrawMesh(teleporterOkMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugNoCeiling) Graphics.DrawMesh(noCeilingMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugGroundNodes) Graphics.DrawMesh(groundNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugAirNodes) Graphics.DrawMesh(airNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugAirLinks) Graphics.DrawMesh(airLinkLineMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            if (DebugGroundLinks)
            {
                Graphics.DrawMesh(groundLinkLineMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
                Graphics.DrawMesh(groundLinkArrowMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, SceneView.lastActiveSceneView.camera, 0);
            }
        }

        bool CheckedField(System.Action drawField, string label = null)
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

        Mesh GenerateNodeExtraMesh(NodeGraph.Node[] nodes, Vector3 offset, Color color, Predicate<NodeGraph.Node> predicate)
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
        Mesh GenerateNodeMesh(NodeGraph.Node[] nodes)
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

        Mesh GenerateGroundLinkMesh(LinkMeshType linkMeshType, NodeGraph.Node[] nodes, NodeGraph.Link[] links)
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
                                AddLine(new Vector3(nodeAPos.x, nodeAPos.y, nodeAPos.z),
                                        new Vector3(nodeAPos.x, nodeAPos.y + VerticalOffset, nodeAPos.z), color);

                                AddLine(new Vector3(nodeAPos.x, nodeAPos.y + VerticalOffset, nodeAPos.z),
                                        new Vector3(nodeBPos.x, nodeBPos.y + VerticalOffset, nodeBPos.z), color);
                                break;
                            case LinkMeshType.arrow:
                                float halfArrowWidth = arrowSize / 2;

                                var offset = percentageOffset ? Mathf.Clamp(arrowOffset, 0, 1) * displacement.magnitude : arrowOffset;
                                nodeAModA = nodeAPos + (linkDirection * offset);
                                nodeAModB = nodeAPos + (linkDirection * (offset + arrowSize));

                                var linkCross = Vector3.Cross(linkDirection, Vector3.up).normalized;

                                var a = nodeAModA + (linkCross * halfArrowWidth);
                                var b = nodeAModA - (linkCross * halfArrowWidth);

                                AddTriangle(new Vector3(a.x, a.y + VerticalOffset, a.z),
                                            new Vector3(b.x, b.y + VerticalOffset, b.z),
                                            new Vector3(nodeAModB.x, nodeAModB.y + VerticalOffset, nodeAModB.z), color);
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

        Mesh GenerateAirLinkMesh(NodeGraph.Node[] nodes, NodeGraph.Link[] links)
        {
            var linkEnds = new List<Vector3>();
            var linkEdges = new List<int>();
            var colors = new List<Color>();
            foreach (var link in links)
            {
                var nodeA = nodes[link.nodeIndexA.nodeIndex];
                var nodeB = nodes[link.nodeIndexB.nodeIndex];
                linkEnds.Add(nodeA.position);
                linkEdges.Add(linkEdges.Count);
                linkEnds.Add(nodeB.position);
                linkEdges.Add(linkEdges.Count);
                var addedColors = false;
                foreach (var hull in masks)
                    if (((HullMask)link.hullMask).HasFlag(hull))
                    {
                        colors.Add(colormap[hull]);
                        colors.Add(colormap[hull]);
                        addedColors = true;
                        break;
                    }
                if (!addedColors)
                {
                    colors.Add(Color.black);
                    colors.Add(Color.black);
                }
            }
            return GetMesh(linkEnds, linkEdges, colors, MeshTopology.Lines);
        }

        private static Mesh GetMesh(List<Vector3> linkEnds, List<int> linkEdges, List<Color> colors, MeshTopology topology)
        {
            var linkMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            linkMesh.SetVertices(linkEnds);
            linkMesh.SetIndices(linkEdges.ToArray(), topology, 0);
            linkMesh.SetColors(colors);

            return linkMesh;
        }

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

            return material;
        }
    }
}
