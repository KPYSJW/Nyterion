using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VContainer;
using VContainer.Unity;
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
using Nytherion.Core.Interfaces;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Gameplay.Relics.Modules;
using System.Linq;

namespace Nytherion.UI.Test
{
    /// <summary>
    /// 게임 개발 및 테스트를 위한 치트/디버그 패널
    /// </summary>
    public class DebugPanelUI : UIPanelBase
    {
        [Header("Debug UI References")]
        [SerializeField] private GameObject contentPanel;
        [SerializeField] private GameObject contentPanel2;
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

        [Header("허수아비 소환")]
        [SerializeField] private TrainingDummy trainingDummyPrefab;
        [SerializeField, Min(0.5f)] private float trainingDummySpawnDistance = 1.5f;
        private readonly List<TrainingDummy> trainingDummies = new List<TrainingDummy>();
        private Vector2 trainingDummySpawnDirection = Vector2.right;
        private InputManager inputManager;
        private IObjectResolver objectResolver;

        // 의존성 주입
        private InventoryDataManager inventoryDataManager;
        private CurrencyDataManager currencyDataManager;
        private SaveLoadManager saveLoadManager;
        private PlayerManager playerManager;
        private ShopManager shopManager;
        private ShopUI shopUI;
        private RelicUIController relicUIController;
        private RelicManager relicManager;
        private SkillDataManager skillDataManager;
        private ILocalizationService localizationService;
        private Button languageToggleButton;
        private readonly List<StatModifier> debugStatModifiers = new List<StatModifier>();
        private readonly Dictionary<string, GameObject> categoryPages = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, Button> categoryButtons = new Dictionary<string, Button>();
        private readonly Dictionary<Button, (RelicSetBonusData set, int requirement, ColorBlock colors)> setEffectButtons =
            new Dictionary<Button, (RelicSetBonusData, int, ColorBlock)>();
        private readonly Dictionary<TMP_Text, RelicSetBonusData> setNameLabels = new Dictionary<TMP_Text, RelicSetBonusData>();
        private readonly Dictionary<Button, RelicTranscendenceData> transcendenceEffectButtons =
            new Dictionary<Button, RelicTranscendenceData>();
        private readonly Dictionary<Button, (EquipmentTrait trait, string label)> attackTraitButtons =
            new Dictionary<Button, (EquipmentTrait, string)>();
        private Button debugButtonTemplate;
        private bool effectButtonsCreated;
        private string selectedCategory = "플레이어";

        // 비율 능력치는 고정값으로 더한다. 예: 0.05는 5%p 증가입니다.
        private static readonly (StatType stat, string label, float amount, bool ratio)[] debugStats =
        {
            (StatType.MaxHealth, "최대 체력", 10f, false),
            (StatType.Defense, "방어력", 1f, false),
            (StatType.MoveSpeed, "이동 속도", 0.5f, false),
            (StatType.MeleeDamage, "근접 공격력", 5f, false),
            (StatType.RangedDamage, "원거리 공격력", 5f, false),
            (StatType.MeleeSpeed, "근접 공격 속도", 0.1f, false),
            (StatType.RangedSpeed, "원거리 공격 속도", 0.1f, false),
            (StatType.DashSpeed, "대시 속도", 0.5f, false),
            (StatType.DashDuration, "대시 지속 시간", 0.1f, false),
            (StatType.DashCooldown, "대시 쿨타임", 0.1f, false),
            (StatType.ExtraProjectiles, "추가 투사체 수", 1f, false),
            (StatType.Lifesteal, "생명력 흡수", 0.05f, true),
            (StatType.ChargeTimeReduction, "충전 시간 감소", 0.05f, true),
            (StatType.CritChance, "치명타 확률", 0.05f, true),
            (StatType.CritDamage, "치명타 피해량", 0.1f, true),
            (StatType.ProjectileSize, "효과 범위 증가", 0.1f, false)
        };

        [Inject]
        public void Construct(
            InventoryDataManager inventoryDataManager,
            CurrencyDataManager currencyDataManager,
            SaveLoadManager saveLoadManager,
            PlayerManager playerManager,
            ShopManager shopManager,
            ShopUI shopUI,
            RelicUIController relicUIController,
            RelicManager relicManager,
            SkillDataManager skillDataManager,
            ILocalizationService localizationService,
            InputManager inputManager,
            IObjectResolver objectResolver)
        {
            this.inventoryDataManager = inventoryDataManager;
            this.currencyDataManager = currencyDataManager;
            this.saveLoadManager = saveLoadManager;
            this.playerManager = playerManager;
            this.shopManager = shopManager;
            this.shopUI = shopUI;
            this.relicUIController = relicUIController;
            this.relicManager = relicManager;
            this.skillDataManager = skillDataManager;
            this.localizationService = localizationService;
            this.inputManager = inputManager;
            this.objectResolver = objectResolver;
        }

