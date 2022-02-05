using System.Linq;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Plugin.AssetMapping
{
    [ExecuteAlways]
    public class ResourceMaterialMapper : ResourceAssetArrayMapper<Renderer, Material>
    {
        protected override string MemberName => "materials";

        [WeakAssetReference(typeof(Material))]
        public string[] EditorAssets;
    }
}
