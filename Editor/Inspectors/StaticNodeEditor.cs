using PassivePicasso.RainOfStages.Plugin.Navigation;
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
                    if (Physics.Raycast(mouseRay, out var hitInfo, float.MaxValue))
                    {
                        Undo.RecordObject(staticNode.gameObject, $"(StaticNode) Change {staticNode.name} Position");
                        staticNode.position = hitInfo.point;
                        if (staticNode.onChanged != null)
                            staticNode.onChanged.Invoke();
                        serializedObject.ApplyModifiedProperties();
                        serializedObject.UpdateIfRequiredOrScript();
                    }
                    if (Physics.RaycastNonAlloc(mouseRay, raycastHits, float.MaxValue) > 0)
                    {
                        var hitsByDistance = raycastHits
                                                    .Where(hit => hit.collider != null)
                                                    .OrderBy(hit => hit.distance);

                        var closestNonChildHitInfo = hitsByDistance
                            .FirstOrDefault(hit => !hit.collider.transform.IsChildOf(staticNode.transform));

                        if (closestNonChildHitInfo.collider != null)
                        {
                            Undo.RecordObject(staticNode.gameObject, $"(StaticNode) Change {staticNode.name} Position");
                            staticNode.position = closestNonChildHitInfo.point;
                            if (staticNode.onChanged != null)
                                staticNode.onChanged.Invoke();
                            serializedObject.ApplyModifiedProperties();
                            serializedObject.UpdateIfRequiredOrScript();
                        }
                    }
                }
            }
        }
    }
}

