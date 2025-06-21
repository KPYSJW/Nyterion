using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;

namespace Nytherion.Core
{
    public enum GachaType
    {
        Weapon,
        Engraving
    }
    public class GachaManager : MonoBehaviour
    {
        public static GachaManager Instance { get; private set; }

        [Header("Gacha Tables")]
        [Tooltip("장비 뽑기 테이블")]
        [SerializeField] private GachaTableSO weaponGachaTable;
        [Tooltip("각인 뽑기 테이블")]
        [SerializeField] private GachaTableSO engravingGachaTable;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }
        public void Initialize()
        {
            if (weaponGachaTable == null || engravingGachaTable == null)
            {
                Debug.LogError("Gacha Tables are not assigned in GachaManager!");
            }
            Debug.Log("GachaManager Initialized");
        }
        public List<ScriptableObject> TryDrawItems(GachaType type, int count)
        {
            if (CurrencyManager.Instance.GetCurrency(CurrencyType.Token) < count)
            {
                Debug.LogError("토큰 부족");
                return null;
            }

            if (type == GachaType.Weapon && count > 1 && InventoryManager.Instance.IsFull)
            {
                Debug.LogError("인벤토리가 가득 찼습니다.");
                return null;
            }

            CurrencyManager.Instance.SpendCurrency(CurrencyType.Token, count);

            GachaTableSO currentTable = (type == GachaType.Weapon) ? weaponGachaTable : engravingGachaTable;
            if (currentTable == null)
            {
                Debug.LogError("Gacha Table이 설정되지 않았습니다.");
                return null;
            }

            List<ScriptableObject> drawnItems = new List<ScriptableObject>();
            for (int i = 0; i < count; i++)
            {
                ScriptableObject item = currentTable.DrawItem();
                if (item != null)
                {
                    drawnItems.Add(item);
                    ProcessDrawnItem(item);
                }
            }
            return drawnItems;
        }
        private void ProcessDrawnItem(ScriptableObject item)
        {
            if (item is WeaponData weapon)
            {
                InventoryManager.Instance.AddItem(weapon);
            }
            else if (item is EngravingData engraving)
            {
                EngravingManager.Instance.AddNewEngravingToStorage(engraving);
            }
        }
    }
}