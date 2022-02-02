using PassivePicasso.RainOfStages.Plugin.Navigation;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(GraphBuilder), true)]
    public class GraphBuilderEditor : Editor
    {
        protected GraphBuilder builder;
        public override void OnInspectorGUI()
        {
            builder = target as GraphBuilder;
            if (!builder) return;

            if (GUILayout.Button("Build"))
            {
                try
                {
                    builder.Build();
                }
                finally
                {
                    builder.rebuild = false;
                }
            }

            if (GUILayout.Button("Rebuild"))
            {
                builder.rebuild = true;
            }

            if (GetType() == typeof(GraphBuilderEditor))
            {
                EditorGUI.BeginChangeCheck();
                DrawPropertiesExcluding(serializedObject, "m_Script");
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }
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