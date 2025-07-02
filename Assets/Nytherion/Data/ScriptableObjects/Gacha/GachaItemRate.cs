using UnityEngine;
using System;

namespace Nytherion.Data.ScriptableObjects.Gacha
{
    [Serializable]
    public class GachaItemRate
    {
        public ScriptableObject item;

        [Tooltip("아이템 등장 확률 가중치")]
        public int weight;
        
    }
}
