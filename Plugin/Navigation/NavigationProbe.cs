using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class NavigationProbe : MonoBehaviour
    {
        public Color navigationProbeColor = new Color(0, 1, 1, 0.5f);

        public float distance = 15;
        private float lastDistance = float.MinValue;
        public bool drawVolumeSphere = false;


        void Update()
        {
            if (transform.hasChanged || lastDistance != distance)
            {
                transform.hasChanged = false;
                lastDistance = distance;
                FindObjectOfType<GroundGraphBuilder>().UpdateTriangleCollections();
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = new Color(navigationProbeColor.r, navigationProbeColor.g, navigationProbeColor.b, 1);
            Gizmos.DrawCube(transform.position, Vector3.one * 2);

            if (drawVolumeSphere)
            {
                Gizmos.color = new Color(navigationProbeColor.r, navigationProbeColor.g, navigationProbeColor.b, 0.25f);
                Gizmos.DrawSphere(transform.position, distance);
            }
        }
    }
}