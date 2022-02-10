using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class NavigationProbe : MonoBehaviour
    {
        public Color navigationProbeColor = Color.green;

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
            Gizmos.color = navigationProbeColor;
            Gizmos.DrawCube(transform.position, Vector3.one);

            if (!drawVolumeSphere) return;

            Gizmos.color = new Color(navigationProbeColor.r, navigationProbeColor.g, navigationProbeColor.b, 0.25f);
            Gizmos.DrawSphere(transform.position, distance);
        }
    }
}