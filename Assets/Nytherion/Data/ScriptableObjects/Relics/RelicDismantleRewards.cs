using Nytherion.Core.Enums;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Relics
{
    [CreateAssetMenu(fileName = "RelicDismantleRewards", menuName = "Data/Relic Dismantle Rewards")]
    public class RelicDismantleRewards : ScriptableObject
    {
        [Header("등급별 분해 골드")]
        [Min(0)] public int commonGold = 105;
        [Min(0)] public int uncommonGold = 210;
        [Min(0)] public int rareGold = 420;
        [Min(0)] public int epicGold = 840;
        [Min(0)] public int legendaryGold = 1750;

        public int GetGold(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.Common: return Mathf.Max(0, commonGold);
                case Rarity.Uncommon: return Mathf.Max(0, uncommonGold);
                case Rarity.Rare: return Mathf.Max(0, rareGold);
                case Rarity.Epic: return Mathf.Max(0, epicGold);
                case Rarity.Legendary: return Mathf.Max(0, legendaryGold);
                default: return 0;
            }
        }
    }
}
