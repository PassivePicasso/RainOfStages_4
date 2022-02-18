using PassivePicasso.RainOfStages.Configurators;
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

namespace PassivePicasso.RainOfStages.Designer
{
    public static class GraphDebugDrawers
    {
        private const string RainOfStagesSceneInfoEditorSettings = nameof(RainOfStagesSceneInfoEditorSettings);
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

        public static DebugSettings DebugSettings = new DebugSettings
        {
            VerticalOffset = 1f,
            ArrowSize = 1f,
            ArrowOffset = .51f,
            PercentageOffset = true,
            HumanColor = Color.green,
            GolemColor = Color.blue,
            QueenColor = Color.red,
            NoCeilingColor = Color.cyan,
            TeleporterOkColor = Color.yellow,
            HullMask = (int)(HullMask.Human | HullMask.Golem | HullMask.BeetleQueen)
        };

        private static Material triangleMaterial;
        private static Mesh cube;
        private static Dictionary<NavigationProbe, Mesh> probeMeshes = new Dictionary<NavigationProbe, Mesh>();
        private static Dictionary<NavigationProbe, Color> probeColors = new Dictionary<NavigationProbe, Color>();

        private static Vector2 scrollPosition;

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
        private static bool regenerateLineMesh, regenerateArrowMesh, regenerateNodeMesh;
        private static bool repaint;
        private static GUIContent
            hullMaskContent,
            debugNoCeilingContent,
            debugTeleporterOkContent,
            debugGroundNodesContent,
            debugAirNodesContent,
            debugGroundLinksContent,
            debugAirLinksContent,
            verticalOffsetContent,
            arrowSizeContent,
            arrowOffsetContent,
            percentageOffsetContent,
            humanColorContent,
            golemColorContent,
            queenColorContent,
            probeLineOfSightOverlayContent,
            noCeilingColorContent,
            teleporterOkColorContent;
        #endregion


        [InitializeOnLoadMethod]
        static void InitializeDebugDrawer()
        {
            LoadDebugValues();
            triangleMaterial = AssetDatabase.LoadAssetAtPath<Material>(PathHelper.RoSPath("RoSShared", "Materials", "VertexColor.mat"));
            linkMaterial = GetDebugMaterial();
            nodeMaterial = GetDebugMaterial(true);
            var cubePrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMf = cubePrim.GetComponent<MeshFilter>();
            cube = cubeMf.sharedMesh;
            GameObject.DestroyImmediate(cubePrim);

            LoadDebugValues();
            OnBuilt();
            Camera.onPreCull -= Draw;
            Camera.onPreCull += Draw;
            EditorApplication.update -= UpdateMeshCheck;
            EditorApplication.update += UpdateMeshCheck;
            GraphBuilder.OnBuilt -= OnBuilt;
            GraphBuilder.OnBuilt += OnBuilt;
            SceneView.onSceneGUIDelegate -= ExternalSceneGui;
            SceneView.onSceneGUIDelegate += ExternalSceneGui;
            GenerateGUIContent();
        }

