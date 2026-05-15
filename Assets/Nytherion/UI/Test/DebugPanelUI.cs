using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Characters.Player;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Nytherion.UI.Test
{
    /// <summary>
    /// 게임 개발 및 테스트를 위한 치트/디버그 패널
    /// </summary>
    public class DebugPanelUI : UIPanelBase
    {
        [Header("Debug UI References")]
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private TMP_Text statusText;
        
        [Header("Test Assets")]
        [SerializeField] private ItemData testWeapon;
        [SerializeField] private ItemData testPotion;

        // 의존성 주입
        private InventoryDataManager inventoryDataManager;
        private CurrencyDataManager currencyDataManager;
        private SaveLoadManager saveLoadManager;
        private PlayerManager playerManager;
        private ShopManager shopManager;

        [Inject]
        public void Construct(
            InventoryDataManager inventoryDataManager,
            CurrencyDataManager currencyDataManager,
            SaveLoadManager saveLoadManager,
            PlayerManager playerManager,
            ShopManager shopManager)
        {
            this.inventoryDataManager = inventoryDataManager;
            this.currencyDataManager = currencyDataManager;
            this.saveLoadManager = saveLoadManager;
            this.playerManager = playerManager;
            this.shopManager = shopManager;
            Debug.Log("[DebugPanelUI] Dependencies Injected Successfully.");
        }

        private void Start()
        {
            // Start 시점의 에러 로그는 제거하고 필요할 때 주입 시도
            if (inventoryDataManager == null || currencyDataManager == null)
            {
                TryManualInject();
            }
        }

        /// <summary>
        /// 의존성 주입이 늦어지거나 누락된 경우 직접 수동으로 주입 시도
        /// </summary>
        private void TryManualInject()
        {
            var dataScope = Nytherion.Core.Systems.DataLifetimeScope.Instance;
            if (dataScope != null)
            {
                if (inventoryDataManager == null) inventoryDataManager = dataScope.GetDataManager<InventoryDataManager>();
                if (currencyDataManager == null) currencyDataManager = dataScope.GetDataManager<CurrencyDataManager>();
                if (saveLoadManager == null) saveLoadManager = dataScope.GetDataManager<SaveLoadManager>();
                if (shopManager == null) shopManager = dataScope.GetDataManager<ShopManager>();
                
                // PlayerManager는 보통 GameSceneScope에 있으므로 씬에서 직접 찾음
                if (playerManager == null) playerManager = FindObjectOfType<PlayerManager>();

                if (inventoryDataManager != null) Debug.Log("[DebugPanelUI] Manually Injected Dependencies.");
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (contentPanel != null) contentPanel.SetActive(false);
        }

        private void Update()
        {
            // F12 키로 디버그 패널 토글
            if (Input.GetKeyDown(KeyCode.F12))
            {
                Toggle();
            }
        }

        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (contentPanel != null) contentPanel.SetActive(isOpen);
            
            if (isOpen)
            {
                UpdateStatusText("디버그 패널 활성화됨");
                // 패널이 열릴 때 게임 시간 정지 (선택 사항)
                // Time.timeScale = 0f;
            }
            else
            {
                // Time.timeScale = 1f;
            }
        }

        /* ==========================================================
         * Economy (재화) 관련 기능
         * ========================================================== */
        
        public void AddGold(int amount)
        {
            if (currencyDataManager != null)
            {
                currencyDataManager.AddCurrency(CurrencyType.Gold, amount);
                UpdateStatusText($"{amount} 골드 추가됨");
            }
        }

        public void AddToken(int amount)
        {
            if (currencyDataManager != null)
            {
                currencyDataManager.AddCurrency(CurrencyType.Token, amount);
                UpdateStatusText($"{amount} 토큰 추가됨");
            }
        }

        /* ==========================================================
         * Inventory (인벤토리) 관련 기능
         * ========================================================== */

        public void AddTestItem()
        {
            if (inventoryDataManager != null && testWeapon != null)
            {
                inventoryDataManager.AddItem(testWeapon, 1);
                UpdateStatusText($"테스트 아이템({testWeapon.itemName}) 추가됨");
            }
        }

        public void AddTestPotion(int count)
        {
            if (inventoryDataManager != null && testPotion != null)
            {
                inventoryDataManager.AddItem(testPotion, count);
                UpdateStatusText($"테스트 포션({testPotion.itemName}) {count}개 추가됨");
            }
        }

        public void ClearInventory()
        {
            if (inventoryDataManager != null)
            {
                // InventoryModel에 접근하여 클리어 (또는 Manager에 메서드 추가 필요)
                // 현재는 Manager에 Clear 메서드가 없으므로 나중에 추가하거나 루프로 삭제
                var allItems = inventoryDataManager.GetAllItems();
                foreach(var item in allItems)
                {
                    // 수량만큼 제거
                    inventoryDataManager.RemoveItem(item.item.ID, item.count);
                }
                UpdateStatusText("인벤토리 비우기 완료");
            }
        }

        /* ==========================================================
         * Player (플레이어) 관련 기능
         * ========================================================== */

        public void HealFull()
        {
            if (playerManager != null && playerManager.playerHealth != null)
            {
                playerManager.playerHealth.Heal(9999);
                UpdateStatusText("플레이어 체력 완전 회복");
            }
        }

        public void ToggleGodMode()
        {
            if (playerManager != null && playerManager.playerHealth != null)
            {
                bool isInvulnerable = !playerManager.playerHealth.IsInvulnerable;
                playerManager.playerHealth.SetInvulnerable(isInvulnerable);
                UpdateStatusText($"무적 모드: {(isInvulnerable ? "ON" : "OFF")}");
            }
        }

        /* ==========================================================
         * System (시스템) 관련 기능
         * ========================================================== */

        public void SaveGame()
        {
            if (saveLoadManager != null)
            {
                saveLoadManager.SaveGame();
                UpdateStatusText("게임 강제 저장 완료");
            }
        }

        public void LoadGame()
        {
            if (saveLoadManager != null)
            {
                saveLoadManager.ForceLoadGame();
                UpdateStatusText("게임 강제 불러오기 완료");
            }
        }

        public void RestartScene()
        {
            UpdateStatusText("씬 재시작 중...");
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void UpdateStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = $"[DEBUG] {message}";
                Debug.Log($"[DebugPanel] {message}");
            }
        }
    }
}