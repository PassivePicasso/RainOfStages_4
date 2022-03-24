using RoR2;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Proxy
{
    public class InteractableSpawnCard : RoR2.InteractableSpawnCard, IProxyReference<SpawnCard>
    {
        private static FieldInfo[] writableFields;

        static InteractableSpawnCard()
        {
            writableFields = typeof(RoR2.InteractableSpawnCard)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => (field.GetCustomAttributes().Any(attr => attr is SerializeField) || field.IsPublic) && !field.IsNotSerialized)
                .ToArray();

        }
        public SpawnCard ResolveProxy() => Resources.Load<SpawnCard>($"SpawnCards/InteractableSpawnCard/{name}");
        void Awake()
        {
            if (Application.isEditor) return;
            var card = (RoR2.InteractableSpawnCard)ResolveProxy();

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

    }
}
