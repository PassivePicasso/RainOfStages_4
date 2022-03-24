using RoR2;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace PassivePicasso.RainOfStages.Proxy
{
    public class CharacterSpawnCard : RoR2.CharacterSpawnCard, IProxyReference<SpawnCard>
    {
        //static FieldInfo runtimeLoadoutField = typeof(RoR2.CharacterSpawnCard).GetField("runtimeLoadout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private static FieldInfo[] writableFields;

        static CharacterSpawnCard()
        {
            writableFields = typeof(RoR2.CharacterSpawnCard)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => (field.GetCustomAttributes().Any(attr => attr is SerializeField) || field.IsPublic) && !field.IsNotSerialized)
                .ToArray();

        }
        new void Awake()
        {
            if (Application.isEditor) return;
            var card = (RoR2.CharacterSpawnCard)ResolveProxy();

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
            base.Awake();
        }

        public SpawnCard ResolveProxy() 
        {
            switch (name)
            {
                case "cscGrandparent":
                case "cscTitanBlackBeach":
                case "cscTitanDampCave":
                case "cscTitanGolemPlains":
                case "cscTitanGooLake":
                    return Resources.Load<SpawnCard>($"SpawnCards/CharacterSpawnCards/Titan/{name}");
                default:
                    return Resources.Load<SpawnCard>($"SpawnCards/CharacterSpawnCards/{name}");
            }
        }
    }
}
