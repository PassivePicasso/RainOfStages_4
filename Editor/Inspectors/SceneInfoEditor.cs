using RoR2;
using UnityEditor;

namespace Packages.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(SceneInfo), true)]
    public class SceneInfoEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawPropertiesExcluding(serializedObject, "m_Script", "approximateMapBoundMesh", "groundNodeGroup", "airNodeGroup", "railNodeGroup");
        }
    }
}
