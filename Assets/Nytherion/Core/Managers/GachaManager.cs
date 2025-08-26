using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Gacha;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Data;
using Zenject;

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

        private CurrencyManager currencyManager;
        private InventoryManager inventoryManager;
        private EngravingManager engravingManager;

       [Inject]
        public void Construct(CurrencyManager currencyManager, InventoryManager inventoryManager, EngravingManager engravingManager)
        {
            this.currencyManager = currencyManager;
            this.inventoryManager = inventoryManager;
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
            // 유효성 검사
            Debug.Log($"[GachaManager] TryDrawItems 호출. 타입: {type}, 횟수: {count}");

            // 토큰을 확인하고 소비
            if (currencyManager.GetCurrency(CurrencyType.Token) < count)
            {
                Debug.LogError($"[GachaManager] 토큰 부족. 현재 토큰: {currencyManager.GetCurrency(CurrencyType.Token)}, 필요 토큰: {count}");
                return null;
            }

            // 장비 가챠 진행 시 인벤토리가 가득차있는지 확인
            if (type == GachaType.Weapon && count > 1 && inventoryManager.IsFull)
            {
                Debug.LogError("[GachaManager] 인벤토리가 가득 찼습니다.");
                return null;
            }

            // 토큰 소비
            currencyManager.SpendCurrency(CurrencyType.Token, count);
            Debug.Log($"[GachaManager] 토큰 {count}개 사용 완료.");

            // 가챠 테이블 설정
            // 타입에 따라 다른 테이블을 사용 (장비인지 각인인지에 따라)
            GachaTableSO currentTable = (type == GachaType.Weapon) ? weaponGachaTable : engravingGachaTable;

            // 테이블이 설정되지 않았다면 null 반환
            if (currentTable == null)
            {
                Debug.LogError($"[GachaManager] Gacha Table({type})이 설정되지 않았습니다.");
                return null;
            }

            // 뽑은 아이템을 저장하는 리스트 
            List<ScriptableObject> drawnItems = new List<ScriptableObject>();

            // 뽑은 아이템을 리스트에 추가
            for (int i = 0; i < count; i++)
            {
                // 뽑은 아이템
                ScriptableObject item = currentTable.DrawItem();

                // 뽑은 아이템이 null이 아니라면
                if (item != null)
                {
                    Debug.Log($"[GachaManager] 아이템 뽑기 성공: {item.name}");
                    // 뽑은 아이템을 리스트에 추가
                    drawnItems.Add(item);
                    // 뽑은 아이템을 처리
                    ProcessDrawnItem(item);
                }
                // 뽑은 아이템이 null이라면
                else
                {
                    Debug.LogError("[GachaManager] DrawItem()에서 null을 반환했습니다. GachaTableSO 설정을 확인하세요.");
                }
            }

            Debug.Log($"[GachaManager] 최종적으로 {drawnItems.Count}개의 아이템을 뽑았습니다.");
            return drawnItems;
        }
        
        // 뽑은 아이템을 처리
        private void ProcessDrawnItem(ScriptableObject item)
        {
            // 장비 뽑기를 한 경우 
            if (item is WeaponData weapon)
            {
                inventoryManager.AddItem(weapon);
            }
            // 각인 뽑기를 한 경우 
            else if (item is EngravingData engraving)
            {
                engravingManager.AddNewEngravingToStorage(engraving);
            }
        }
    }
}