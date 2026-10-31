using UnityEngine;

namespace Nytherion.Data.ScriptableObjects
{
    [CreateAssetMenu(fileName = "StatusEffectDatabase", menuName = "Nytherion/Data/StatusEffectDatabase")]
    public class StatusEffectDatabase : ScriptableObject
    {
        [Header("Status Effect Icons")]
        public Sprite fireIcon;
        public Sprite iceIcon;
        public Sprite lightningIcon;
        public Sprite poisonIcon;
        public Sprite curseIcon;
        public Sprite holyIcon;
        public Sprite demonicIcon;

        [Header("화상 피해 이펙트")]
        public Nytherion.GamePlay.Combat.BurnTickVFX burnTickPrefab;

        [Header("감전 이펙트")]
        public Nytherion.GamePlay.Combat.ShockPulseVFX shockPulsePrefab;
        [Min(0f)] public float lightningBonusDamageRatio = 0.3f;

        public Sprite GetIcon(string effectId)
        {
            switch (effectId)
            {
                case "Fire": return fireIcon;
                case "Ice": return iceIcon;
                case "Lightning": return lightningIcon;
                case "Poison": return poisonIcon;
                case "Curse": return curseIcon;
                case "Holy": return holyIcon;
                case "Demonic": return demonicIcon;
                default: return null;
            }
        }
    }
}
