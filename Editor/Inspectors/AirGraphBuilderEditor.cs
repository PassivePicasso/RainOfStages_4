using PassivePicasso.RainOfStages.Plugin.Navigation;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(AirGraphBuilder), false)]
    public class AirGraphBuilderEditor : GraphBuilderEditor
    {
        bool ShowProbes = false;
        Dictionary<AirGraphBuilder, bool[]> probeStates = new Dictionary<AirGraphBuilder, bool[]>();

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var airBuilder = this.builder as AirGraphBuilder;
            if (!airBuilder) return;
            if (!probeStates.ContainsKey(airBuilder))
                probeStates[airBuilder] = new bool[airBuilder.Probes.Count];

            if (GUILayout.Button("Add Probe"))
            {
                var name = ObjectNames.GetUniqueName(airBuilder.Probes.Select(p => p.name).ToArray(), "Probe (1)");
                var probe = Instantiate(airBuilder.Probes.Last());
                probe.name = name;
                probe.transform.parent = airBuilder.transform;
                if (airBuilder.Probes == null) airBuilder.Probes = new List<NavigationProbe>();
                airBuilder.Probes.Add(probe.GetComponent<NavigationProbe>());
                var newStates = new bool[airBuilder.Probes.Count];
                probeStates[airBuilder].CopyTo(newStates, 0);
                probeStates[airBuilder] = newStates;
            }
            ShowProbes = EditorGUILayout.Foldout(ShowProbes, new GUIContent("Probes"));
            EditorGUI.indentLevel++;
            if (ShowProbes)
                for (int i = 0; i < airBuilder.Probes.Count; i++)
                {
                    var probe = airBuilder.Probes[i];
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
                        airBuilder.Probes.RemoveAt(i);
                        DestroyImmediate(probe.gameObject);
                        airBuilder.LinkGlobalNodes();
                        break;
                    }
                    probeStates[airBuilder][i] = EditorGUILayout.Foldout(probeStates[airBuilder][i], new GUIContent(probe.name));
                    if (probeStates[airBuilder][i])
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