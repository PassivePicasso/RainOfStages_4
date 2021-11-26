using UnityEngine;

namespace PassivePicasso.RainOfStages.Proxy
{
    public partial class GlobalEventManager : global::RoR2.GlobalEventManager
    {
        void Awake()
        {
            AACannonMuzzleEffect = (GameObject)Resources.Load("prefabs/effects/muzzleflashes/muzzleflashaacannon");
            AACannonPrefab = (GameObject)Resources.Load("prefabs/projectiles/aacannon");
            bleedOnHitAndExplodeBlastEffect = Resources.Load<GameObject>("prefabs/networkedobjects/BleedOnHitAndExplodeDelay");
            bleedOnHitAndExplodeImpactEffect = Resources.Load<GameObject>("prefabs/effects/impacteffects/BleedOnHitAndExplode_Impact");
            chainLightingPrefab = (GameObject)Resources.Load("prefabs/projectiles/chainlightning");
            daggerPrefab = (GameObject)Resources.Load("prefabs/projectiles/daggerprojectile");
            explodeOnDeathPrefab = (GameObject)Resources.Load("prefabs/networkedobjects/willowispdelay");
            healthOrbPrefab = (GameObject)Resources.Load("prefabs/networkedobjects/healthglobe");
            missilePrefab = (GameObject)Resources.Load("prefabs/projectiles/missileprojectile");
            plasmaCorePrefab = (GameObject)Resources.Load("prefabs/projectiles/plasmacore");
        }
    }
}
