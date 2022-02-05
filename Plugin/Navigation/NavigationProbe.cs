using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class NavigationProbe : MonoBehaviour
    {
        private static Mesh mesh;
        public Color navigationProbeColor = Color.green;

        public int seed = 1;
        [SerializeField]
        public List<Vector3> nodePositions = new List<Vector3>();
        public float distance = 15;
        public float minimumSurfaceDistance = 2f;
        public float maximumSurfaceDistance = 30f;
        public float linkDistance = 10;
        public float nodeSeparation = 5;
        public int targetPointCount = 100;
        public int pointPasses = 10;
        public bool isDirty = false;
        public bool drawVolumeSphere = false;

        [SerializeField, HideInInspector]
        private Vector3 lastPosition, lastScale;
        [SerializeField, HideInInspector]
        private Quaternion lastRotation;
        [SerializeField, HideInInspector]
        private float lastDistance;
        [SerializeField, HideInInspector]
        private int lastSeed;
        [SerializeField, HideInInspector]
        private float lastLinkDistance = 10;
        [SerializeField, HideInInspector]
        private float lastNodeSeparation = 5;
        [SerializeField, HideInInspector]
        private int lastTargetPointCount = 5;
        [SerializeField, HideInInspector]
        public float lastMinimumSurfaceDistance = 2f;
        [SerializeField, HideInInspector]
        public float lastMaximumSurfaceDistance = 30f;

        public void Update()
        {
            if (Vector3.Distance(lastPosition, transform.position) > 0.1f
              || lastScale != transform.localScale
              || lastRotation != transform.rotation
              || lastDistance != distance
              || lastLinkDistance != linkDistance
              || lastNodeSeparation != nodeSeparation
              || lastTargetPointCount != targetPointCount
              || lastSeed != seed
              || lastMinimumSurfaceDistance != minimumSurfaceDistance
              || lastMaximumSurfaceDistance != maximumSurfaceDistance
              )
            {
                lastPosition = transform.position;
                lastScale = transform.localScale;
                lastRotation = transform.rotation;
                lastDistance = distance;
                lastLinkDistance = linkDistance;
                lastNodeSeparation = nodeSeparation;
                lastTargetPointCount = targetPointCount;
                lastMinimumSurfaceDistance = minimumSurfaceDistance;
                lastMaximumSurfaceDistance = maximumSurfaceDistance;
                lastSeed = seed;
                isDirty = true;
            }
        }
        void OnDrawGizmos()
        {
            if (!drawVolumeSphere) return;

            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.black;
            Gizmos.DrawCube(transform.position, Vector3.one);
            var maxLength = 0f;
            if (nodePositions.Any())
                maxLength = nodePositions.Max(v => v.magnitude);
            else
                maxLength = distance;

            Gizmos.color = new Color(navigationProbeColor.r, navigationProbeColor.g, navigationProbeColor.b, 0.5f);
            Gizmos.DrawSphere(transform.position, maxLength);
        }
    }
}