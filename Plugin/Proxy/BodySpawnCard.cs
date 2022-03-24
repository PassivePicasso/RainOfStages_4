using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using RoR2;

namespace PassivePicasso.RainOfStages.Proxy
{
    public class BodySpawnCard : RoR2.BodySpawnCard, IProxyReference<SpawnCard>
    {
        private static FieldInfo[] writableFields;

        static BodySpawnCard()
        {
            writableFields = typeof(RoR2.BodySpawnCard)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => (field.GetCustomAttributes().Any(attr => attr is SerializeField) || field.IsPublic) && !field.IsNotSerialized)
                .ToArray();

        }

        void Awake()
        {
            if (Application.isEditor) return;
            var card = (RoR2.BodySpawnCard)ResolveProxy();

            foreach (var field in writableFields)
            {
                try
                {
                    field.SetValue(this, field.GetValue(card));
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }
        public SpawnCard ResolveProxy() => Resources.Load<SpawnCard>($"SpawnCards/BodySpawnCards/{name}");
    }
}
