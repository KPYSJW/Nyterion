using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Nytherion.Data.Enums;

namespace Nytherion.Data.ScriptableObjects.Gacha
{
    [CreateAssetMenu(fileName = "GachaPool", menuName = "Nytherion/Gacha/Gacha Pool")]
    public class GachaPoolSO : ScriptableObject
    {
        [Tooltip("이 뽑기 풀의 등급")]
        public Rarity rarity;

        [Tooltip("이 등급이 뽑힐 확률 가중치")]
        public int drawWeight;

        [Tooltip("이 풀에 포함된 아이템 목록과 각 아이템의 등장 확률 가중치")]
        public List<GachaItemRate> items;

        public ScriptableObject GetRandomItem()
        {
            if(items == null || items.Count == 0)
            {
                return null;
            }
            float totalWeight = items.Sum(itemRate => itemRate.weight);
            float randomPoint = Random.Range(0f, totalWeight);

            foreach (GachaItemRate itemRate in items)
            {
                if(randomPoint < itemRate.weight)
                {
                    return itemRate.item;
                }
                randomPoint -= itemRate.weight;
            }
            return items.Last().item;
        }

    }
}
