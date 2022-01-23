using RoR2;
using RoR2.Navigation;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class StaticNode : MonoBehaviour
    {
        private static Mesh mesh;
        public Color staticNodeColor = Color.green;

        [EnumMask(typeof(HullMask))]
        public HullMask forbiddenHulls;
        [EnumMask(typeof(NodeFlags))]
        public NodeFlags nodeFlags;
        public bool overridePosition;
        public Vector3 position;
        [SerializeField, HideInInspector]
        public Vector3 lastPosition;
        public StaticNode[] HardLinks;
        public bool overrideDistanceScore;
        public float distanceScore;

        private void Update()
        {
            if (!overridePosition)
                position = transform.position;

            if (lastPosition != position)
            {
                lastPosition = position;
                try
                {
                    var ggb = Resources.FindObjectsOfTypeAll<GroundGraphBuilder>()[0];
                    ggb.rebuild = true;
                }
                catch { }
            }
        }

        void OnDrawGizmos()
        {
            if (!mesh)
            {
                var filter = GameObject
                    .CreatePrimitive(PrimitiveType.Cube)
                    .GetComponent<MeshFilter>();
                mesh = filter.sharedMesh;
                DestroyImmediate(filter.gameObject);
            }

            Gizmos.matrix = Matrix4x4.TRS(position - Vector3.up * 0.5f, Quaternion.identity, Vector3.one * 2);
            Gizmos.color = staticNodeColor;
            Gizmos.DrawMesh(mesh);
        }
    }
}