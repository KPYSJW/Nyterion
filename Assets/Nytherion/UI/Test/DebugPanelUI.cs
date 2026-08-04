using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Data;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.UI.Controllers;
using Nytherion.Data.ScriptableObjects.Shop;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Characters.Enemy;

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
        [SerializeField] private ShopData testShopData;

        [Header("Debug Enemy Spawn Config")]
        [SerializeField] private EnemyData meleeEnemyData;
        [SerializeField] private EnemyData rangedEnemyData;
        [SerializeField] private EnemyData hybridEnemyData;
        [SerializeField] private float spawnOffsetRange = 3f;

        // 의존성 주입
        private InventoryDataManager inventoryDataManager;
        private CurrencyDataManager currencyDataManager;
        private SaveLoadManager saveLoadManager;
        private PlayerManager playerManager;
        private ShopManager shopManager;
        private ShopUI shopUI;
        private GachaUIController gachaUIController;
        private RelicUIController relicUIController;
        private RelicManager relicManager;

        [Inject]
        public void Construct(
            InventoryDataManager inventoryDataManager,
            CurrencyDataManager currencyDataManager,
            SaveLoadManager saveLoadManager,
            PlayerManager playerManager,
            ShopManager shopManager,
            ShopUI shopUI,
            GachaUIController gachaUIController,
            RelicUIController relicUIController,
            RelicManager relicManager)
        {
            this.inventoryDataManager = inventoryDataManager;
            this.currencyDataManager = currencyDataManager;
            this.saveLoadManager = saveLoadManager;
            this.playerManager = playerManager;
            this.shopManager = shopManager;
            this.shopUI = shopUI;
            this.gachaUIController = gachaUIController;
            this.relicUIController = relicUIController;
            this.relicManager = relicManager;
        }

        private void Start()
        {
            // Start 시점의 에러 로그는 제거하고 필요할 때 주입 시도
            if (inventoryDataManager == null || currencyDataManager == null)
            {
                TryManualInject();
            }

            // 몬스터 소환 버튼들 런타임 동적 생성
            CreateMonsterSpawnButtons();
        }

        /// <summary>
        /// 의존성 주입이 늦어지거나 누락된 경우 직접 수동으로 주입 시도
        /// </summary>
        private void TryManualInject()
        {
            Nytherion.Core.Systems.DataLifetimeScope dataScope = Nytherion.Core.Systems.DataLifetimeScope.Instance;
            if (dataScope != null)
            {
                if (inventoryDataManager == null) inventoryDataManager = dataScope.GetDataManager<InventoryDataManager>();
                if (currencyDataManager == null) currencyDataManager = dataScope.GetDataManager<CurrencyDataManager>();
                if (saveLoadManager == null) saveLoadManager = dataScope.GetDataManager<SaveLoadManager>();
                if (shopManager == null) shopManager = dataScope.GetDataManager<ShopManager>();
                if (relicManager == null) relicManager = dataScope.GetDataManager<RelicManager>();
                
                // UI 컨트롤러들과 PlayerManager는 보통 GameSceneScope에 있으므로 씬에서 직접 찾음
                if (playerManager == null) playerManager = FindObjectOfType<PlayerManager>();
                if (shopUI == null) shopUI = FindObjectOfType<ShopUI>();
                if (gachaUIController == null) gachaUIController = FindObjectOfType<GachaUIController>();
                if (relicUIController == null) relicUIController = FindObjectOfType<RelicUIController>();

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
         * UI Shortcut (UI 단축 실행) 기능
         * ========================================================== */

        public void OpenTestShop()
        {
            if (shopUI != null && testShopData != null)
            {
                shopUI.OpenShop(testShopData);
                Close(); // 디버그 패널은 닫음
                UpdateStatusText("테스트 상점 열기");
            }
            else
            {
                UpdateStatusText("상점 UI 또는 데이터가 없습니다.");
            }
        }

        public void OpenGachaUI()
        {
            if (gachaUIController != null)
            {
                gachaUIController.Toggle();
                Close();
                UpdateStatusText("가챠 UI 토글");
            }
        }

        public void OpenRelicUI()
        {
            if (relicUIController != null)
            {
                relicUIController.Toggle();
                Close();
                UpdateStatusText("유물 UI 토글");
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

        public void AddAllRelics()
        {
            if (relicManager == null)
            {
                TryManualInject();
            }

            if (relicManager != null)
            {
                relicManager.AddAllRelicsToStorage();
                UpdateStatusText("모든 유물 획득 완료");
            }
            else
            {
                UpdateStatusText("RelicManager를 찾을 수 없습니다.");
            }
        }

        private void UpdateStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = $"[DEBUG] {message}";
            }
        }

        /* ==========================================================
         * Monster Spawn (몬스터 소환) 관련 기능
         * ========================================================== */

        private void CreateMonsterSpawnButtons()
        {
            if (contentPanel == null) return;

            // contentPanel 하위에서 Button 컴포넌트를 가진 자식을 템플릿으로 확보
            Button templateButton = contentPanel.GetComponentInChildren<Button>();
            if (templateButton == null)
            {
                Debug.LogWarning("[DebugPanelUI] 복제 템플릿으로 쓸 버튼을 찾을 수 없습니다.");
                return;
            }

            Transform parentTransform = templateButton.transform.parent;

            // 근접 몬스터 소환 버튼 생성
            CreateSingleSpawnButton(templateButton, parentTransform, "몬스터 (근접)", SpawnEnemyMelee);
            // 원거리 몬스터 소환 버튼 생성
            CreateSingleSpawnButton(templateButton, parentTransform, "몬스터 (원거리)", SpawnEnemyRanged);
            // 하이브리드 몬스터 소환 버튼 생성
            CreateSingleSpawnButton(templateButton, parentTransform, "몬스터 (하이브리드)", SpawnEnemyHybrid);
        }

        private void CreateSingleSpawnButton(Button template, Transform parent, string label, UnityEngine.Events.UnityAction action)
        {
            Button newButton = Instantiate(template, parent);
            newButton.name = $"SpawnButton_{label}";

            // 버튼의 텍스트(TMP_Text) 변경
            TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>();
            if (buttonText != null)
            {
                buttonText.text = label;
            }
            else
            {
                // 일반 Text 컴포넌트인 경우 대응
                Text legacyText = newButton.GetComponentInChildren<Text>();
                if (legacyText != null)
                {
                    legacyText.text = label;
                }
            }

            // 이벤트 재바인딩
            newButton.onClick.RemoveAllListeners();
            newButton.onClick.AddListener(action);
        }

        public void SpawnEnemyMelee()
        {
            SpawnEnemy(meleeEnemyData);
        }

        public void SpawnEnemyRanged()
        {
            SpawnEnemy(rangedEnemyData);
        }

        public void SpawnEnemyHybrid()
        {
            SpawnEnemy(hybridEnemyData);
        }

        private void SpawnEnemy(EnemyData enemyData)
        {
            if (enemyData == null)
            {
                UpdateStatusText("소환할 몬스터 데이터가 없습니다.");
                return;
            }

            if (playerManager == null)
            {
                // 수동 재주입 시도
                playerManager = FindObjectOfType<PlayerManager>();
                if (playerManager == null)
                {
                    UpdateStatusText("플레이어를 찾을 수 없습니다.");
                    return;
                }
            }

            if (ObjectPoolManager.Instance == null)
            {
                UpdateStatusText("ObjectPoolManager를 찾을 수 없습니다.");
                return;
            }

            // 플레이어 주변 랜덤 위치 계산 (2m ~ spawnOffsetRange 사이)
            Vector2 randomDirection = Random.insideUnitCircle.normalized * Random.Range(2f, spawnOffsetRange);
            Vector3 spawnPosition = playerManager.transform.position + new Vector3(randomDirection.x, randomDirection.y, 0f);

            GameObject enemyObj = ObjectPoolManager.Instance.SpawnFromPool(
                enemyData.enemyName,
                spawnPosition,
                Quaternion.identity);

            if (enemyObj != null)
            {
                EnemyBase enemy;
                if (enemyObj.TryGetComponent<EnemyBase>(out enemy))
                {
                    enemy.Initialize(enemyData);
                    UpdateStatusText($"몬스터 소환 성공: {enemyData.enemyName}");
                }
                else
                {
                    ObjectPoolManager.Instance.ReturnToPool(enemyData.enemyName, enemyObj);
                    UpdateStatusText("소환된 오브젝트에 EnemyBase가 없습니다.");
                }
            }
            else
            {
                UpdateStatusText($"몬스터 풀에서 소환 실패: {enemyData.enemyName}");
            }
        }
    }
}