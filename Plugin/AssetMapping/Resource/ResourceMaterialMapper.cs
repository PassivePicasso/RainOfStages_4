using System;
using System.Linq;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.AssetMapping
{
    [ExecuteAlways]
    public class ResourceMaterialMapper : ResourceAssetArrayMapper<Renderer, Material>
    {
        [SerializeField]
        private static Material[] defaultMaterials;
        protected override string MemberName => "materials";

        [WeakAssetReference(typeof(Material))]
        public string[] EditorAssets;

#if UNITY_EDITOR
        private void Start()
        {
            string defaultMaterialGuid = UnityEditor.AssetDatabase.AssetPathToGUID("Packages/twiner-rainofstages/RoSShared/Materials/DefaultMaterial.mat");
            EditorAssets = new[] { defaultMaterialGuid };
            //defaultMaterials = new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Packages/twiner-rainofstages/RainOfStages/RoSShared/Materials/DefaultMaterial.mat") };
        }
        public override Material[] ClonedAssets
        {
            get
            {
                var materials = EditorAssets
                                .Select(x => UnityEditor.AssetDatabase.GUIDToAssetPath(x))
                                .Select(x => UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(x))
                                .ToArray();
                if (materials.Any() && materials.Any(asset => asset))
                    return materials
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
                    return defaultMaterials;
                }
            }
        }
#endif
    }
}
