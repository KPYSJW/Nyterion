using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Nytherion.Data.ScriptableObjects.Items
{
    public abstract class ItemData : ScriptableObject
    {
        [Header("ID")]
        [SerializeField]
        private string uniqueID;

        public string ID => uniqueID;

        [Header("Basic Info")]
        public string itemName;
        public Sprite icon;
        [TextArea] public string description;

        [Header("Inventory Settings")]
        public bool isStackable = true;
        public int maxStack = 99;

        [Header("Value")]
        public int baseValue = 10;

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(uniqueID))
            {
                uniqueID = System.Guid.NewGuid().ToString();
                EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}