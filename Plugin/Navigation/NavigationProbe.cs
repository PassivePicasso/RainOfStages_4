using System.Collections.Generic;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class NavigationProbe : MonoBehaviour
    {
        public int seed = 1;
        [SerializeField]
        public List<Vector3> nodePositions = new List<Vector3>();
        public float distance = 15;
        public float minimumSurfaceDistance = 2f;
        public float maximumSurfaceDistance = 30f;
        public int targetPointCount = 100;
        public int pointPasses = 10;
        public int linkDistance = 10;
        public int nodeSeparation = 5;
        public bool isDirty = false;

        [SerializeField, HideInInspector]
        private Vector3 lastPosition, lastScale;
        [SerializeField, HideInInspector]
        private Quaternion lastRotation;
        [SerializeField, HideInInspector]
        private float lastDistance;
        [SerializeField, HideInInspector]
        private int lastSeed;
        [SerializeField, HideInInspector]
        private int lastLinkDistance = 10;
        [SerializeField, HideInInspector]
        private int lastNodeSeparation = 5;
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
    }
}