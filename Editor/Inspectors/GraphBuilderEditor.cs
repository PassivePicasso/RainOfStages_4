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
        }

        void OnSceneGUI()
        {

        }
    }
}