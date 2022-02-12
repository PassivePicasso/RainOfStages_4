using PassivePicasso.RainOfStages.Plugin.Navigation;
using PassivePicasso.RainOfStages.Plugin.Utility;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(GroundGraphBuilder), true)]
    public class GroundGraphBuilderEditor : GraphBuilderEditor
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
                previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(PathHelper.RoSPath("RoSShared", "Materials", "TransparentRed.mat"));
            if (!SceneView.currentDrawingSceneView?.camera)
                return;
            var probes = Selection.GetFiltered<NavigationProbe>(SelectionMode.Deep);
            var groundGraphBuilders = Selection.GetFiltered<GroundGraphBuilder>(SelectionMode.Deep);
            if (probes.Any() || groundGraphBuilders.Any())
                foreach (var target in FindObjectsOfType<GroundGraphBuilder>())
                {
                    Graphics.DrawMesh(target.mesh, Vector3.up * 0.1f, Quaternion.identity, previewMaterial, 0, cam, 0);
                }
        }
    }
}