        static void GenerateGUIContent()
        {
            hullMaskContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.HullMask)));
            debugNoCeilingContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugNoCeiling)));
            debugTeleporterOkContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugTeleporterOk)));
            debugGroundNodesContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugGroundNodes)));
            debugAirNodesContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugAirNodes)));
            debugGroundLinksContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugGroundLinks)));
            debugAirLinksContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugAirLinks)));
            verticalOffsetContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.VerticalOffset)));
            arrowSizeContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.ArrowSize)));
            arrowOffsetContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.ArrowOffset)));
            percentageOffsetContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.PercentageOffset)));
            humanColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.HumanColor)));
            golemColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.GolemColor)));
            queenColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.QueenColor)));
            probeLineOfSightOverlayContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.ProbeLineOfSightOverlay)));
            noCeilingColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.NoCeilingColor)));
            teleporterOkColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.TeleporterOkColor)));
        }

        private static void ExternalSceneGui(SceneView sceneView)
        {
            var probe = GameObject.FindObjectOfType<NavigationProbe>();
            var groundGraphBuilders = GameObject.FindObjectOfType<GroundGraphBuilder>();
            var airGraphBuilders = GameObject.FindObjectOfType<AirGraphBuilder>();
            if (probe || groundGraphBuilders || airGraphBuilders)
            {
                var height = CalculateHeight();
                var width = CalculateWidth();
                Rect screenRect = new Rect(4, 4, width, height);


                Handles.BeginGUI();
                GUILayout.BeginArea(screenRect, EditorStyles.helpBox);
                EditorGUI.BeginChangeCheck();
                try
                {
                    if (DebugSettings.ShowGraphTools && GUILayout.Button(new GUIContent("X"), GUILayout.Width(EditorGUIUtility.singleLineHeight), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                        DebugSettings.ShowGraphTools = false;
                    if (!DebugSettings.ShowGraphTools && GUILayout.Button(new GUIContent(">"), GUILayout.Width(EditorGUIUtility.singleLineHeight), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
                        DebugSettings.ShowGraphTools = true;

                    if (!DebugSettings.ShowGraphTools) return;

                    if (GUILayout.Button(new GUIContent("Build Ground Graph")))
                    {
                        foreach (var ggb in GameObject.FindObjectsOfType<GroundGraphBuilder>())
                            ggb.Build();
                    }
                    if (GUILayout.Button(new GUIContent("Build Air Graph")))
                    {
                        foreach (var agb in GameObject.FindObjectsOfType<AirGraphBuilder>())
                            agb.Build();
                    }
                    if (DebugSettings.ShowSettings = GUILayout.Toggle(DebugSettings.ShowSettings, "Debug Settings"))
                        using (var scroll = new GUILayout.ScrollViewScope(scrollPosition))
                        {
                            OnDebugGUI();
                            scrollPosition = scroll.scrollPosition;
                        }

                }
                finally
                {
                    if (EditorGUI.EndChangeCheck())
                    {
                        SaveDebugValues();
                    }
                    GUILayout.EndArea();
                    Handles.EndGUI();
                }
            }
        }
        private static void OnBuilt()
        {
            regenerateLineMesh = true;
            repaint = true;
        }
        private static void Draw(Camera camera)
        {
            if (DebugSettings.DebugTeleporterOk && teleporterOkMesh) Graphics.DrawMesh(teleporterOkMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugNoCeiling && noCeilingMesh) Graphics.DrawMesh(noCeilingMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugGroundNodes && groundNodeMesh) Graphics.DrawMesh(groundNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugAirNodes && airNodeMesh) Graphics.DrawMesh(airNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugAirLinks)
            {
                if (airLinkLineMesh)
                    Graphics.DrawMesh(airLinkLineMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, camera, 0);
                if (airLinkArrowMesh)
                    Graphics.DrawMesh(airLinkArrowMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, camera, 0);
            }
            if (DebugSettings.DebugGroundLinks)
            {
                var position = Vector3.up * DebugSettings.VerticalOffset;
                if (groundLinkLineMesh)
                    Graphics.DrawMesh(groundLinkLineMesh, position, Quaternion.identity, linkMaterial, 0, camera, 0);
                if (groundLinkArrowMesh)
                    Graphics.DrawMesh(groundLinkArrowMesh, position, Quaternion.identity, linkMaterial, 0, camera, 0);
            }
            var probes = Selection.GetFiltered<NavigationProbe>(SelectionMode.Deep);
            var groundGraphBuilders = Selection.GetFiltered<GroundGraphBuilder>(SelectionMode.Deep);
            if (probes.Any() || groundGraphBuilders.Any())
            {

                if (DebugSettings.ProbeLineOfSightOverlay)
                    foreach (var target in GameObject.FindObjectsOfType<GroundGraphBuilder>())
                        Graphics.DrawMesh(target.mesh, Vector3.up * 0.1f, Quaternion.identity, triangleMaterial, 0, camera, 0);

                foreach (var probe in GameObject.FindObjectsOfType<NavigationProbe>())
                {
                    var color = new Color(probe.navigationProbeColor.r, probe.navigationProbeColor.g, probe.navigationProbeColor.b, 1);
                    if (!probeMeshes.ContainsKey(probe) || probeColors[probe] != probe.navigationProbeColor)
                    {
                        probeColors[probe] = probe.navigationProbeColor;
                        probeMeshes[probe] = GameObject.Instantiate(cube);
                        probeMeshes[probe].SetIndices(cube.triangles, MeshTopology.Triangles, 0);
                        probeMeshes[probe].colors = new Color[] {
                            Color.Lerp(color, Color.black,0.25f),
                            Color.Lerp(color, Color.black,0.25f),
                            Color.Lerp(color, Color.black,0.25f),
                            Color.Lerp(color, Color.black,0.25f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,.25f),
                            Color.Lerp(color, Color.black,.25f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,.25f),
                            Color.Lerp(color, Color.black,.25f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,.5f),
                            Color.Lerp(color, Color.black,0),
                            Color.Lerp(color, Color.black,0),
                            Color.Lerp(color, Color.black,0),
                            Color.Lerp(color, Color.black,0),
                            Color.Lerp(color, Color.black,0),
                            Color.Lerp(color, Color.black,0),
                            Color.Lerp(color, Color.black,0),
                            Color.Lerp(color, Color.black,0),
                        };
                    }
                    var matrix = Matrix4x4.TRS(probe.transform.position, Quaternion.identity, Vector3.one * 2);
                    Graphics.DrawMesh(probeMeshes[probe], matrix, triangleMaterial, 0, camera);
                }
            }
        }
        private static void UpdateMeshCheck()
        {
            if (repaint)
            {
                var sceneInfo = GameObject.FindObjectOfType<SceneInfo>();
                if (!sceneInfo) return;

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

                SceneView.lastActiveSceneView?.Repaint();
                repaint = false;
            }
        }
        public static void OnDebugGUI()
        {
            regenerateLineMesh |= CheckedField(() => DebugSettings.HullMask = (int)(HullMask)EditorGUILayout.EnumFlagsField(hullMaskContent, (HullMask)DebugSettings.HullMask));


            repaint |= CheckedField(() => DebugSettings.DebugNoCeiling = EditorGUILayout.Toggle(debugNoCeilingContent, DebugSettings.DebugNoCeiling));
            if (DebugSettings.DebugNoCeiling)
                regenerateNodeMesh |= CheckedField(() => DebugSettings.NoCeilingColor = EditorGUILayout.ColorField(DebugSettings.NoCeilingColor), noCeilingColorContent);

            repaint |= CheckedField(() => DebugSettings.DebugTeleporterOk = EditorGUILayout.Toggle(debugTeleporterOkContent, DebugSettings.DebugTeleporterOk));
            if (DebugSettings.DebugTeleporterOk)
                regenerateNodeMesh |= CheckedField(() => DebugSettings.TeleporterOkColor = EditorGUILayout.ColorField(DebugSettings.TeleporterOkColor), teleporterOkColorContent);

            regenerateNodeMesh |= CheckedField(() => DebugSettings.DebugGroundNodes = EditorGUILayout.Toggle(debugGroundNodesContent, DebugSettings.DebugGroundNodes));
            regenerateNodeMesh |= CheckedField(() => DebugSettings.DebugGroundLinks = EditorGUILayout.Toggle(debugGroundLinksContent, DebugSettings.DebugGroundLinks));


            regenerateNodeMesh |= CheckedField(() => DebugSettings.DebugAirNodes = EditorGUILayout.Toggle(debugAirNodesContent, DebugSettings.DebugAirNodes));
            regenerateLineMesh |= CheckedField(() => DebugSettings.DebugAirLinks = EditorGUILayout.Toggle(debugAirLinksContent, DebugSettings.DebugAirLinks));
            if (DebugSettings.DebugGroundLinks)
            {
                repaint |= CheckedField(() => DebugSettings.VerticalOffset = EditorGUILayout.Slider(verticalOffsetContent, DebugSettings.VerticalOffset, 0, 20));
            }
            if (DebugSettings.DebugGroundLinks || DebugSettings.DebugAirLinks)
            {
                regenerateArrowMesh |= CheckedField(() => DebugSettings.ArrowSize = EditorGUILayout.Slider(arrowSizeContent, DebugSettings.ArrowSize, 0.5f, 10));
                regenerateArrowMesh |= CheckedField(() => DebugSettings.ArrowOffset = EditorGUILayout.Slider(arrowOffsetContent, DebugSettings.ArrowOffset, 0, DebugSettings.PercentageOffset ? 1 : 20));
                regenerateArrowMesh |= CheckedField(() => DebugSettings.PercentageOffset = EditorGUILayout.Toggle(percentageOffsetContent, DebugSettings.PercentageOffset));
            }
            if (DebugSettings.DebugGroundLinks || DebugSettings.DebugGroundNodes || DebugSettings.DebugAirLinks || DebugSettings.DebugAirNodes)
            {
                bool updateColors = false;
                updateColors |= CheckedField(() => DebugSettings.HumanColor = EditorGUILayout.ColorField(humanColorContent, DebugSettings.HumanColor));
                updateColors |= CheckedField(() => DebugSettings.GolemColor = EditorGUILayout.ColorField(golemColorContent, DebugSettings.GolemColor));
                updateColors |= CheckedField(() => DebugSettings.QueenColor = EditorGUILayout.ColorField(queenColorContent, DebugSettings.QueenColor));
                regenerateArrowMesh |= updateColors;
                regenerateNodeMesh |= updateColors;
            }

            repaint |= CheckedField(() => DebugSettings.ProbeLineOfSightOverlay = EditorGUILayout.Toggle(probeLineOfSightOverlayContent, DebugSettings.ProbeLineOfSightOverlay));

            repaint |= regenerateLineMesh || regenerateNodeMesh || regenerateArrowMesh;
        }
        private static void SaveDebugValues()
        {
            string key = GUIDName(RainOfStagesSceneInfoEditorSettings);
            EditorPrefs.SetString(key, JsonUtility.ToJson(DebugSettings));
        }
        private static void LoadDebugValues()
        {
            string key = GUIDName(RainOfStagesSceneInfoEditorSettings);
            if (EditorPrefs.HasKey(key))
            {
                string json = EditorPrefs.GetString(key);
                DebugSettings = JsonUtility.FromJson<DebugSettings>(json);
            }
        }
        private static void RegenerateMeshes()
        {
            if (colormap == null)
                colormap = new Dictionary<HullMask, Color> {
                    { HullMask.Human, DebugSettings.HumanColor },
                    { HullMask.Golem, DebugSettings.GolemColor },
                    { HullMask.BeetleQueen, DebugSettings.QueenColor },
                };
            else
            {
                colormap[HullMask.Human] = DebugSettings.HumanColor;
                colormap[HullMask.Golem] = DebugSettings.GolemColor;
                colormap[HullMask.BeetleQueen] = DebugSettings.QueenColor;
            }

            var nodeCorrection = (Vector3.left * 0.5f) + (Vector3.back * 0.5f);
            if (groundNodeGraph)
            {
                if (regenerateLineMesh)
                    groundLinkLineMesh = groundNodeGraph.GenerateLinkDebugMesh((HullMask)DebugSettings.HullMask);
                //GenerateLinkMesh(LinkMeshType.line, groundNodes, groundLinks, VerticalOffset);
                if (regenerateArrowMesh)
                    groundLinkArrowMesh = GenerateLinkMesh(LinkMeshType.arrow, groundNodes, groundLinks);
                if (regenerateNodeMesh)
                {
                    groundNodeMesh = GenerateNodeMesh(groundNodes, nodeCorrection);
                    teleporterOkMesh = GenerateNodeExtraMesh(groundNodes, Vector3.right, DebugSettings.TeleporterOkColor, node => node.flags.HasFlag(NodeFlags.TeleporterOK));
                    noCeilingMesh = GenerateNodeExtraMesh(groundNodes, Vector3.left, DebugSettings.NoCeilingColor, node => node.flags.HasFlag(NodeFlags.NoCeiling));
                }
            }

            if (airNodeGraph)
            {
                if (regenerateLineMesh)
                    airLinkLineMesh = airNodeGraph.GenerateLinkDebugMesh((HullMask)DebugSettings.HullMask);
                if (regenerateArrowMesh)
                    airLinkArrowMesh = GenerateLinkMesh(LinkMeshType.arrow, airNodes, airLinks);
                if (regenerateNodeMesh)
                    airNodeMesh = GenerateNodeMesh(airNodes, nodeCorrection);
            }

            regenerateNodeMesh = false;
            regenerateLineMesh = false;
            regenerateArrowMesh = false;
        }
        static bool CheckedField(Action drawField, GUIContent label = null)
        {
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (label != null)
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
        private static Mesh GenerateNodeMesh(NodeGraph.Node[] nodes, Vector3 offset)
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
                                    cubeVertices[cubeTriangles[i + 0]] + position + offset,
                                    cubeVertices[cubeTriangles[i + 1]] + position + offset,
                                    cubeVertices[cubeTriangles[i + 2]] + position + offset,
                                    colormap[mask]);

                            position += Vector3.up;
                        }
                }
                catch { }
            }

            return GetMesh(vertices, indices, colors, MeshTopology.Triangles);
        }
        private static Mesh GenerateLinkMesh(LinkMeshType linkMeshType, NodeGraph.Node[] nodes, NodeGraph.Link[] links)
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

            foreach (var node in nodes)
            {
                var linkIndex = node.linkListIndex;
                for (int i = linkIndex.index; i < linkIndex.index + linkIndex.size; i++)
                {
                    var link = links[i];

                    var mask = ((HullMask)link.hullMask).GetMaxSetFlagValue();
                    var color = colormap[(HullMask)mask];
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
                            AddLine(new Vector3(nodeAPos.x, nodeAPos.y - 5, nodeAPos.z),
                                    new Vector3(nodeAPos.x, nodeAPos.y, nodeAPos.z), color);

                            AddLine(new Vector3(nodeAPos.x, nodeAPos.y, nodeAPos.z),
                                    new Vector3(nodeBPos.x, nodeBPos.y, nodeBPos.z), color);
                            break;
                        case LinkMeshType.arrow:
                            float halfArrowWidth = DebugSettings.ArrowSize / 2;

                            var offset = DebugSettings.PercentageOffset ? Mathf.Clamp(DebugSettings.ArrowOffset, 0, 1) * displacement.magnitude : DebugSettings.ArrowOffset;
                            nodeAModA = nodeAPos + (linkDirection * offset);
                            nodeAModB = nodeAPos + (linkDirection * (offset + DebugSettings.ArrowSize));

                            var linkCross = Vector3.Cross(linkDirection, Vector3.up).normalized;

                            var a = nodeAModA + (linkCross * halfArrowWidth);
                            var b = nodeAModA - (linkCross * halfArrowWidth);

                            AddTriangle(new Vector3(a.x, a.y, a.z),
                                        new Vector3(b.x, b.y, b.z),
                                        new Vector3(nodeAModB.x, nodeAModB.y, nodeAModB.z), color);
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
        private static float CalculateHeight()
        {
            if (!DebugSettings.ShowGraphTools) return 23f;
            var result = 84f;
            var debugType = typeof(DebugSettings);
            var fields = debugType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var toggleFields = fields.Where(fi => fi.FieldType == typeof(bool)).ToArray();
            if (DebugSettings.ShowSettings)
                result += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + 1) * toggleFields.Length;
            var trueToggles = toggleFields.Where(fi => (bool)fi.GetValue(DebugSettings)).ToArray();

            return result;
        }
        private static float CalculateWidth()
        {
            if (!DebugSettings.ShowGraphTools) return 24f;

            var result = 160f;
            if (DebugSettings.ShowSettings)
                result += 140f;

            return result;
        }
    }
}
