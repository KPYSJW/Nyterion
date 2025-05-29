using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{

    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable
    }
    [CreateAssetMenu(fileName = "NewItemData", menuName = "Data/Item")]
    public class ItemData : ScriptableObject
    {
        public ItemType itemType;
        [SerializeField] private string _uniqueID;
        public string ID
        {
            get
            {
                if (string.IsNullOrEmpty(_uniqueID))
                {
#if UNITY_EDITOR
                    if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        _uniqueID = System.Guid.NewGuid().ToString();
                        UnityEditor.EditorUtility.SetDirty(this);
                    }
#endif
                }
                return _uniqueID;
            }
        }
        public string itemName;
        public Sprite icon;
        [TextArea] public string description;
        public bool isStackable = true;
        public int maxStack = 99;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_uniqueID))
            {
                _uniqueID = System.Guid.NewGuid().ToString();
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"ItemData '{this.name}' created or updated with new ID: {_uniqueID}");
            }
        }
#endif
    }
}