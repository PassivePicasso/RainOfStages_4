using System.Reflection;
using UnityEngine;
using RoR2;
using System.Linq;
using System;

namespace PassivePicasso.RainOfStages.Proxy
{
    public class MusicTrackDefRef : MusicTrackDef, IProxyReference<MusicTrackDef>
    {
        private static FieldInfo[] writableFields;

        static MusicTrackDefRef()
        {
            writableFields = typeof(MusicTrackDef)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => (field.GetCustomAttributes().Any(attr => attr is SerializeField) || field.IsPublic) && !field.IsNotSerialized)
                .ToArray();
        }

        void Awake()
        {
            if (Application.isEditor) return;
            var trackDef = ResolveProxy();
            foreach (var field in writableFields)
            {
                try
                {
                    field.SetValue(this, field.GetValue(trackDef));
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        public MusicTrackDef ResolveProxy() => Resources.Load<MusicTrackDef>($"MusicTrackDefs/{(this as ScriptableObject)?.name}");
    }
}
