using PassivePicasso.RainOfStages.Plugin.Utilities;
using RoR2;
using RoR2.Navigation;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;
using static RoR2.Navigation.NodeGraph;

namespace PassivePicasso.RainOfStages.Plugin.Navigation
{
    [ExecuteAlways]
    public abstract class GraphBuilder : MonoBehaviour
    {
        public NodeGraph nodeGraph;

        protected readonly List<int> resultsIndices = new List<int>();

        public static UnityAction OnBuilt;

        public void Build()
        {
            OnBuild();
            OnBuilt?.Invoke();
        }

        protected abstract void OnBuild();

        protected void InitializeSeed(int seed)
        {
            if (seed == -1)
            {
                Random.InitState((int)Time.realtimeSinceStartup);
            }
            else
            {
                Random.InitState(seed);
            }
        }

        protected void Apply(FieldInfo nodeGraphAssetField, string graphName, List<Node> nodes, List<Link> links)
        {
            Profiler.BeginSample("Save Graph Changes");
            var sceneInfo = FindObjectOfType<SceneInfo>();
            nodeGraph = (NodeGraph)nodeGraphAssetField.GetValue(sceneInfo);
            if (!nodeGraph)
            {
                nodeGraph = ScriptableObject.CreateInstance<NodeGraph>();
                nodeGraph.name = graphName;
            }

            nodeGraph.SetNodes(nodes);
            nodeGraph.SetLinks(links);
            nodeGraphAssetField.SetValue(sceneInfo, nodeGraph);
            Profiler.EndSample();
        }
    }
}