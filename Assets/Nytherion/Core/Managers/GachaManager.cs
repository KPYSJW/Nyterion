using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using VContainer;
using VContainer.Unity;

namespace Nytherion.Core.Managers
{
    public enum GachaType
    {
        Weapon,
        Engraving
    }
    public class GachaManager : BaseManager, IInitializable, ISaveable
    {

        [Header("장비 뽑기 테이블")]
        [SerializeField] private GachaTableSO weaponGachaTable;

        [Header("각인 뽑기 테이블")]
        [SerializeField] private GachaTableSO engravingGachaTable;

        private CurrencyDataManager currencyDataManager;
        private InventoryDataManager inventoryDataManager;
        private EngravingManager engravingManager;

       [Inject]
        public void Construct(CurrencyDataManager currencyDataManager, InventoryDataManager inventoryDataManager, EngravingManager engravingManager)
        {
            this.currencyDataManager = currencyDataManager;
            this.inventoryDataManager = inventoryDataManager;
            this.engravingManager = engravingManager;
        }
        public override void Initialize()
        {
            if (weaponGachaTable == null || engravingGachaTable == null)
            {
                Debug.LogError("[GachaManager] GachaTables이 할당되지 않았습니다.");
            }
        }
        public override void PopulateSaveData(SaveData saveData)
        {
            // 저장할 데이터 설정
        }
        public override void LoadFromSaveData(SaveData saveData)
        {
            // 저장된 데이터 로드
        }
        public List<ScriptableObject> TryDrawItems(GachaType type, int count)
        {

            if (currencyDataManager.GetCurrency(CurrencyType.Token) < count)
            {
                Debug.LogError($"[GachaManager] 토큰 부족. 현재 토큰: {currencyDataManager.GetCurrency(CurrencyType.Token)}, 필요 토큰: {count}");
                return null;
            }

            if (type == GachaType.Weapon && count > 1 && inventoryDataManager.IsFull())
            {
                Debug.LogError("[GachaManager] 인벤토리가 가득 찼습니다.");
                return null;
            }

            currencyDataManager.SpendCurrency(CurrencyType.Token, count);

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
                    drawnItems.Add(item);
                    ProcessDrawnItem(item);
                }
                else
                {
                    Debug.LogError("[GachaManager] DrawItem()에서 null을 반환했습니다. GachaTableSO 설정을 확인하세요.");
                }
            }

            return drawnItems;
        }
        
        private void ProcessDrawnItem(ScriptableObject item)
        {
            if (item is WeaponData weapon)
            {
                inventoryDataManager.AddItem(weapon);
            }
            else if (item is EngravingData engraving)
            {
                engravingManager.AddNewEngravingToStorage(engraving);
            }
        }
    }
}