        private void Start()
        {
            // Start 시점의 에러 로그는 제거하고 필요할 때 주입 시도
            if (inventoryDataManager == null || currencyDataManager == null)
            {
                TryManualInject();
            }

            // 디버그용 장비/스킬 지급, 언어 전환 및 몬스터 소환 버튼을 런타임에 생성
            CreateRuntimeDebugButtons();
            CreateRuntimeStatButtons();
            CreateCategoryNavigation();

            if (localizationService != null)
            {
                localizationService.LanguageChanged += OnLanguageChanged;
            }
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
                if (skillDataManager == null) skillDataManager = dataScope.GetDataManager<SkillDataManager>();
                
                // UI 컨트롤러들과 PlayerManager는 보통 GameSceneScope에 있으므로 씬에서 직접 찾음
                if (playerManager == null) playerManager = FindObjectOfType<PlayerManager>();
                if (shopUI == null) shopUI = FindObjectOfType<ShopUI>();
                if (relicUIController == null) relicUIController = FindObjectOfType<RelicUIController>();

                if (inventoryDataManager != null) Debug.Log("[DebugPanelUI] Manually Injected Dependencies.");
            }
        }

        protected override void Awake()
        {
            base.Awake();
            if (contentPanel != null) contentPanel.SetActive(false);
            if (contentPanel2 != null) contentPanel2.SetActive(false);
        }

        private void Update()
        {
            // 패널을 연 뒤 버튼을 클릭해도 열기 직전의 조준 방향 앞에 소환합니다.
            if (!IsOpen && playerManager != null && inputManager != null && Camera.main != null)
            {
                Vector3 mousePosition = inputManager.MousePosition;
                mousePosition.z = Mathf.Abs(Camera.main.transform.position.z - playerManager.transform.position.z);
                Vector2 aimDirection = Camera.main.ScreenToWorldPoint(mousePosition) - playerManager.transform.position;
                if (aimDirection.sqrMagnitude > 0.0001f)
                    trainingDummySpawnDirection = aimDirection.normalized;
            }

            // F12 키로 디버그 패널 토글
            if (Input.GetKeyDown(KeyCode.F12))
            {
                Toggle();
            }
        }

        private void OnDestroy()
        {
            ResetDebugStats();
            GetRelicEffectController()?.ResetDebugEffects();
            if (playerManager != null) playerManager.PlayerCombat?.ResetDebugAttackTraits();
            if (localizationService != null)
            {
                localizationService.LanguageChanged -= OnLanguageChanged;
            }
        }

        private void LateUpdate()
        {
            if (IsOpen) RefreshCategoryGridLayouts();
        }

