using PassivePicasso.RainOfStages.Plugin.Navigation;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(GraphBuilder), true)]
    public class GraphBuilderEditor : Editor
    {
        protected GraphBuilder builder;
        private static GUIContent overlayContent ;

        protected virtual IEnumerable<string> ExcludedProperties()
        {
            yield break;
        }

        string[] excludedProperties;
        private void OnEnable()
        {
            excludedProperties = ExcludedProperties().Prepend("m_Script").Prepend("nodeGraph").Distinct().ToArray();
            overlayContent = new GUIContent(ObjectNames.NicifyVariableName(nameof(GraphDebugDrawers.DebugSettings.ShowGraphTools)));
        }
        public override void OnInspectorGUI()
        {
            builder = target as GraphBuilder;
            if (!builder) return;

            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, excludedProperties);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            if (builder.nodeGraph)
                if (!EditorUtility.IsPersistent(builder.nodeGraph))
                {
                    var scene = builder.gameObject.scene;
                    var scenePath = scene.path;
                    scenePath = System.IO.Path.GetDirectoryName(scenePath);
                    if (!AssetDatabase.IsValidFolder(System.IO.Path.Combine(scenePath, scene.name)))
                        AssetDatabase.CreateFolder(scenePath, scene.name);

                    var nodeGraphPath = System.IO.Path.Combine(scenePath, scene.name, builder.nodeGraph.name);

                    AssetDatabase.CreateAsset(builder.nodeGraph, nodeGraphPath);
                    AssetDatabase.Refresh();
                }
                else
                {
                    EditorUtility.SetDirty(builder.nodeGraph);
                    var so = new SerializedObject(builder.nodeGraph);
                    so.ApplyModifiedProperties();
                }
        }
    }
}