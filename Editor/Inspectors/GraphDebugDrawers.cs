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
        private static readonly Vector3[] maskOffsets = new[] { Vector3.right, Vector3.left, Vector3.zero };

        public static DebugSettings DebugSettings = new DebugSettings
        {
            VerticalOffset = 1f,
            HumanColor = Color.green,
            GolemColor = Color.blue,
            QueenColor = Color.red,
            NoCeilingColor = Color.cyan,
            TeleporterOkColor = Color.yellow,
            NoCharacterSpawnColor = Color.white,
            NoChestSpawnColor = Color.magenta,
            NoShrineSpawnColor = Color.black,
            HullMask = (int)HullMask.Human
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
        private static Mesh noCharacterSpawnMesh;
        private static Mesh noChestSpawnMesh;
        private static Mesh noShrineSpawnMesh;
        private static bool regenerateLineMesh, regenerateArrowMesh, regenerateNodeMesh;
        private static bool repaint;
        private static GUIContent
            hullMaskContent,
            debugGraphContent,
            debugFlagsContent,
            debugNodesContent,
            debugLinksContent,
            verticalOffsetContent,
            humanColorContent,
            golemColorContent,
            queenColorContent,
            probeLineOfSightOverlayContent,
            noCeilingColorContent,
            teleporterOkColorContent,
            noCharacterSpawnColorContent,
            noChestSpawnColorContent,
            noShrineSpawnColorContent
            ;
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
            debugGraphContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugGraph)));
            debugFlagsContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugFlags)));
            debugNodesContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugNodes)));
            debugLinksContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.DebugLinks)));
            verticalOffsetContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.VerticalOffset)));
            humanColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.HumanColor)));
            golemColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.GolemColor)));
            queenColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.QueenColor)));
            probeLineOfSightOverlayContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.ProbeLineOfSightOverlay)));
            noCeilingColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.NoCeilingColor)));
            teleporterOkColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.TeleporterOkColor)));
            noCharacterSpawnColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.NoCharacterSpawnColor)));
            noChestSpawnColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.NoChestSpawnColor)));
            noShrineSpawnColorContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(DebugSettings.NoShrineSpawnColor)));
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
                    float slh = EditorGUIUtility.singleLineHeight;
                    var buttonWidth = GUILayout.Width(slh);
                    var buttonHeight = GUILayout.Height(slh);
                    if (DebugSettings.ShowGraphTools && GUILayout.Button(new GUIContent("X", "Close NodeGraph Tools"), buttonWidth, buttonHeight))
                        DebugSettings.ShowGraphTools = false;
                    if (!DebugSettings.ShowGraphTools && GUILayout.Button(new GUIContent(">", "Open NodeGraph Tools"), buttonWidth, buttonHeight))
                        DebugSettings.ShowGraphTools = true;

                    if (!DebugSettings.ShowGraphTools) return;

                    GUI.Label(new Rect(24, 3, width - 36, slh), "NodeGraph Tools");

                    if (!DebugSettings.ShowSettings && GUI.Button(new Rect(width - slh - 6, 3, slh + 2, slh), ">"))
                        DebugSettings.ShowSettings = true;
                    if (DebugSettings.ShowSettings && GUI.Button(new Rect(width - slh - 6, 3, slh + 2, slh), "<"))
                        DebugSettings.ShowSettings = false;

                    if (DebugSettings.ShowSettings) GUILayout.BeginHorizontal();

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
                    if (DebugSettings.ShowSettings) GUILayout.EndHorizontal();

                    if (DebugSettings.ShowSettings)
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
            if (DebugSettings.DebugFlags.HasFlag(NodeFlags.TeleporterOK) && teleporterOkMesh) Graphics.DrawMesh(teleporterOkMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugFlags.HasFlag(NodeFlags.NoCeiling) && noCeilingMesh) Graphics.DrawMesh(noCeilingMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugFlags.HasFlag(NodeFlags.NoCharacterSpawn) && noCharacterSpawnMesh) Graphics.DrawMesh(noCharacterSpawnMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugFlags.HasFlag(NodeFlags.NoChestSpawn) && noChestSpawnMesh) Graphics.DrawMesh(noChestSpawnMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
            if (DebugSettings.DebugFlags.HasFlag(NodeFlags.NoShrineSpawn) && noShrineSpawnMesh) Graphics.DrawMesh(noShrineSpawnMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);

            switch (DebugSettings.DebugGraph)
            {
                case DebugSettings.Graph.Ground:
                    if (DebugSettings.DebugNodes && groundNodeMesh)
                        Graphics.DrawMesh(groundNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);
                    if (DebugSettings.DebugLinks)
                    {
                        var position = Vector3.up * DebugSettings.VerticalOffset;
                        if (groundLinkLineMesh)
                            Graphics.DrawMesh(groundLinkLineMesh, position, Quaternion.identity, linkMaterial, 0, camera, 0);
                    }
                    break;
                case DebugSettings.Graph.Air:
                    if (DebugSettings.DebugNodes)
                    {
                        if (airNodeMesh) Graphics.DrawMesh(airNodeMesh, Vector3.zero, Quaternion.identity, nodeMaterial, 0, camera, 0);

                    }
                    if (DebugSettings.DebugLinks && airLinkLineMesh)
                        Graphics.DrawMesh(airLinkLineMesh, Vector3.zero, Quaternion.identity, linkMaterial, 0, camera, 0);
                    break;
            }

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
            EditorGUIUtility.labelWidth = 170f;
            regenerateLineMesh |= CheckedField(() => DebugSettings.HullMask = (int)(HullMask)EditorGUILayout.EnumPopup(hullMaskContent, (HullMask)DebugSettings.HullMask));
            regenerateNodeMesh |= regenerateLineMesh |= CheckedField(() => DebugSettings.DebugGraph = (DebugSettings.Graph)EditorGUILayout.EnumPopup(debugGraphContent, DebugSettings.DebugGraph));
            regenerateNodeMesh |= repaint |= CheckedField(() => DebugSettings.DebugFlags = (NodeFlags)EditorGUILayout.EnumFlagsField(debugFlagsContent, DebugSettings.DebugFlags));

            regenerateNodeMesh |= CheckedField(() => DebugSettings.DebugNodes = EditorGUILayout.Toggle(debugNodesContent, DebugSettings.DebugNodes));
            regenerateLineMesh|= CheckedField(() => DebugSettings.DebugLinks = EditorGUILayout.Toggle(debugLinksContent, DebugSettings.DebugLinks));
            repaint |= CheckedField(() => DebugSettings.ProbeLineOfSightOverlay = EditorGUILayout.Toggle(probeLineOfSightOverlayContent, DebugSettings.ProbeLineOfSightOverlay));

            repaint |= CheckedField(() => DebugSettings.VerticalOffset = EditorGUILayout.Slider(verticalOffsetContent, DebugSettings.VerticalOffset, 0, 20));

            bool updateColors = false;
            updateColors |= CheckedField(() => DebugSettings.NoCeilingColor = EditorGUILayout.ColorField(noCeilingColorContent, DebugSettings.NoCeilingColor));
            updateColors |= CheckedField(() => DebugSettings.TeleporterOkColor = EditorGUILayout.ColorField(teleporterOkColorContent, DebugSettings.TeleporterOkColor));
            updateColors |= CheckedField(() => DebugSettings.NoCharacterSpawnColor = EditorGUILayout.ColorField(noCharacterSpawnColorContent, DebugSettings.NoCharacterSpawnColor));
            updateColors |= CheckedField(() => DebugSettings.NoChestSpawnColor = EditorGUILayout.ColorField(noChestSpawnColorContent, DebugSettings.NoChestSpawnColor));
            updateColors |= CheckedField(() => DebugSettings.NoShrineSpawnColor = EditorGUILayout.ColorField(noShrineSpawnColorContent, DebugSettings.NoShrineSpawnColor));
            updateColors |= CheckedField(() => DebugSettings.HumanColor = EditorGUILayout.ColorField(humanColorContent, DebugSettings.HumanColor));
            updateColors |= CheckedField(() => DebugSettings.GolemColor = EditorGUILayout.ColorField(golemColorContent, DebugSettings.GolemColor));
            updateColors |= CheckedField(() => DebugSettings.QueenColor = EditorGUILayout.ColorField(queenColorContent, DebugSettings.QueenColor));
            regenerateArrowMesh |= updateColors;
            regenerateNodeMesh |= updateColors;

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
                    teleporterOkMesh = GenerateNodeExtraMesh(groundNodes, Vector3.back + Vector3.right + nodeCorrection, DebugSettings.TeleporterOkColor, node => node.flags.HasFlag(NodeFlags.TeleporterOK));
                    noCeilingMesh = GenerateNodeExtraMesh(groundNodes, Vector3.back + Vector3.left + nodeCorrection, DebugSettings.NoCeilingColor, node => node.flags.HasFlag(NodeFlags.NoCeiling));
                    noCharacterSpawnMesh = GenerateNodeExtraMesh(groundNodes, Vector3.forward + nodeCorrection, DebugSettings.NoCharacterSpawnColor, node => node.flags.HasFlag(NodeFlags.NoCharacterSpawn));
                    noChestSpawnMesh = GenerateNodeExtraMesh(groundNodes, Vector3.forward + Vector3.right + nodeCorrection, DebugSettings.NoChestSpawnColor, node => node.flags.HasFlag(NodeFlags.NoChestSpawn));
                    noShrineSpawnMesh = GenerateNodeExtraMesh(groundNodes, Vector3.forward + Vector3.left + nodeCorrection, DebugSettings.NoShrineSpawnColor, node => node.flags.HasFlag(NodeFlags.NoShrineSpawn));
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
        static bool CheckedField(Action drawField)
        {
            EditorGUI.BeginChangeCheck();
            drawField();
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
                    for (int i = 0; i < masks.Length; i++)
                    {
                        var mask = masks[i];
                        if (!node.forbiddenHulls.HasFlag(mask))
                        {
                            for (int j = 0; j < cubeTriangles.Length; j += 3)
                                AddTriangle(
                                    cubeVertices[cubeTriangles[j + 0]] + position + offset + maskOffsets[i],
                                    cubeVertices[cubeTriangles[j + 1]] + position + offset + maskOffsets[i],
                                    cubeVertices[cubeTriangles[j + 2]] + position + offset + maskOffsets[i],
                                    colormap[mask]);
                        }
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
            var result = 64f;
            if (DebugSettings.ShowSettings)
                result += EditorGUIUtility.singleLineHeight * (10);

            return result;
        }
        private static float CalculateWidth()
        {
            if (!DebugSettings.ShowGraphTools) return 24f;

            var result = 160f;
            if (DebugSettings.ShowSettings)
                result += 150f;

            return result;
        }
    }
}