        protected override void OnPanelStateChanged(bool isOpen)
        {
            if (contentPanel != null) contentPanel.SetActive(isOpen);
            if (contentPanel2 != null) contentPanel2.SetActive(isOpen);
            
            if (isOpen)
            {
                CreateRelicEffectButtons();
                RefreshEffectButtonLabels();
                RefreshAttackTraitButtonLabels();
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

        public void AddAllEquipment()
        {
            if (inventoryDataManager == null)
            {
                TryManualInject();
            }

            if (inventoryDataManager == null || !inventoryDataManager.IsInitialized)
            {
                UpdateStatusText("인벤토리가 아직 준비되지 않았습니다.");
                return;
            }

            var equipmentAssets = new List<EquipmentData>();
            foreach (ItemData item in ItemDatabase.GetAllItems())
            {
                if (item is EquipmentData equipment)
                {
                    if (equipment is WeaponData weapon && !weapon.IsRuntimeAvailable)
                    {
                        continue;
                    }

                    equipmentAssets.Add(equipment);
                }
            }

            if (equipmentAssets.Count == 0)
            {
                UpdateStatusText("아이템 데이터베이스에 등록된 장비가 없습니다.");
                return;
            }

            // 기존 아이템을 보존하면서 모든 장비가 들어갈 공간을 확보한다.
            int requiredSlots = inventoryDataManager.MaxSlotCount
                - inventoryDataManager.GetEmptySlotCount() + equipmentAssets.Count;
            inventoryDataManager.EnsureSlotCapacity(requiredSlots);

            int addedCount = 0;
            foreach (EquipmentData equipmentAsset in equipmentAssets)
            {
                EquipmentData instance = Instantiate(equipmentAsset);
                instance.instanceId = System.Guid.NewGuid().ToString();
                instance.ApplyRarityStats(Rarity.Common);
                if (inventoryDataManager.AddEquipmentInstance(instance))
                {
                    addedCount++;
                }
                else
                {
                    Destroy(instance);
                }
            }

            UpdateStatusText($"일반 등급 장비 {addedCount}/{equipmentAssets.Count}개 지급 완료");
        }

        /// <summary>
        /// 루티 포탑 스킬을 기존 획득 경로로 보관함에 지급합니다.
        /// </summary>
        public void AcquireRootiTurretSkill()
        {
            if (skillDataManager == null)
            {
                TryManualInject();
            }

            if (skillDataManager == null || skillDataManager.skillDatabase == null)
            {
                UpdateStatusText("스킬 매니저 또는 데이터베이스가 준비되지 않았습니다.");
                return;
            }

            SkillData skill = skillDataManager.skillDatabase.GetSkillById("skill_turret");
            if (skill == null)
            {
                UpdateStatusText("데이터베이스에서 루티 포탑 스킬을 찾을 수 없습니다.");
                return;
            }

            // 반복 클릭으로 레벨이나 경험치가 변경되지 않도록 중복 지급을 막습니다.
            if (skillDataManager.skillStates.ContainsKey(skill.skillID))
            {
                UpdateStatusText("루티 포탑 스킬을 이미 보유하고 있습니다.");
                return;
            }

            if (skillDataManager.storageSkills == null ||
                System.Array.IndexOf(skillDataManager.storageSkills, null) < 0)
            {
                UpdateStatusText("스킬 보관함이 가득 찼습니다. 빈 슬롯을 확보해 주세요.");
                return;
            }

            skillDataManager.AcquireSkill(skill);
            UpdateStatusText("루티 포탑 획득 완료! 스킬 UI에서 장착해 주세요.");
        }

        /// <summary>
        /// 데이터베이스의 모든 스킬 중 아직 보유하지 않은 스킬을 보관함에 지급합니다.
        /// </summary>
        public void AddAllSkills()
        {
            if (skillDataManager == null)
            {
                TryManualInject();
            }

            if (skillDataManager == null || skillDataManager.skillDatabase == null)
            {
                UpdateStatusText("스킬 매니저 또는 데이터베이스가 준비되지 않았습니다.");
                return;
            }

            List<SkillData> allSkills = skillDataManager.skillDatabase.allSkills;
            if (allSkills == null || allSkills.Count == 0)
            {
                UpdateStatusText("스킬 데이터베이스에 등록된 스킬이 없습니다.");
                return;
            }

            var pendingSkills = new List<SkillData>();
            var skillIds = new HashSet<string>();
            foreach (SkillData skill in allSkills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.skillID) || !skillIds.Add(skill.skillID))
                {
                    continue;
                }

                // 장착 중인 스킬도 포함해 중복 지급으로 레벨이나 경험치가 바뀌지 않도록 한다.
                if (!skillDataManager.skillStates.ContainsKey(skill.skillID))
                {
                    pendingSkills.Add(skill);
                }
            }

            if (pendingSkills.Count == 0)
            {
                UpdateStatusText(skillIds.Count == 0
                    ? "스킬 데이터베이스에 유효한 스킬이 없습니다."
                    : "모든 스킬을 이미 보유하고 있습니다.");
                return;
            }

            int occupiedSlots = 0;
            if (skillDataManager.storageSkills != null)
            {
                foreach (SkillData skill in skillDataManager.storageSkills)
                {
                    if (skill != null) occupiedSlots++;
                }
            }

            // 기존 스킬을 보존하면서 전체 지급에 필요한 보관함 공간을 확보한다.
            skillDataManager.EnsureStorageCapacity(occupiedSlots + pendingSkills.Count);
            foreach (SkillData skill in pendingSkills)
            {
                skillDataManager.AcquireSkill(skill);
            }

            UpdateStatusText($"모든 스킬 획득 완료! 새 스킬 {pendingSkills.Count}개 지급. 스킬 UI에서 장착해 주세요.");
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

        private void CreateRuntimeDebugButtons()
        {
            if (contentPanel == null) return;

            Transform buttonsContainer = contentPanel.transform.Find("Buttons");

            // 패널은 시작 시 비활성 상태이므로 비활성 자식까지 포함해 템플릿을 찾는다.
            Button templateButton = buttonsContainer != null
                ? buttonsContainer.GetComponentInChildren<Button>(true)
                : contentPanel.GetComponentInChildren<Button>(true);
            if (templateButton == null)
            {
                Debug.LogWarning("[DebugPanelUI] 복제 템플릿으로 쓸 버튼을 찾을 수 없습니다.");
                return;
            }

            Transform parentTransform = buttonsContainer != null
                ? buttonsContainer
                : templateButton.transform.parent;

            CreateRuntimeButton(templateButton, parentTransform,
                "AddAllEquipmentButton", "모든 장비 (일반)", AddAllEquipment);

            CreateRuntimeButton(templateButton, parentTransform,
                "AcquireRootiTurretSkillButton", "루티 포탑 획득", AcquireRootiTurretSkill);

            CreateRuntimeButton(templateButton, parentTransform,
                "AddAllSkillsButton", "모든 스킬 획득", AddAllSkills);

            languageToggleButton = CreateRuntimeButton(
                templateButton,
                parentTransform,
                "LanguageToggleButton",
                GetLanguageButtonLabel(),
                ToggleLanguage);

            // 근접 몬스터 소환 버튼 생성
            CreateRuntimeButton(templateButton, parentTransform, "SpawnButton_Melee", "몬스터 (근접)", SpawnEnemyMelee);
            // 원거리 몬스터 소환 버튼 생성
            CreateRuntimeButton(templateButton, parentTransform, "SpawnButton_Ranged", "몬스터 (원거리)", SpawnEnemyRanged);
            // 하이브리드 몬스터 소환 버튼 생성
            CreateRuntimeButton(templateButton, parentTransform, "SpawnButton_Hybrid", "몬스터 (하이브리드)", SpawnEnemyHybrid);
            // 소환/제거 버튼은 작은 해상도에서도 바로 누를 수 있도록 앞쪽에 배치합니다.
            CreateRuntimeButton(templateButton, parentTransform, "SpawnTrainingDummyButton", "허수아비 소환", SpawnTrainingDummy)
                .transform.SetSiblingIndex(1);
            CreateRuntimeButton(templateButton, parentTransform, "RemoveTrainingDummiesButton", "허수아비 제거", RemoveTrainingDummies)
                .transform.SetSiblingIndex(2);
        }

        private Button CreateRuntimeButton(
            Button template,
            Transform parent,
            string objectName,
            string label,
            UnityEngine.Events.UnityAction action)
        {
            Button newButton = Instantiate(template, parent);
            newButton.name = objectName;

            // 버튼의 텍스트(TMP_Text) 변경
            TMP_Text buttonText = newButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null)
            {
                buttonText.text = label;
            }
            else
            {
                // 일반 Text 컴포넌트인 경우 대응
                Text legacyText = newButton.GetComponentInChildren<Text>(true);
                if (legacyText != null)
                {
                    legacyText.text = label;
                }
            }

            // 이벤트 재바인딩
            // 복제된 버튼의 Inspector 영구 이벤트까지 제거한다.
            newButton.onClick = new Button.ButtonClickedEvent();
            newButton.onClick.AddListener(action);
            newButton.gameObject.SetActive(true);
            return newButton;
        }

