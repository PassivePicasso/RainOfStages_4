using PassivePicasso.RainOfStages.Plugin.Navigation;
using RoR2;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(StaticNode), true)]
    public class StaticNodeEditor : Editor
    {
        static RaycastHit[] raycastHits = new RaycastHit[128];
        private void OnSceneGUI()
        {
            StaticNode staticNode = (StaticNode)target;
            Vector3 newTargetPosition = staticNode.position;
            using (new Handles.DrawingScope(staticNode.staticNodeColor))
            {
                var changedPosition = Handles.Slider(newTargetPosition, Vector3.up, 1f, Handles.CubeHandleCap, 0.1f);
                if (Vector3.Distance(changedPosition, newTargetPosition) > .1f)
                {
                    Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                    if (Physics.RaycastNonAlloc(mouseRay, raycastHits, float.MaxValue, LayerIndex.world.mask) > 0)
                    {
                        var hitInfo = raycastHits
                            .Where(hit => hit.collider != null)
                            .OrderBy(hit => hit.distance)
                            .FirstOrDefault(hit => !hit.collider.transform.IsChildOf(staticNode.transform));

                        if (hitInfo.collider != null)
                        {
                            Undo.RecordObject(staticNode.gameObject, $"(StaticNode) Change {staticNode.name} Position");
                            staticNode.position = hitInfo.point;
                        }
                    }
                }
            }
        }
    }
}
