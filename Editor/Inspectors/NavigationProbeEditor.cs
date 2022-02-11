using PassivePicasso.RainOfStages.Plugin.Navigation;
using PassivePicasso.RainOfStages.Plugin.Utility;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(NavigationProbe), true)]
    [CanEditMultipleObjects]
    public class NavigationProbeEditor : Editor
    {
        private static Material previewMaterial;

        [InitializeOnLoadMethod]
        static void InitializeDebugDrawer()
        {
            Camera.onPreCull -= Draw;
            Camera.onPreCull += Draw;
        }

        private static void Draw(Camera cam)
        {
            if (!previewMaterial)
                previewMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(PathHelper.RoSPath("RoSShared", "Materials", "vertexcolor.mat"));
            if (!UnityEditor.SceneView.currentDrawingSceneView?.camera)
                return;
            var targets = Selection.GetFiltered<NavigationProbe>(SelectionMode.Deep);

            foreach (var target in targets.OfType<NavigationProbe>())
            {
                foreach (var mesh in target.meshes)
                {
                    Graphics.DrawMesh(mesh, Vector3.up * 0.1f, Quaternion.identity, previewMaterial, 0, cam, 0);
                }
            }
        }
    }
}
