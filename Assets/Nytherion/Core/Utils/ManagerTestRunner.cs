using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using System.Collections.Generic;

namespace Nytherion.Core.Utils
{
    /// <summary>
    /// 리팩토링된 매니저들의 기본 기능을 테스트하는 유틸리티 클래스
    /// Unity 에디터에서 Inspector를 통해 테스트할 수 있습니다.
    /// </summary>
    public class ManagerTestRunner : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool runTestsOnStart = false;
        [SerializeField] private bool enableDetailedLogging = true;

        [Header("Manager References")]
        [SerializeField] private CurrencyManager currencyManager;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private ShopManager shopManager;

        private void Start()
        {
            if (runTestsOnStart)
            {
                RunAllTests();
            }
        }

        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            Debug.Log("=== Starting Manager Tests ===");
            
            TestBaseManagerFunctionality();
            TestCurrencyManager();
            TestInventoryManager();
            TestShopManager();
            TestSaveLoadFunctionality();
            
            Debug.Log("=== Manager Tests Completed ===");
        }

        [ContextMenu("Test Base Manager")]
        public void TestBaseManagerFunctionality()
        {
            LogTest("Testing BaseManager functionality...");
            
            var managers = new BaseManager[] { currencyManager, inventoryManager, shopManager };
            
            foreach (var manager in managers)
            {
                if (manager == null) continue;
                
                string statusBefore = manager.GetStatusInfo();
                LogDetail($"Manager Status Before: {statusBefore}");
                
                if (!manager.IsInitialized)
                {
                    manager.Initialize();
                }
                
                string statusAfter = manager.GetStatusInfo();
                LogDetail($"Manager Status After: {statusAfter}");
                
                // 활성화/비활성화 테스트
                manager.SetActive(false);
                LogDetail($"Manager Active State: {manager.IsActive}");
                
                manager.SetActive(true);
                LogDetail($"Manager Active State: {manager.IsActive}");
            }
            
            LogTest("BaseManager functionality test completed.");
        }

        [ContextMenu("Test Currency Manager")]
        public void TestCurrencyManager()
        {
            if (currencyManager == null)
            {
                LogError("CurrencyManager is null!");
                return;
            }

            LogTest("Testing CurrencyManager...");
            
            // 초기 상태 확인
            int initialGold = currencyManager.GetCurrency(CurrencyType.Gold);
            int initialToken = currencyManager.GetCurrency(CurrencyType.Token);
            LogDetail($"Initial Gold: {initialGold}, Token: {initialToken}");
            
            // 통화 추가 테스트
            currencyManager.AddCurrency(CurrencyType.Gold, 100);
            currencyManager.AddCurrency(CurrencyType.Token, 10);
            
            int goldAfterAdd = currencyManager.GetCurrency(CurrencyType.Gold);
            int tokenAfterAdd = currencyManager.GetCurrency(CurrencyType.Token);
            LogDetail($"After adding - Gold: {goldAfterAdd}, Token: {tokenAfterAdd}");
            
            // 통화 소비 테스트
            bool spendResult1 = currencyManager.SpendCurrency(CurrencyType.Gold, 50);
            bool spendResult2 = currencyManager.SpendCurrency(CurrencyType.Token, 5);
            LogDetail($"Spend results - Gold: {spendResult1}, Token: {spendResult2}");
            
            // 잔액 확인
            int finalGold = currencyManager.GetCurrency(CurrencyType.Gold);
            int finalToken = currencyManager.GetCurrency(CurrencyType.Token);
            LogDetail($"Final balances - Gold: {finalGold}, Token: {finalToken}");
            
            // 잔액 부족 테스트
            bool insufficientTest = currencyManager.SpendCurrency(CurrencyType.Gold, 10000);
            LogDetail($"Insufficient funds test result: {insufficientTest}");
            
            LogTest("CurrencyManager test completed.");
        }

        [ContextMenu("Test Inventory Manager")]
        public void TestInventoryManager()
        {
            if (inventoryManager == null)
            {
                LogError("InventoryManager is null!");
                return;
            }

            LogTest("Testing InventoryManager...");
            
            // 초기 상태 확인
            bool isFullBefore = inventoryManager.IsFull;
            int emptySlotsBefore = inventoryManager.GetEmptySlotCount();
            LogDetail($"Initial state - Full: {isFullBefore}, Empty slots: {emptySlotsBefore}");
            
            // 인벤토리 초기화 확인
            if (!inventoryManager.IsInitialized)
            {
                inventoryManager.Initialize();
            }
            
            // 상태 정보 출력
            string statusInfo = inventoryManager.GetStatusInfo();
            LogDetail($"Inventory status: {statusInfo}");
            
            LogTest("InventoryManager test completed.");
        }

        [ContextMenu("Test Shop Manager")]
        public void TestShopManager()
        {
            if (shopManager == null)
            {
                LogError("ShopManager is null!");
                return;
            }

            LogTest("Testing ShopManager...");
            
            // 초기화 확인
            if (!shopManager.IsInitialized)
            {
                shopManager.Initialize();
            }
            
            // 상점 목록 확인
            var shopNames = shopManager.GetAllShopNames();
            LogDetail($"Available shops: {string.Join(", ", shopNames)}");
            
            // 상태 정보 출력
            string statusInfo = shopManager.GetStatusInfo();
            LogDetail($"Shop status: {statusInfo}");
            
            // 상점 존재 여부 테스트
            foreach (var shopName in shopNames)
            {
                bool exists = shopManager.ShopExists(shopName);
                LogDetail($"Shop '{shopName}' exists: {exists}");
            }
            
            LogTest("ShopManager test completed.");
        }

        [ContextMenu("Test Save/Load")]
        public void TestSaveLoadFunctionality()
        {
            LogTest("Testing Save/Load functionality...");
            
            var testSaveData = new SaveData();
            
            // 각 매니저의 저장 기능 테스트
            if (currencyManager != null)
            {
                currencyManager.PopulateSaveData(testSaveData);
                LogDetail("CurrencyManager save data populated");
            }
            
            if (inventoryManager != null)
            {
                inventoryManager.PopulateSaveData(testSaveData);
                LogDetail("InventoryManager save data populated");
            }
            
            if (shopManager != null)
            {
                shopManager.PopulateSaveData(testSaveData);
                LogDetail("ShopManager save data populated");
            }
            
            // 로드 기능 테스트 (실제로는 현재 상태를 다시 로드)
            if (currencyManager != null)
            {
                currencyManager.LoadFromSaveData(testSaveData);
                LogDetail("CurrencyManager data loaded");
            }
            
            if (inventoryManager != null)
            {
                inventoryManager.LoadFromSaveData(testSaveData);
                LogDetail("InventoryManager data loaded");
            }
            
            if (shopManager != null)
            {
                shopManager.LoadFromSaveData(testSaveData);
                LogDetail("ShopManager data loaded");
            }
            
            LogTest("Save/Load functionality test completed.");
        }

        private void LogTest(string message)
        {
            Debug.Log($"<color=cyan>[ManagerTest]</color> {message}");
        }

        private void LogDetail(string message)
        {
            if (enableDetailedLogging)
            {
                Debug.Log($"<color=yellow>[ManagerTest]</color> {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"<color=red>[ManagerTest]</color> {message}");
        }

        [ContextMenu("Show All Manager Status")]
        public void ShowAllManagerStatus()
        {
            Debug.Log("=== Manager Status Report ===");
            
            if (currencyManager != null)
                Debug.Log($"Currency: {currencyManager.GetStatusInfo()}");
            else
                Debug.LogWarning("CurrencyManager is null");
                
            if (inventoryManager != null)
                Debug.Log($"Inventory: {inventoryManager.GetStatusInfo()}");
            else
                Debug.LogWarning("InventoryManager is null");
                
            if (shopManager != null)
                Debug.Log($"Shop: {shopManager.GetStatusInfo()}");
            else
                Debug.LogWarning("ShopManager is null");
        }
    }
}