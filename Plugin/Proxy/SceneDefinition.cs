using RoR2;
using System.Collections.Generic;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Proxy
{
    public class SceneDefinition : SceneDef
    {
        [Header("Stage Weaving")]
        [Tooltip("Add this stages to other stages variants collection")]
        public List<SceneDefReference> reverseSceneNameOverrides;

        [Tooltip("Add this stages to other stages destinations")]
        public List<SceneDefReference> destinationInjections;
    }
}