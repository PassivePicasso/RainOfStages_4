using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ThunderKit.Common.Package;
using ThunderKit.Core.Attributes;
using ThunderKit.Core.Manifests.Datums;
using ThunderKit.Core.Paths;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace ThunderKit.Core.Pipelines.Jobs
{
    using Object = UnityEngine.Object;

    struct ScriptPath
    {
        public string path;
        public MonoScript monoScript;
        public ScriptPath(string path, MonoScript mooScript)
        {
            this.path = path;
            this.monoScript = mooScript;
        }
    }
    struct AssetPath
    {
        public string path;
        public Object asset;
        public AssetPath(string path, Object asset)
        {
            this.path = path;
            this.asset = asset;
        }
    }

    struct RemapData
    {
        public string Path;
        public Regex ToAssemblyReference;
        public Regex ToScriptReference;
        public string ScriptReference;
        public string AssemblyReference;

        public RemapData(string path, Regex toAssemblyReference, Regex toScriptReference, string scriptReference, string assemblyReference)
        {
            Path = path;
            ToAssemblyReference = toAssemblyReference;
            ToScriptReference = toScriptReference;
            ScriptReference = scriptReference;
            AssemblyReference = assemblyReference;
        }
    }
    public enum Conversion { ScriptToAssembly, AssemblyToScript }
    [PipelineSupport(typeof(Pipeline))]
    public class ConvertReferences : PipelineJob
    {
        public Conversion conversion;
        public Object[] UnityObjects;
        public override Task Execute(Pipeline pipeline)
        {
            var unityObjects = UnityObjects
                .Select(AssetDatabase.GetAssetPath)
                .SelectMany(path =>
                {
                    if (AssetDatabase.IsValidFolder(path))
                        return Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                    else
                        return Enumerable.Repeat(path, 1);
                })
                .Select(path => new AssetPath(path, AssetDatabase.LoadAssetAtPath<Object>(path)))
                .ToArray();
            var remappableAssets = unityObjects.SelectMany(map =>
            {
                var path = map.path;
                var asset = map.asset;
                if (asset is Preset preset)
                {
                    var presetSo = new SerializedObject(preset);
                    SerializedProperty m_TargetType, m_ManagedTypePPtr;
                    m_TargetType = presetSo.FindProperty(nameof(m_TargetType));
                    m_ManagedTypePPtr = m_TargetType.FindPropertyRelative(nameof(m_ManagedTypePPtr));
                    var monoScript = m_ManagedTypePPtr.objectReferenceValue as MonoScript;

                    return Enumerable.Repeat(new ScriptPath(path, monoScript), 1);
                }

                if (asset is GameObject goAsset)
                    return goAsset.GetComponentsInChildren<MonoBehaviour>()
                                     .Select(mb => new ScriptPath(path, MonoScript.FromMonoBehaviour(mb)));

                if (asset is ScriptableObject soAsset)
                    return Enumerable.Repeat(new ScriptPath(path, MonoScript.FromScriptableObject(soAsset)), 1);


                return Enumerable.Empty<ScriptPath>();
            })
                .Select(map =>
                {
                    var type = map.monoScript.GetClass();
                    var fileId = FileIdUtil.Compute(type);
                    var assemblyPath = type.Assembly.Location;
                    var libraryGuid = PackageHelper.GetFileNameHash(assemblyPath);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(map.monoScript, out string scriptGuid, out long scriptId);

                    return new RemapData(map.path,
                             new Regex($"(\\{{fileID:)\\s*?{scriptId},(\\s*?guid:)\\s*?{scriptGuid},(\\s*?type:\\s*?\\d+\\s*?\\}})", RegexOptions.Singleline),
                             new Regex($"(\\{{fileID:)\\s*?{fileId},(\\s*?guid:)\\s*?{libraryGuid},(\\s*?type:\\s*?\\d+\\s*?\\}})", RegexOptions.Singleline),
                                 $"$1 {scriptId},$2 {scriptGuid},$3",
                                 $"$1 {fileId},$2 {libraryGuid},$3"
                                );
                })
                .ToArray();

            switch (conversion)
            {
                case Conversion.AssemblyToScript:
                    foreach (var map in remappableAssets)
                    {
                        var fileText = File.ReadAllText(map.Path);
                        fileText = map.ToScriptReference.Replace(fileText, map.ScriptReference);
                        File.WriteAllText(map.Path, fileText);
                    }
                    break;
                case Conversion.ScriptToAssembly:
                    foreach (var map in remappableAssets)
                    {
                        var fileText = File.ReadAllText(map.Path);
                        fileText = map.ToAssemblyReference.Replace(fileText, map.AssemblyReference);
                        File.WriteAllText(map.Path, fileText);
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }
}