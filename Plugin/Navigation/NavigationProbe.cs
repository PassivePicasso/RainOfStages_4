using RoR2;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public class NavigationProbe : MonoBehaviour
    {
        public static readonly List<NavigationProbe> ActiveProbes = new List<NavigationProbe>();
        public Color navigationProbeColor = new Color(0, 1, 1, 0.5f);

        public float distance = 15;
        private float lastDistance = float.MinValue;
        public bool drawVolumeSphere = false;
        public bool IsDirty { get; set; }
        public bool setDirty;
        public MeshFilter[] meshFilters;

        private void OnEnable()
        {
            ActiveProbes.Add(this);
        }
        private void OnDisable()
        {
            ActiveProbes.Remove(this);
        }

        void Update()
        {
            if (transform.hasChanged || lastDistance != distance || setDirty)
            {
                transform.hasChanged = false;
                lastDistance = distance;
                IsDirty = true;
                setDirty = false;
                var hits = Physics.OverlapSphereNonAlloc(transform.position, distance, GraphBuilder.colliders, LayerIndex.world.mask);
                meshFilters = GraphBuilder.colliders.Take(hits)
                    .Select(collider => collider.GetComponent<MeshFilter>())
                    .Where(mf => mf)
                    .Where(mf => mf.GetComponentsInParent<ExcludeFromNavigation>().Length == 0)
                    .ToArray();
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.clear;
            Gizmos.DrawCube(transform.position, Vector3.one * 3);

            if (drawVolumeSphere)
            {
                Gizmos.color = new Color(navigationProbeColor.r, navigationProbeColor.g, navigationProbeColor.b, 0.25f);
                Gizmos.DrawSphere(transform.position, distance);
            }
        }
    }
}