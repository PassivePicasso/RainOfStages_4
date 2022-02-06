using PassivePicasso.RainOfStages.Plugin.AssetMapping;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace PassivePicasso.RainOfStages.Designer.Inspectors
{
    [CustomEditor(typeof(ResourceMaterialMapper)), CanEditMultipleObjects]
    public class ResourceMaterialMapperEditor : Editor
    {
        static Material[] defaultMaterials = new Material[1];
        static Dictionary<ResourceMaterialMapper, string[]> EditorAssets;
        [InitializeOnLoadMethod]
        static void Initializer()
        {
            EditorApplication.update += RegisterAssignmentEvents;
            defaultMaterials[0] = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath("81d27759c3eb94140aca5d3a7b549e3e"));
            EditorAssets = new Dictionary<ResourceMaterialMapper, string[]>();
        }

        private static void RegisterAssignmentEvents()
        {
            var allRmms = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ResourceMaterialMapper>()).Distinct().ToArray();
            foreach (var rmm in allRmms)
            {
                rmm.BeforeEditorAssign -= OnAssign;
                rmm.BeforeEditorAssign += OnAssign;
            }
        }

        private static void OnAssign(AssetArrayMapper<Renderer, Material> aam)
        {
            var rmm = aam as ResourceMaterialMapper;
            bool rebuild = false;
            if (EditorAssets.ContainsKey(rmm))
            {
                var cachedAssets = EditorAssets[rmm];
                var rmmAssets = rmm.EditorAssets;
                if (cachedAssets.Length != rmmAssets.Length) rebuild = true;
                else if (cachedAssets.Any(guid => !rmmAssets.Contains(guid))) rebuild = true;
                else if (rmmAssets.Any(guid => !cachedAssets.Contains(guid))) rebuild = true;

                if (rebuild)
                    EditorAssets[rmm] = rmmAssets;
            }
            else rebuild = true;
            if (!rebuild) return;

            EditorAssets[rmm] = rmm.EditorAssets;
            var materials = rmm.EditorAssets
                          .Select(x => UnityEditor.AssetDatabase.GUIDToAssetPath(x))
                          .Select(x => UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(x))
                          .ToArray();

            if (materials.Any() && materials.Any(asset => asset))
                rmm.ClonedAssets = materials
                    .Where(asset => asset)
                    .Select(Instantiate)
                    .Select(clone =>
                    {
                        clone.hideFlags = HideFlags.HideAndDontSave;
                        clone.name = clone.name.Replace("(Clone)", "(WeakAssetReference)");
                        return clone;
                    })
                    .ToArray();
            else
            {
                rmm.ClonedAssets = defaultMaterials;
            }

        }
    }
}