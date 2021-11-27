using RoR2;
using RoR2.Navigation;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    public class StaticNode : MonoBehaviour
    {
        [EnumMask(typeof(HullMask))]
        public HullMask forbiddenHulls;
        [EnumMask(typeof(NodeFlags))]
        public NodeFlags nodeFlags;
        public Vector3 position;
        public StaticNode[] HardLinks;
        public bool overrideDistanceScore;
        public float distanceScore;

        private Material gizmoMaterial;

        private void OnRenderObject()
        {
            if (!Selection.gameObjects.Contains(gameObject)) return;
            if (!gizmoMaterial) gizmoMaterial = new Material(Shader.Find("VR/SpatialMapping/Wireframe"));
            gizmoMaterial.SetPass(0);
            GL.PushMatrix();
            GL.MultMatrix(transform.localToWorldMatrix);
            GL.Begin(GL.TRIANGLES);
            try
            {
                //GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //GL.Color(Color.green);
                //for (int i = 0; i < cubeTriangles.Length; i += 3)
                //{
                //    var a = cubeVertices[cubeTriangles[i + 0]] * 2;
                //    var b = cubeVertices[cubeTriangles[i + 1]] * 2;
                //    var c = cubeVertices[cubeTriangles[i + 2]] * 2;
                //    GL.Vertex3(a.x, a.y, a.z);
                //    GL.Vertex3(b.x, b.y, b.z);
                //    GL.Vertex3(c.x, c.y, c.z);
                //}
            }
            catch { }
            GL.End();
            GL.PopMatrix();
        }
    }
}