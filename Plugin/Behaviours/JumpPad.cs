using PassivePicasso.RainOfStages.Plugin.Navigation;
using RoR2;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Behaviours
{
    [ExecuteAlways]
    public class JumpPad : MonoBehaviour
    {
        private static readonly string DebugShaderName = "RainOfStages/VertexColor";
        public float time;
        public Vector3 destination;
        private Vector3 origin => transform.position;
        public string jumpSoundString;
        [SerializeField, HideInInspector]
        public StaticNode originNode;
        [SerializeField, HideInInspector]
        public StaticNode destinationNode;
        public bool regenerateStaticNodes = false;

        public void Update()
        {
            if (Application.isPlaying) return;
            if (regenerateStaticNodes)
                foreach (var node in GetComponents<StaticNode>())
                    DestroyImmediate(node);
            if (!destinationNode)
            {
                destinationNode = gameObject.AddComponent<StaticNode>();
                destinationNode.nodeName = "Destination";
                destinationNode.forbiddenHulls = HullMask.None;
                destinationNode.HardLinks = new StaticNode[0];
                destinationNode.staticNodeColor = Color.green;
                destinationNode.worldSpacePosition = true;
                destinationNode.relativePosition = false;
                destinationNode.overrideDistanceScore = true;
                destinationNode.allowDynamicConnections = true;
                destinationNode.nodePosition = transform.forward * 10;
            }
            if (!originNode)
            {
                originNode = gameObject.AddComponent<StaticNode>();
                originNode.nodeName = "Origin";
                originNode.forbiddenHulls = HullMask.BeetleQueen;
                originNode.staticNodeColor = Color.cyan;
                originNode.HardLinks = new StaticNode[] { destinationNode };
                originNode.worldSpacePosition = false;
                originNode.relativePosition = false;
                originNode.overrideDistanceScore = true;
                originNode.allowDynamicConnections = true;
                originNode.allowOutboundConnections = false;
            }
            if (regenerateStaticNodes) regenerateStaticNodes = false;

            destinationNode.onChanged -= OnDestinationChanged;
            destinationNode.onChanged += OnDestinationChanged;

            destination = destinationNode.position;
        }

        private void OnDestinationChanged()
        {
            destination = destinationNode.position;
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
        void OnDrawGizmos()
        {
            if (!gizmoMaterial || gizmoMaterial.shader.name != DebugShaderName)
                gizmoMaterial = new Material(Shader.Find(DebugShaderName));
            gizmoMaterial.SetPass(0);
            Color originColor = originNode.staticNodeColor;
            Color destinationColor = destinationNode.staticNodeColor;

            var trajectory = Trajectory().ToArray();
            GL.PushMatrix();
            GL.Begin(GL.LINES);
            for (int i = 0; i < trajectory.Length - 1; i++)
            {
                var t = (float)i / (float)trajectory.Length;
                Color currentColor = Color.Lerp(originColor, destinationColor, t);
                GL.Color(currentColor);
                GL.Vertex3(trajectory[i].x, trajectory[i].y, trajectory[i].z);
                
                t = (float)(i + 1) / (float)trajectory.Length;
                currentColor = Color.Lerp(originColor, destinationColor, t);
                GL.Color(currentColor);
                GL.Vertex3(trajectory[i + 1].x, trajectory[i + 1].y, trajectory[i + 1].z);
            }
            GL.End();
            GL.PopMatrix();

            var planarTargetPosition = new Vector3(destination.x, origin.y, destination.z);
            transform.forward = planarTargetPosition - origin;
        }

        public IEnumerable<Vector3> Trajectory()
        {
            var to = transform.position;
            var tf = time;
            var velocity = GetVelocity(tf);
            var timeStep = Time.fixedDeltaTime;
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
