using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;

namespace Nytherion.Core.Managers
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
        }
        public List<ScriptableObject> TryDrawItems(GachaType type, int count)
        {
            Debug.Log($"[GachaManager] TryDrawItems 호출. 타입: {type}, 횟수: {count}");

            if (CurrencyManager.Instance.GetCurrency(CurrencyType.Token) < count)
            {
                Debug.LogError($"[GachaManager] 토큰 부족. 현재 토큰: {CurrencyManager.Instance.GetCurrency(CurrencyType.Token)}, 필요 토큰: {count}");
                return null;
            }

            if (type == GachaType.Weapon && count > 1 && InventoryManager.Instance.IsFull)
            {
                Debug.LogError("[GachaManager] 인벤토리가 가득 찼습니다.");
                return null;
            }

            CurrencyManager.Instance.SpendCurrency(CurrencyType.Token, count);
            Debug.Log($"[GachaManager] 토큰 {count}개 사용 완료.");

            GachaTableSO currentTable = (type == GachaType.Weapon) ? weaponGachaTable : engravingGachaTable;
            if (currentTable == null)
            {
                Debug.LogError($"[GachaManager] Gacha Table({type})이 설정되지 않았습니다.");
                return null;
            }

            List<ScriptableObject> drawnItems = new List<ScriptableObject>();
            for (int i = 0; i < count; i++)
            {
                ScriptableObject item = currentTable.DrawItem();
                if (item != null)
                {
                    Debug.Log($"[GachaManager] 아이템 뽑기 성공: {item.name}");
                    drawnItems.Add(item);
                    ProcessDrawnItem(item);
                }
                else
                {
                    Debug.LogError("[GachaManager] DrawItem()에서 null을 반환했습니다. GachaTableSO 설정을 확인하세요.");
                }
            }

            Debug.Log($"[GachaManager] 최종적으로 {drawnItems.Count}개의 아이템을 뽑았습니다.");
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