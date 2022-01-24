using PassivePicasso.RainOfStages.Plugin.Navigation;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Behaviours
{
    [ExecuteAlways, RequireComponent(typeof(SphereCollider))]
    public class JumpPad : MonoBehaviour
    {
        private static readonly string DebugShaderName = "Unlit/Color";
        public float time;
        public Vector3 destination;
        private Vector3 origin => transform.position;
        public int groundingDistance = -1;
        public string jumpSoundString;
        [SerializeField, HideInInspector]
        private StaticNode originNode;
        [SerializeField, HideInInspector]
        private StaticNode destinationNode;

        private void Update()
        {
            if (!originNode)
            {
                originNode = gameObject.AddComponent<StaticNode>();
                originNode.overridePosition = false;
                originNode.overrideDistanceScore = true;
                originNode.staticNodeColor = Color.cyan;
            }
            if (!destinationNode)
            {
                destinationNode = gameObject.AddComponent<StaticNode>();
                destinationNode.overridePosition = true;
                destinationNode.overrideDistanceScore = true;
            }
            originNode.hideFlags = HideFlags.NotEditable;
            destinationNode.hideFlags = HideFlags.NotEditable;

            if (originNode.HardLinks == null || !originNode.HardLinks.Contains(destinationNode))
                originNode.HardLinks = new StaticNode[] { destinationNode };

            destinationNode.position = destination;
            if (groundingDistance > 0)
            {
                var groundingPosition = transform.position + Vector3.up * groundingDistance;
                if (Physics.Raycast(new Ray(groundingPosition, Vector3.down), out var hitInfo, groundingDistance * 2, LayerIndex.world.mask))
                    groundingPosition = hitInfo.point;
                transform.position = groundingPosition;
            }
        }

        public void OnTriggerEnter(Collider other)
        {
            RoR2.CharacterMotor motor = other.GetComponent<CharacterMotor>();
            if (!motor || !motor.hasEffectiveAuthority) return;

            if (!motor.disableAirControlUntilCollision)
            {
                _ = Util.PlaySound(jumpSoundString, gameObject);
            }

            motor.disableAirControlUntilCollision = true;
            motor.velocity = GetVelocity(time);
            motor.Motor.ForceUnground();
        }

        private Material gizmoMaterial;
        private static Mesh mesh;
        void OnDrawGizmos()
        {
            if (!gizmoMaterial || gizmoMaterial.shader.name != DebugShaderName)
                gizmoMaterial = new Material(Shader.Find(DebugShaderName));
            gizmoMaterial.color = Color.red;
            gizmoMaterial.SetPass(0);

            var trajectory = Trajectory().ToArray();
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            for (int i = 0; i < trajectory.Length; i++)
            {
                GL.Color(Color.red);
                GL.Vertex3(trajectory[i].x, trajectory[i].y, trajectory[i].z);
            }
            GL.End();
            GL.PopMatrix();

            var planarTargetPosition = new Vector3(destination.x, origin.y, destination.z);
            transform.forward = planarTargetPosition - origin;
        }

        public IEnumerable<Vector3> Trajectory()
        {
            var to = transform.position;
            var tf = time * 1.75f;
            var velocity = GetVelocity(tf);
            var timeStep = Time.fixedDeltaTime * 8;
            for (float f = tf; f > 0; f -= timeStep)
            {
                var from = to;
                var delta = velocity * timeStep;
                to = from + delta;

                var ray = new Ray(from, velocity.normalized);
                var impact = Physics.Raycast(ray, out RaycastHit hit, delta.magnitude * timeStep);
                velocity += Physics.gravity * timeStep;

                yield return impact ? hit.point : to;
            }
        }

        public Vector3 GetVelocity(float time)
        {
            var (displacement3d, displacementXZ, direction) = LoadVariables();

            float planarVelocity = RoR2.Trajectory.CalculateGroundSpeed(time, displacementXZ.magnitude);
            float verticalVelocity = RoR2.Trajectory.CalculateInitialYSpeed(time, displacement3d.y);

            return new Vector3(direction.x * planarVelocity, verticalVelocity, direction.z * planarVelocity);
        }

        (Vector3 offset, Vector3 planarOffset, Vector3 normalPlanarOffset) LoadVariables()
        {
            var displacement3d = destination - origin;
            var displacementXZ = Vector3.ProjectOnPlane(displacement3d, Vector3.up);
            var directionNormalized = displacementXZ.normalized;

            return (displacement3d, displacementXZ, directionNormalized);
        }
    }
}
