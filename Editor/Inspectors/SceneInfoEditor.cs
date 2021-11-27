using RoR2;
using UnityEditor;

namespace PassivePicasso.RainOfStages.Designer
{
    [CustomEditor(typeof(SceneInfo), true)]
    public class SceneInfoEditor : UnityEditor.Editor
    {

        public override bool RequiresConstantRepaint()
        {
            var sceneInfo = target as PassivePicasso.RainOfStages.Proxy.SceneInfo;
            return sceneInfo.DebugAirLinks;
        }

        private void OnSceneGUI()
        {
            SceneView.RepaintAll();
        }

    }
}