        private void CreateCategoryNavigation()
        {
            if (contentPanel == null || contentPanel2 == null || categoryPages.Count > 0) return;
            Button[] existingButtons = contentPanel.GetComponentsInChildren<Button>(true);
            Button[] statButtons = contentPanel2.GetComponentsInChildren<Button>(true);
            if (existingButtons.Length == 0) return;
            debugButtonTemplate = existingButtons[0];

            ConfigurePanelRect(contentPanel, new Vector2(0.02f, 0.1f), new Vector2(0.24f, 0.9f));
            ConfigurePanelRect(contentPanel2, new Vector2(0.26f, 0.1f), new Vector2(0.98f, 0.9f));
            RectTransform navigation = CreateRect("Categories", contentPanel.transform);
            StretchRect(navigation, new Vector2(12f, 12f), new Vector2(-12f, -12f));
            VerticalLayoutGroup layout = navigation.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout);

            foreach (string category in new[] { "플레이어", "능력치", "공격 속성", "재화", "장비·아이템", "스킬", "유물", "세트 효과", "초월 효과", "소환", "시스템" })
            {
                categoryPages.Add(category, CreateCategoryPage(category));
                Button button = CreateRuntimeButton(debugButtonTemplate, navigation,
                    "Category_" + category, category, () => SelectCategory(category));
                ConfigureStatButton(button);
                categoryButtons.Add(category, button);
            }

            // 씬에 연결된 영구 이벤트는 그대로 두고 버튼의 부모만 옮긴다.
            foreach (Button button in existingButtons)
            {
                MoveToCategory(button, GetButtonCategory(button));
            }
            foreach (Button button in statButtons) MoveToCategory(button, "능력치");
            Transform oldButtons = contentPanel.transform.Find("Buttons");
            if (oldButtons != null) oldButtons.gameObject.SetActive(false);
            oldButtons = contentPanel2.transform.Find("Buttons");
            if (oldButtons != null) oldButtons.gameObject.SetActive(false);

