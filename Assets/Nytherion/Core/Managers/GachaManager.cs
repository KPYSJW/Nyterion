using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Data.ScriptableObjects.Skill;
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
        Relic,
        Skill
    }
    public class GachaManager : BaseManager, IInitializable, ISaveable
    {

        [Header("장비 뽑기 테이블")]
        [SerializeField] private GachaTableSO weaponGachaTable;

        [Header("각인 뽑기 테이블")]
        [SerializeField] private GachaTableSO relicGachaTable;

        [Header("스킬 뽑기 테이블")] 
        [SerializeField] private GachaTableSO skillGachaTable;

        private CurrencyDataManager currencyDataManager;
        private InventoryDataManager inventoryDataManager;
        private RelicManager relicManager;

        private IProgressionManager progressionManager;
        private SkillDataManager skillDataManager;

        [Inject]
        public void Construct(
            CurrencyDataManager currencyDataManager,
            InventoryDataManager inventoryDataManager,
            RelicManager relicManager,
            IProgressionManager progressionManager,
            SkillDataManager skillDataManager)
        {
            this.currencyDataManager = currencyDataManager;
            this.inventoryDataManager = inventoryDataManager;
            this.relicManager = relicManager;
            this.progressionManager = progressionManager;
            this.skillDataManager = skillDataManager;
        }
        public override void Initialize()
        {
            if (weaponGachaTable == null || relicGachaTable == null || skillGachaTable == null)
            {
                Debug.LogError("[GachaManager] GachaTables이 완전히 할당되지 않았습니다.");
            }
        }
        public override void PopulateSaveData(SaveData saveData) { }
        public override void LoadFromSaveData(SaveData saveData) { }
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
                case GachaType.Skill: currentTable = skillGachaTable; break;
            }

            if (currentTable == null)
            {
                Debug.LogError($"[GachaManager] Gacha Table({type})이 설정되지 않았습니다.");
                return null;
            }

            System.Func<ScriptableObject, bool> validationCheck = (item) =>
            {
                if (item is SkillData skillData)
                {
                    return progressionManager != null && progressionManager.IsSkillUnlocked(skillData);
                }
                return true;
            };

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
            else if (item is SkillData skill)
            {
                skillDataManager.AcquireSkill(skill);
            }
        }
    }
}
