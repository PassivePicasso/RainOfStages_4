using PassivePicasso.RainOfStages.Plugin.Navigation;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Editor.Inspectors
{
    [CustomEditor(typeof(AirGraphBuilder), true)]
    public class AirGraphBuilderEditor : UnityEditor.Editor
    {
        bool ShowProbes = false;
        Dictionary<AirGraphBuilder, bool[]> probeStates = new Dictionary<AirGraphBuilder, bool[]>();

        public override void OnInspectorGUI()
        {
            var builder = target as AirGraphBuilder;
            if (!builder) return;
            if (!probeStates.ContainsKey(builder))
                probeStates[builder] = new bool[builder.Probes.Count];

            if (GUILayout.Button("Add Probe"))
            {
                var probe = new GameObject($"Probe_{builder.Probes.Count}", typeof(AirNavigationProbe));
                probe.transform.parent = builder.transform;
                if (builder.Probes == null) builder.Probes = new List<AirNavigationProbe>();
                builder.Probes.Add(probe.GetComponent<AirNavigationProbe>());
                var newStates = new bool[builder.Probes.Count];
                probeStates[builder].CopyTo(newStates, 0);
                probeStates[builder] = newStates;
            }
            ShowProbes = EditorGUILayout.Foldout(ShowProbes, new GUIContent("Probes"));
            EditorGUI.indentLevel++;
            if (ShowProbes)
                for (int i = 0; i < builder.Probes.Count; i++)
                {
                    var probe = builder.Probes[i];
                    var probeSo = new SerializedObject(probe);
                    var probeGoSo = new SerializedObject(probe.gameObject);
                    var probeTfSo = new SerializedObject(probe.transform);
                    var rect = EditorGUILayout.GetControlRect(false, 0);
                    var right = rect.x + rect.width - 4;
                    var probeTop = rect.y;
                    rect.y += 2;
                    rect.x = right - EditorGUIUtility.singleLineHeight;
                    rect.width = EditorGUIUtility.singleLineHeight + 2;
                    rect.height = EditorGUIUtility.singleLineHeight - 2;
                    if (GUI.Button(rect, "x", EditorStyles.miniButton))
                    {
                        builder.Probes.RemoveAt(i);
                        DestroyImmediate(probe.gameObject);
                        builder.LinkGlobalNodes();
                        break;
                    }
                    probeStates[builder][i] = EditorGUILayout.Foldout(probeStates[builder][i], new GUIContent(probe.name));
                    if (probeStates[builder][i])
                    {
                        EditorGUI.BeginChangeCheck();
                        SerializedProperty property = probeGoSo.FindProperty("m_Name");
                        EditorGUILayout.PropertyField(property);
                        if (EditorGUI.EndChangeCheck())
                        {
                            probeGoSo.SetIsDifferentCacheDirty();
                            probeGoSo.ApplyModifiedProperties();
                        }
                        EditorGUI.BeginChangeCheck();
                        CreateEditor(probe.transform).DrawDefaultInspector();
                        if (EditorGUI.EndChangeCheck())
                        {
                            probeTfSo.SetIsDifferentCacheDirty();
                            probeTfSo.ApplyModifiedProperties();
                        }
                        EditorGUI.BeginChangeCheck();
                        DrawPropertiesExcluding(probeSo, "m_Script");
                        if (EditorGUI.EndChangeCheck())
                        {
                            probeSo.SetIsDifferentCacheDirty();
                            probeSo.ApplyModifiedProperties();
                        }
                    }

                    var box = EditorGUILayout.GetControlRect(false, 0);
                    var trueBox = new Rect(box.x + 2, rect.y, 15, (box.y - probeTop) - 4);
                    var customBoxStyle = new GUIStyle(EditorStyles.helpBox);
                    customBoxStyle.normal.background = null;
                    GUI.Box(trueBox, string.Empty, EditorStyles.helpBox);
                }
            Tools.hidden = ShowProbes;
        }
        private void OnDisable()
        {
            Tools.hidden = false;
        }
        private void OnSceneGUI()
        {
            var builder = target as AirGraphBuilder;
            if (!builder) return;
            if (!probeStates.ContainsKey(builder))
                probeStates[builder] = new bool[builder.Probes.Count];

            if (ShowProbes)
                for (int i = 0; i < builder.Probes.Count; i++)
                {
                    var probe = builder.Probes[i];
                    var probeTransform = probe.transform;
                    var probeTfSo = new SerializedObject(probeTransform);
                    EditorGUI.BeginChangeCheck();
                    probeTransform.position = Handles.DoPositionHandle(probeTransform.position, probeTransform.rotation);
                    probeTransform.rotation = Handles.DoRotationHandle(probeTransform.rotation, probeTransform.position);
                    probeTransform.localScale = Handles.DoScaleHandle(probeTransform.localScale, probeTransform.position, probeTransform.rotation, 1);
                    if (EditorGUI.EndChangeCheck())
                    {
                        probeTfSo.SetIsDifferentCacheDirty();
                        probeTfSo.ApplyModifiedProperties();
                    }
                }
        }
    }
}