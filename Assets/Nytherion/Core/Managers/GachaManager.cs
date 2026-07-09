using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Relics;
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
        Relic
    }

    /// <summary>
    /// 무기와 각인(유물) 가챠를 전담하는 매니저 클래스 (스킬 가챠는 각인 시스템으로 통합되어 제거됨)
    /// </summary>
    public class GachaManager : BaseManager, IInitializable, ISaveable
    {
        [Header("장비 뽑기 테이블")]
        [SerializeField] private GachaTableSO weaponGachaTable;

        [Header("각인 뽑기 테이블")]
        [SerializeField] private GachaTableSO relicGachaTable;

        private CurrencyDataManager currencyDataManager;
        private InventoryDataManager inventoryDataManager;
        private RelicManager relicManager;

        [Inject]
        public void Construct(
            CurrencyDataManager currencyDataManager,
            InventoryDataManager inventoryDataManager,
            RelicManager relicManager)
        {
            this.currencyDataManager = currencyDataManager;
            this.inventoryDataManager = inventoryDataManager;
            this.relicManager = relicManager;
        }

        public override void Initialize()
        {
            if (weaponGachaTable == null || relicGachaTable == null)
            {
                Debug.LogError("[GachaManager] GachaTables(Weapon/Relic)이 완전히 할당되지 않았습니다.");
            }
        }

        public override void PopulateSaveData(SaveData saveData) { }
        public override void LoadFromSaveData(SaveData saveData) { }

        /// <summary>
        /// 지정된 가챠 타입과 수량만큼 아이템을 가챠 테이블에서 추첨하여 지급
        /// </summary>
        public List<ScriptableObject> TryDrawItems(GachaType type, int count)
        {
            if (currencyDataManager.GetCurrency(CurrencyType.Token) < count)
            {
                Debug.LogWarning($"[GachaManager] 토큰 부족. 현재 토큰: {currencyDataManager.GetCurrency(CurrencyType.Token)}, 필요 토큰: {count}");
                return null;
            }

            if (type == GachaType.Weapon && inventoryDataManager.GetEmptySlotCount() < count)
            {
                Debug.LogWarning("[GachaManager] 인벤토리가 가득 찼습니다.");
                return null;
            }

            GachaTableSO currentTable = null;
            switch (type)
            {
                case GachaType.Weapon: currentTable = weaponGachaTable; break;
                case GachaType.Relic: currentTable = relicGachaTable; break;
            }

            if (currentTable == null)
            {
                Debug.LogError($"[GachaManager] Gacha Table({type})이 설정되지 않았습니다.");
                return null;
            }

            System.Func<ScriptableObject, bool> validationCheck = (item) => true;

            if (currentTable.DrawItem(validationCheck) == null)
            {
                Debug.LogWarning($"[GachaManager] {type} 풀에 해금된 아이템이 없어 뽑기를 진행할 수 없습니다. (토큰 미차감)");
                return null;
            }

            currencyDataManager.SpendCurrency(CurrencyType.Token, count);

            List<ScriptableObject> drawnItems = new List<ScriptableObject>();

            for (int i = 0; i < count; i++)
            {
                ScriptableObject item = currentTable.DrawItem(validationCheck);

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
                inventoryDataManager.AddItem(weapon);
            }
            else if (item is RelicData relic)
            {
                relicManager.AddNewRelicToStorage(relic);
            }
        }
    }
}
