using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Items
{

    public enum ItemType
    {
        // 장비 아이템 타입
        Weapon,      // 무기
        Helmet,      // 투구
        Armor,       // 갑옷
        Boots,       // 신발
        Accessory,   // 장신구
        
        // 소비 아이템 타입
        Consumable,  // 소비 아이템
        
        // 기타 아이템 타입
        Material,    // 재료 아이템
        Quest        // 퀘스트 아이템
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
        [Header("Basic Info")]
        public string itemName;
        public Sprite icon;
        [TextArea] public string description;
        
        [Header("Inventory Settings")]
        public bool isStackable = true;
        public int maxStack = 99;
        
        [Header("Usage Settings")]
        [Tooltip("이 아이템이 사용 가능한지 여부")]
        public bool isUsable = false;
        
        [Tooltip("사용 시 재생할 이펙트")]
        public GameObject useEffectPrefab;
        
        [Tooltip("사용 시 재생할 사운드")]
        public AudioClip useSound;
        
        // IUsableItem 인터페이스 구현
        public virtual void Use()
        {
            if (!isUsable) return;
            
            Debug.Log($"{itemName}을(를) 사용합니다.");
            
            // 효과 재생
            if (useEffectPrefab != null)
            {
                Instantiate(useEffectPrefab, Vector3.zero, Quaternion.identity);
            }
            
            // 사운드 재생
            if (useSound != null)
            {
                // AudioManager가 있다면 여기서 재생
                // AudioManager.Instance.PlaySound(useSound);
            }
            
            // 아이템 타입별 추가 동작
            switch (itemType)
            {
                case ItemType.Consumable:
                    // 소비 아이템 효과 적용
                    break;
                case ItemType.Weapon:
                    // 무기 장착 로직
                    break;
                default:
                    // 기타 타입 처리
                    break;
            }
        }
        
        public virtual void CancelUse()
        {
            // 사용 취소 시 처리 (예: 채널링 스킬 중단 등)
            Debug.Log($"{itemName} 사용이 취소되었습니다.");
        }

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