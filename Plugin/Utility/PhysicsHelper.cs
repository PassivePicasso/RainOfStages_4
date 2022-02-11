using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.Utilities
{
    public static class PhysicsHelper
    {
        public static readonly Collider[] colliders = new Collider[128];
        public static readonly RaycastHit[] hitArray = new RaycastHit[128];
    }
}