            if (statusText != null)
            {
                statusText.transform.SetParent(contentPanel2.transform, false);
                StretchRect(statusText.rectTransform, new Vector2(12f, 8f), new Vector2(-12f, 66f));
                statusText.rectTransform.anchorMax = new Vector2(1f, 0f);
                statusText.alignment = TextAlignmentOptions.MidlineLeft;
                statusText.enableAutoSizing = true;
                statusText.fontSizeMin = 14f;
                statusText.fontSizeMax = 22f;
                statusText.raycastTarget = false;
            }
            CreateRelicEffectButtons();
            CreateAttackTraitButtons();
            SelectCategory(selectedCategory);
        }

        private static void StretchRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static void ConfigurePanelRect(GameObject panel, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = (RectTransform)panel.transform;
            StretchRect(rect, Vector2.zero, Vector2.zero);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            Image background = panel.GetComponent<Image>();
            if (background == null) background = panel.AddComponent<Image>();
            background.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
        }

        private static void ConfigureVerticalLayout(VerticalLayoutGroup layout)
        {
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private GameObject CreateCategoryPage(string category)
        {
            RectTransform page = CreateRect("Page_" + category, contentPanel2.transform);
            StretchRect(page, new Vector2(12f, 80f), new Vector2(-12f, -12f));
            ScrollRect scroll = page.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = CreateRect("Viewport", page);
            StretchRect(viewport, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image raycastArea = viewport.gameObject.AddComponent<Image>();
            raycastArea.color = Color.clear;
            RectTransform buttons = CreateRect("Buttons", viewport);
            buttons.anchorMin = new Vector2(0f, 1f);
            buttons.anchorMax = Vector2.one;
            buttons.pivot = new Vector2(0.5f, 1f);
            buttons.sizeDelta = Vector2.zero;
            GridLayoutGroup grid = buttons.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(8f, 8f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(240f, category == "세트 효과" ? 80f : 44f);
            ContentSizeFitter fitter = buttons.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = buttons;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            page.gameObject.SetActive(false);
            return page.gameObject;
        }

        private void RefreshCategoryGridLayouts()
        {
            foreach (var entry in categoryPages)
            {
                if (!entry.Value.activeSelf) continue;
                RectTransform content = entry.Value.GetComponent<ScrollRect>().content;
                GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
                float width = content.rect.width - grid.padding.horizontal;
                if (width <= 0f) continue;
                int columns = Mathf.Clamp(Mathf.FloorToInt((width + grid.spacing.x) / (240f + grid.spacing.x)), 1, 4);
                Vector2 cellSize = new Vector2((width - grid.spacing.x * (columns - 1)) / columns, grid.cellSize.y);
                if (grid.constraintCount != columns) grid.constraintCount = columns;
                if (grid.cellSize != cellSize) grid.cellSize = cellSize;
            }
        }

        private void MoveToCategory(Button button, string category)
        {
            button.transform.SetParent(categoryPages[category].GetComponent<ScrollRect>().content, false);
            ConfigureStatButton(button);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 24f;
            }
        }

        private string GetButtonCategory(Button button)
        {
            string method = button.onClick.GetPersistentEventCount() > 0
                ? button.onClick.GetPersistentMethodName(0) : button.name;
            if (method.Contains("Gold") || method.Contains("Token")) return "재화";
            if (method.Contains("Relic")) return "유물";
            if (method.Contains("Skill")) return "스킬";
            if (method.Contains("Spawn") || method.Contains("TrainingDumm")) return "소환";
            if (method.Contains("Item") || method.Contains("Equipment") || method.Contains("Potion") || method.Contains("Inventory")) return "장비·아이템";
            if (method.Contains("Heal") || method.Contains("GodMode")) return "플레이어";
            return "시스템";
        }

        public void SelectCategory(string category)
        {
            if (!categoryPages.ContainsKey(category)) return;
            selectedCategory = category;
            foreach (var entry in categoryPages) entry.Value.SetActive(entry.Key == category);
            foreach (var entry in categoryButtons) entry.Value.interactable = entry.Key != category;
            RefreshCategoryGridLayouts();
            RefreshEffectButtonLabels();
            RefreshAttackTraitButtonLabels();
            UpdateStatusText(category == "세트 효과" || category == "초월 효과"
                ? "유물 배치 조건을 무시하고 임시 적용합니다. 다시 누르면 해제됩니다. 목록은 휠로 스크롤할 수 있습니다."
                : category == "공격 속성"
                ? "무기 공격에 속성을 추가합니다. 여러 속성을 함께 켤 수 있으며, 다시 누르면 해제됩니다."
                : $"{category} · 목록은 마우스 휠로 스크롤할 수 있습니다.");
        }

        private Button AddCategoryButton(string category, string objectName, string label, UnityEngine.Events.UnityAction action)
        {
            Button button = CreateRuntimeButton(debugButtonTemplate,
                categoryPages[category].GetComponent<ScrollRect>().content, objectName, label, action);
            MoveToCategory(button, category);
            return button;
        }

        private RelicEffectController GetRelicEffectController() =>
            playerManager != null ? playerManager.GetComponent<RelicEffectController>() : null;

        private void CreateAttackTraitButtons()
        {
            var traits = new[]
            {
                (EquipmentTrait.Fire, "화염"),
                (EquipmentTrait.Ice, "빙결"),
                (EquipmentTrait.Lightning, "전격"),
                (EquipmentTrait.Poison, "독"),
                (EquipmentTrait.Curse, "저주"),
                (EquipmentTrait.Holy, "신성"),
                (EquipmentTrait.Demonic, "마성")
            };
            foreach (var entry in traits)
            {
                Button button = AddCategoryButton("공격 속성", "DebugAttackTrait_" + entry.Item1, "",
                    () => ToggleDebugAttackTrait(entry.Item1));
                attackTraitButtons.Add(button, entry);
            }
            AddCategoryButton("공격 속성", "ResetDebugAttackTraitsButton", "테스트 공격 속성 초기화", ResetDebugAttackTraits);
            RefreshAttackTraitButtonLabels();
        }

        public void ToggleDebugAttackTrait(EquipmentTrait trait)
        {
            PlayerCombat combat = playerManager != null ? playerManager.PlayerCombat : null;
            if (combat == null)
            {
                UpdateStatusText("플레이어 전투 기능이 아직 준비되지 않았습니다.");
                return;
            }
            bool active = !combat.IsDebugAttackTraitActive(trait);
            combat.SetDebugAttackTrait(trait, active);
            RefreshAttackTraitButtonLabels();
            foreach (var entry in attackTraitButtons.Values)
            {
                if (entry.trait != trait) continue;
                UpdateStatusText($"공격 {entry.label} 속성 테스트 {(active ? "활성화" : "해제")} · 다음 무기 공격부터 적용됩니다.");
                break;
            }
        }

        public void ResetDebugAttackTraits()
        {
            if (playerManager != null) playerManager.PlayerCombat?.ResetDebugAttackTraits();
            RefreshAttackTraitButtonLabels();
            UpdateStatusText("테스트 공격 속성을 초기화했습니다. 무기 고유 속성은 유지됩니다.");
        }

        private void RefreshAttackTraitButtonLabels()
        {
            PlayerCombat combat = playerManager != null ? playerManager.PlayerCombat : null;
            foreach (var entry in attackTraitButtons)
            {
                bool active = combat != null && combat.IsDebugAttackTraitActive(entry.Value.trait);
                SetButtonLabel(entry.Key, $"[{(active ? "ON" : "OFF")}] {entry.Value.label}");
            }
        }

        private void CreateRelicEffectButtons()
        {
            if (effectButtonsCreated || debugButtonTemplate == null || !categoryPages.ContainsKey("세트 효과")) return;
            if (relicManager == null) TryManualInject();
            if (relicManager == null) return;
            List<RelicSetBonusData> sets = relicManager.GetAvailableSetBonuses().ToList();
            if (sets.Count == 0) return;
            effectButtonsCreated = true;

            AddCategoryButton("세트 효과", "ResetDebugSetEffectsButton", "세트·초월 초기화", ResetDebugRelicEffects);
            AddCategoryButton("초월 효과", "ResetDebugTranscendencesButton", "세트·초월 초기화", ResetDebugRelicEffects);
            foreach (RelicSetBonusData set in sets)
            {
                if (set.bonusModules == null) continue;
                var requirements = set.bonusModules
                    .Where(module => module?.condition is ChainSynergyCondition)
                    .Select(module => ((ChainSynergyCondition)module.condition).requiredChainLength)
                    .Where(count => count > 0).Distinct().OrderBy(count => count).ToList();
                if (requirements.Count == 0) continue;
                RectTransform tierRow = CreateSetEffectGroup(set);
                foreach (int requirement in requirements)
                {
                    Button button = CreateRuntimeButton(debugButtonTemplate, tierRow, $"DebugSet_{set.name}_{requirement}",
                        requirement.ToString(),
                        () => ToggleDebugSetBonus(set, requirement));
                    ConfigureStatButton(button);
                    setEffectButtons.Add(button, (set, requirement, button.colors));
                }
            }
            foreach (RelicTranscendenceData transcendence in sets
                .Where(set => set.linkedTranscendenceEffects != null)
                .SelectMany(set => set.linkedTranscendenceEffects).Where(effect => effect != null)
                .Distinct().OrderBy(effect => effect.name))
            {
                Button button = AddCategoryButton("초월 효과", "DebugTranscendence_" + transcendence.name, "",
                    () => ToggleDebugTranscendence(transcendence));
                transcendenceEffectButtons.Add(button, transcendence);
            }
            RefreshEffectButtonLabels();
        }

        private RectTransform CreateSetEffectGroup(RelicSetBonusData set)
        {
            Transform parent = categoryPages["세트 효과"].GetComponent<ScrollRect>().content;
            RectTransform group = CreateRect("DebugSetGroup_" + set.name, parent);
            Image background = group.gameObject.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.06f);
            background.raycastTarget = false;
            VerticalLayoutGroup layout = group.gameObject.AddComponent<VerticalLayoutGroup>();
            ConfigureVerticalLayout(layout);
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 4f;

            TMP_Text title = CreateRect("SetName", group).gameObject.AddComponent<TextMeshProUGUI>();
            TMP_Text templateLabel = debugButtonTemplate.GetComponentInChildren<TMP_Text>(true);
            if (templateLabel != null)
            {
                title.font = templateLabel.font;
                title.fontSharedMaterial = templateLabel.fontSharedMaterial;
            }
            title.text = set.DisplayName;
            title.color = Color.white;
            title.alignment = TextAlignmentOptions.Center;
            title.enableAutoSizing = true;
            title.fontSizeMin = 14f;
            title.fontSizeMax = 22f;
            title.raycastTarget = false;
            LayoutElement titleSize = title.gameObject.AddComponent<LayoutElement>();
            titleSize.minHeight = 24f;
            titleSize.preferredHeight = 24f;
            titleSize.flexibleHeight = 0f;
            setNameLabels.Add(title, set);

            RectTransform row = CreateRect("Tiers", group);
            HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 4f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;
            LayoutElement rowSize = row.gameObject.AddComponent<LayoutElement>();
            rowSize.minHeight = 32f;
            rowSize.preferredHeight = 40f;
            rowSize.flexibleHeight = 1f;
            return row;
        }

        public void ToggleDebugSetBonus(RelicSetBonusData set, int requirement)
        {
            RelicEffectController controller = GetRelicEffectController();
            if (controller == null || playerManager.currentPlayerData == null)
            {
                UpdateStatusText("플레이어 유물 효과가 아직 준비되지 않았습니다.");
                return;
            }
            if (set == null || requirement <= 0) return;
            bool activate = controller.GetDebugSetRequirement(set) != requirement;
            controller.SetDebugSetBonus(set, activate ? requirement : 0);
            RefreshEffectButtonLabels();
            string criticalNotice = activate && set.synergySeriesId == "StormArcherSeries" ? " · 치명타 100%" : "";
            UpdateStatusText($"{set.DisplayName} {requirement}세트 테스트 {(activate ? "활성화" : "해제")}{criticalNotice} (보드·저장에는 반영되지 않음)");
        }

        public void ToggleDebugTranscendence(RelicTranscendenceData transcendence)
        {
            RelicEffectController controller = GetRelicEffectController();
            if (controller == null || playerManager.currentPlayerData == null)
            {
                UpdateStatusText("플레이어 유물 효과가 아직 준비되지 않았습니다.");
                return;
            }
            if (transcendence == null) return;
            bool activate = !controller.IsDebugTranscendenceActive(transcendence);
            controller.SetDebugTranscendence(transcendence, activate);
            RefreshEffectButtonLabels();
            UpdateStatusText($"{transcendence.DisplayName} 테스트 {(activate ? "활성화" : "해제")} (보드·저장에는 반영되지 않음)");
        }

        public void ResetDebugRelicEffects()
        {
            GetRelicEffectController()?.ResetDebugEffects();
            RefreshEffectButtonLabels();
            UpdateStatusText("세트·초월 테스트 효과를 해제했습니다. 실제 장착 효과는 유지됩니다.");
        }

        private void RefreshEffectButtonLabels()
        {
            RelicEffectController controller = GetRelicEffectController();
            foreach (var entry in setNameLabels) entry.Key.text = entry.Value.DisplayName;
            foreach (var entry in setEffectButtons)
            {
                bool active = controller != null && controller.GetDebugSetRequirement(entry.Value.set) == entry.Value.requirement;
                ColorBlock colors = entry.Value.colors;
                if (active)
                {
                    colors.normalColor = new Color(0.35f, 0.9f, 0.55f);
                    colors.highlightedColor = new Color(0.5f, 1f, 0.7f);
                    colors.selectedColor = colors.normalColor;
                }
                entry.Key.colors = colors;
            }
            foreach (var entry in transcendenceEffectButtons)
                SetButtonLabel(entry.Key, $"[{(controller != null && controller.IsDebugTranscendenceActive(entry.Value) ? "ON" : "OFF")}] {entry.Value.DisplayName}");
        }

        private static void SetButtonLabel(Button button, string label)
        {
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;
            else
            {
                Text legacyText = button.GetComponentInChildren<Text>(true);
                if (legacyText != null) legacyText.text = label;
            }
        }

        private void CreateRuntimeStatButtons()
        {
            if (contentPanel2 == null) return;

            Button template = contentPanel != null
                ? contentPanel.GetComponentInChildren<Button>(true)
                : null;
            if (template == null)
            {
                Debug.LogWarning("[DebugPanelUI] 능력치 버튼의 복제 템플릿이 없습니다.", this);
                return;
            }

            Transform container = contentPanel2.transform.Find("Buttons");
            if (container == null)
            {
                GameObject buttons = new GameObject("Buttons", typeof(RectTransform));
                buttons.layer = contentPanel2.layer;
                buttons.transform.SetParent(contentPanel2.transform, false);
                container = buttons.transform;
            }

            // 능력치 버튼이 부모 패널의 공간을 사용할 수 있도록 컨테이너를 확장한다.
            RectTransform rect = (RectTransform)container;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 12f);
            rect.offsetMax = new Vector2(-12f, -12f);

            // Inspector에서 지정한 Grid 등 기존 레이아웃을 유지하고, 없을 때만 세로 배치를 추가한다.
            LayoutGroup layout = container.GetComponent<LayoutGroup>();
            if (layout == null) layout = container.gameObject.AddComponent<VerticalLayoutGroup>();
            if (layout is VerticalLayoutGroup verticalLayout)
            {
                verticalLayout.padding = new RectOffset(0, 0, 0, 0);
                verticalLayout.spacing = 6f;
                verticalLayout.childAlignment = TextAnchor.UpperCenter;
                verticalLayout.childControlWidth = true;
                verticalLayout.childControlHeight = true;
                verticalLayout.childForceExpandWidth = true;
                verticalLayout.childForceExpandHeight = false;
            }

            foreach (var entry in debugStats)
            {
                string increase = entry.ratio ? $"{entry.amount * 100f:0.##}%p" : $"{entry.amount:0.##}";
                Button button = CreateRuntimeButton(template, container, $"IncreaseStat_{entry.stat}",
                    $"{entry.label} +{increase}", () => IncreaseDebugStat(entry.stat, entry.amount));
                ConfigureStatButton(button);
            }

            ConfigureStatButton(CreateRuntimeButton(template, container,
                "ResetDebugStatsButton", "테스트 능력치 초기화", ResetDebugStats));
        }

        private void ConfigureStatButton(Button button)
        {
            LayoutElement element = button.GetComponent<LayoutElement>();
            if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = false;
            element.minHeight = 32f;
            element.preferredHeight = 40f;
            element.flexibleHeight = 0f;
            button.gameObject.SetActive(true);
        }

        public void IncreaseDebugStat(StatType stat, float amount)
        {
            if (playerManager == null || playerManager.currentPlayerData == null)
            {
                UpdateStatusText("플레이어 능력치가 아직 준비되지 않았습니다.");
                return;
            }

            foreach (var entry in debugStats)
            {
                if (entry.stat != stat) continue;
                if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;

                // 원본 데이터 수정 없이 기존 능력치 재계산 및 UI 갱신 경로를 사용한다.
                StatModifier modifier = new StatModifier { stat = stat, value = amount, isPercentage = false };
                debugStatModifiers.Add(modifier);
                playerManager.AddTemporaryStatModifier(modifier);
                string current = entry.ratio
                    ? $"{GetDebugStatValue(stat) * 100f:0.##}%"
                    : $"{GetDebugStatValue(stat):0.##}";
                UpdateStatusText($"{entry.label} 증가 적용 → 현재 {current}");
                return;
            }
        }

        public void ResetDebugStats()
        {
            if (playerManager != null && playerManager.currentPlayerData != null)
            {
                foreach (StatModifier modifier in debugStatModifiers)
                    playerManager.RemoveTemporaryStatModifier(modifier);
            }
            debugStatModifiers.Clear();
            UpdateStatusText("테스트로 추가한 능력치를 초기화했습니다.");
        }

        private float GetDebugStatValue(StatType stat)
        {
            var data = playerManager.currentPlayerData;
            return stat switch
            {
                StatType.MaxHealth => data.maxHealth,
                StatType.Defense => data.defense,
                StatType.MoveSpeed => data.moveSpeed,
                StatType.MeleeDamage => data.meleeDamage,
                StatType.RangedDamage => data.rangedDamage,
                StatType.MeleeSpeed => data.meleeSpeed,
                StatType.RangedSpeed => data.rangedSpeed,
                StatType.DashSpeed => data.dashSpeed,
                StatType.DashDuration => data.dashDuration,
                StatType.DashCooldown => data.dashCooldown,
                StatType.ExtraProjectiles => data.extraProjectiles,
                StatType.Lifesteal => data.lifesteal,
                StatType.ChargeTimeReduction => data.chargeTimeReduction,
                StatType.CritChance => data.critChance,
                StatType.CritDamage => data.critDamageMultiplier,
                StatType.ProjectileSize => data.projectileSizeMultiplier,
                _ => 0f
            };
        }

        public void ToggleLanguage()
        {
            if (localizationService == null)
            {
                UpdateStatusText("LocalizationService를 찾을 수 없습니다.");
                return;
            }

            SupportedLanguage nextLanguage = localizationService.CurrentLanguage == SupportedLanguage.Korean
                ? SupportedLanguage.English
                : SupportedLanguage.Korean;

            localizationService.SetLanguage(nextLanguage);
            UpdateLanguageButtonLabel();
            UpdateStatusText(nextLanguage == SupportedLanguage.Korean
                ? "임시 언어: 한국어"
                : "Temporary language: English");
        }

        private void OnLanguageChanged(SupportedLanguage _)
        {
            UpdateLanguageButtonLabel();
            RefreshEffectButtonLabels();
        }

        private void UpdateLanguageButtonLabel()
        {
            if (languageToggleButton == null)
            {
                return;
            }

            TMP_Text buttonText = languageToggleButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonText != null)
            {
                buttonText.text = GetLanguageButtonLabel();
                return;
            }

            Text legacyText = languageToggleButton.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.text = GetLanguageButtonLabel();
            }
        }

        private string GetLanguageButtonLabel()
        {
            return localizationService != null &&
                   localizationService.CurrentLanguage == SupportedLanguage.English
                ? "Language: English → 한국어"
                : "언어: 한국어 → English";
        }

        public void SpawnTrainingDummy()
        {
            if (playerManager == null || objectResolver == null)
            {
                UpdateStatusText("플레이어 또는 소환 의존성이 준비되지 않았습니다.");
                return;
            }
            if (trainingDummyPrefab == null || trainingDummyPrefab.enemyData == null)
            {
                UpdateStatusText("허수아비 프리팹 또는 적 데이터가 없습니다.");
                return;
            }

            Vector3 position = playerManager.transform.position
                + (Vector3)(trainingDummySpawnDirection * trainingDummySpawnDistance);
            TrainingDummy dummy = objectResolver.Instantiate(trainingDummyPrefab, position, Quaternion.identity);
            dummy.Initialize(trainingDummyPrefab.enemyData);
            trainingDummies.RemoveAll(target => target == null);
            trainingDummies.Add(dummy);
            UpdateStatusText($"플레이어 앞에 허수아비 소환 완료 ({trainingDummies.Count}개)");
        }

        public void RemoveTrainingDummies()
        {
            foreach (TrainingDummy dummy in trainingDummies)
            {
                if (dummy != null) Destroy(dummy.gameObject);
            }
            trainingDummies.Clear();
            UpdateStatusText("소환한 허수아비를 모두 제거했습니다.");
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
