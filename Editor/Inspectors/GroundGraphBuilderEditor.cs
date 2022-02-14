using PassivePicasso.RainOfStages.Plugin.Navigation;
using PassivePicasso.RainOfStages.Plugin.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(GroundGraphBuilder), true)]
    public class GroundGraphBuilderEditor : GraphBuilderEditor
    {
        private static Material triangleMaterial;
        private static Mesh cube;
        private static Dictionary<NavigationProbe, Mesh> probeMeshes = new Dictionary<NavigationProbe, Mesh>();

        [InitializeOnLoadMethod]
        static void InitializeDebugDrawer()
        {
            Camera.onPreCull -= Draw;
            Camera.onPreCull += Draw;
            var cubePrim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMf = cubePrim.GetComponent<MeshFilter>();

            cube = cubeMf.sharedMesh;

            DestroyImmediate(cubePrim);
        }


        private static void Draw(Camera cam)
        {
            if (!triangleMaterial)
                triangleMaterial = AssetDatabase.LoadAssetAtPath<Material>(PathHelper.RoSPath("RoSShared", "Materials", "VertexColor.mat"));
            if (!SceneView.currentDrawingSceneView?.camera)
                return;
            var probes = Selection.GetFiltered<NavigationProbe>(SelectionMode.Deep);
            var groundGraphBuilders = Selection.GetFiltered<GroundGraphBuilder>(SelectionMode.Deep);
            if (probes.Any() || groundGraphBuilders.Any())
            {

                foreach (var target in FindObjectsOfType<GroundGraphBuilder>())
                    Graphics.DrawMesh(target.mesh, Vector3.up * 0.1f, Quaternion.identity, triangleMaterial, 0, cam, 0);

                foreach (var probe in FindObjectsOfType<NavigationProbe>())
                {
                    var color = new Color(probe.navigationProbeColor.r, probe.navigationProbeColor.g, probe.navigationProbeColor.b, 1);
                    if (!probeMeshes.ContainsKey(probe))
                    {
                        probeMeshes[probe] = Instantiate(cube);
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
                    Graphics.DrawMesh(probeMeshes[probe], matrix, triangleMaterial, 0, cam);
                }
            }
        }
    }
}