using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{
    [CreateAssetMenu(fileName = "NewItemData", menuName = "Data/Item")]

    public class ItemData : ScriptableObject
    {
        public string ID => itemName; // 또는 고유한 ID를 위한 별도 필드 추가
        public string itemName;
        public Sprite icon;
        [TextArea] public string description;
        public bool isStackable = true;
        public int maxStack = 99;
    }
}