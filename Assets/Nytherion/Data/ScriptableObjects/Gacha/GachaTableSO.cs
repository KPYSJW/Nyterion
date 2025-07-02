using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Nytherion.Data.ScriptableObjects.Gacha
{
    [CreateAssetMenu(fileName = "GachaTable", menuName = "Nytherion/Gacha/Gacha Table")]
    public class GachaTableSO : ScriptableObject
    {
        [Tooltip("이 테이블에 포함된 모든 등급별 아이템 풀 리스트")]
        public List<GachaPoolSO> gachaPools;

        public ScriptableObject DrawItem()
        {
            if(gachaPools == null || gachaPools.Count == 0)
            {
                Debug.LogError("GachaPools이 설정되지 않았습니다.");
                return null;
            }
            float totalWeight = gachaPools.Sum(pool => pool.drawWeight);
            float randomPoint = Random.Range(0f, totalWeight);
            GachaPoolSO selectedPool = null;

            foreach (GachaPoolSO pool in gachaPools)
            {
                if(randomPoint < pool.drawWeight)
                {
                    selectedPool = pool;
                    break;
                }
                randomPoint -= pool.drawWeight;
            }
            if(selectedPool == null)
            {
                selectedPool = gachaPools.Last();
            }
            return selectedPool.GetRandomItem();
        }
    }
}
