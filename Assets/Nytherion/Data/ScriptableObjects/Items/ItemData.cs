using UnityEngine;
using UnityEngine.Serialization;
using Nytherion.Core.Utils;

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
        
        [System.NonSerialized]
        public string instanceId;

        [Header("Basic Info")]
        [FormerlySerializedAs("itemName")]
        public string itemName_KR;
        public string itemName_EN;
        public Sprite icon;
        [FormerlySerializedAs("description")]
        [TextArea] public string description_KR;
        [TextArea] public string description_EN;

        // 하위 호환성 및 다국어 지원을 위한 프로퍼티
        public string itemName => GetLocalizedName();
        public string description => GetLocalizedDescription();

        private string GetLocalizedName()
        {
            return LocalizationText.Get(
                LocalizationTables.Items,
                LocalizationKeys.ItemName(ID),
                itemName_KR,
                itemName_EN);
        }

        private string GetLocalizedDescription()
        {
            return LocalizationText.Get(
                LocalizationTables.Items,
                LocalizationKeys.ItemDescription(ID),
                description_KR,
                description_EN);
        }

        [Header("Inventory Settings")]
        public bool isStackable = true;
        public int maxStack = 99;

        [Header("Unlock Settings")]
        [Tooltip("이 아이템을 해금하기 위해 필요한 마일스톤 ID (비어있으면 기본 해금)")]
        public string unlockMilestoneID;

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
