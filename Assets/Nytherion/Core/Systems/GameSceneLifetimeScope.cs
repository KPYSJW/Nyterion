using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Core.Test;
using Nytherion.GamePlay;
using Nytherion.GamePlay.Characters.NPC;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Dungeon;
using Nytherion.GamePlay.Relics;
using Nytherion.GamePlay.Systems;
using Nytherion.UI.Controllers;
using Nytherion.UI.RelicBoard;
using Nytherion.UI.Inventory;
using Nytherion.UI.Map;
using Nytherion.UI.Presenters;
using Nytherion.UI.Progression;
using Nytherion.UI.Skill;
using Nytherion.UI.Test;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;
using VContainer.Unity;
using Nytherion.UI;
public class GameSceneLifetimeScope : LifetimeScope
{
    [Header("UI References From Scene")]
    [SerializeField] private GameSceneUIRefs gameSceneUIRefs;

    [Header("GameScene Managers")]
    [SerializeField] private GachaManager gachaManagerPrefab;
    [SerializeField] private GachaUIController gachaUIControllerPrefab;
    [SerializeField] private InteractionManager interactionManagerPrefab;
    [SerializeField] private DungeonManager dungeonManagerPrefab;
    [SerializeField] private QuickSlotManager quickSlotManagerPrefab;
    [SerializeField] private ObjectPoolManager objectPoolManagerPrefab;

    [Header("GameScene UI")]
    [SerializeField] private InventoryUI inventoryUIPrefab;
    [SerializeField] private ShopUI shopUIPrefab;
    [SerializeField] private RelicUIController relicUIControllerPrefab;

    [Header("UI Controllers")]
    [SerializeField] private InventoryUIController inventoryUIControllerPrefab;
    [SerializeField] private CurrencyUIController currencyUIControllerPrefab;
    [SerializeField] private MilestoneUIController milestoneUIControllerPrefab;
    [SerializeField] private SkillUIController skillUIControllerPrefab;

    [Header("GameScene Systems")]
    [SerializeField] private PlayerManager playerManagerPrefab;
    [SerializeField] private MenuManager menuManagerPrefab;
    [SerializeField] private ItemUsageManager itemUsageManagerPrefab;
    [SerializeField] private InventoryManager inventoryManagerPrefab;
    [SerializeField] private CurrencyDataManager currencyManagerPrefab;

    [Header("Debug Systems")]
    [SerializeField] private RelicSystemDebugger relicSystemDebuggerPrefab;

    [Header("GameScene Gameplay")]
    [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private FollowCamera followCameraPrefab;
    [SerializeField] private SettingsManager settingsManagerPrefab;
    [SerializeField] private InventoryPresenter inventoryPresenterPrefab;
    [SerializeField] private GameSceneUIManager gameSceneUIManager;
    [SerializeField] private GlobalUIManager globalUIManagerPrefab;

    [Header("Test Progression")]
    [SerializeField] private ProgressionDebugUI progressionDebugUI;
    [SerializeField] private Nytherion.UI.Test.DebugPanelUI debugPanelUI;

    protected DataLifetimeScope dataLifetimeScope;
    protected bool isDataManagersReady = false;
    protected bool waitForDataManagers = true;
    protected float maxWaitTime = 10f;

    protected override void Awake()
    {
        base.Awake();

        if (waitForDataManagers)
        {
            StartCoroutine(WaitForDataManagers());
        }
    }

    protected override void Configure(IContainerBuilder builder)
    {
        // 부모 scope의 의존성들을 현재 scope에서 사용할 수 있도록 등록
        RegisterParentScopeDependencies(builder);

        // GameSceneUIRefs를 하이라키에서 자동으로 찾아서 등록 (UI 컨트롤러들이 필요로 함)
        RegisterUIReferences(builder);

        // UI 컨트롤러들을 먼저 설치 (NPC들이 의존성을 주입받기 전에)
        InstallGameSceneUI(builder);

        InstallGameSceneManagers(builder);

        InstallGameSceneSystems(builder);

        builder.RegisterEntryPoint<GameSceneInitializer>();

        if (progressionDebugUI != null)
        {
            builder.RegisterComponent(progressionDebugUI);
        }
    }

    /// <summary>
    /// 데이터 매니저들이 준비될 때까지 대기
    /// </summary>
    private IEnumerator WaitForDataManagers()
    {
        float elapsedTime = 0f;

        while (elapsedTime < maxWaitTime)
        {
            dataLifetimeScope = DataLifetimeScope.Instance;

            if (dataLifetimeScope != null && dataLifetimeScope.IsDataManagersReady())
            {
                isDataManagersReady = true;
                OnDataManagersReady();
                yield break;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>
    /// 데이터 매니저들이 준비되었을 때 호출되는 콜백
    /// </summary>
    protected virtual void OnDataManagersReady()
    {
        // GameScene UI 초기화
        InitializeGameSceneUI();
    }

    private void RegisterParentScopeDependencies(IContainerBuilder builder)
    {
        // RootLifetimeScope에서 기본 매니저들 가져오기
        if (Parent != null)
        {
            // InputManager
            if (Parent.Container.TryResolve<InputManager>(out var inputManager))
            {
                builder.RegisterInstance(inputManager);
            }

            // EventManager
            if (Parent.Container.TryResolve<EventManager>(out var eventManager))
            {
                builder.RegisterInstance(eventManager);
            }

            // AudioManager
            if (Parent.Container.TryResolve<AudioManager>(out var audioManager))
            {
                builder.RegisterInstance(audioManager);
            }

            // SceneTransitionManager
            if (Parent.Container.TryResolve<SceneTransitionManager>(out var sceneTransitionManager))
            {
                builder.RegisterInstance(sceneTransitionManager);
            }
        }

        // DataLifetimeScope에서 데이터 매니저들 가져오기
        dataLifetimeScope = DataLifetimeScope.Instance;
        if (dataLifetimeScope != null && dataLifetimeScope.Container != null && dataLifetimeScope.IsDataManagersReady())
        {
            // SaveLoadManager를 GameSceneLifetimeScope에서도 접근 가능하도록 등록
            RegisterDataManagerIfExists<SaveLoadManager>(builder);
            RegisterDataManagerIfExists<CurrencyDataManager>(builder);
            RegisterDataManagerIfExists<InventoryDataManager>(builder);
            RegisterDataManagerIfExists<RelicManager>(builder);
            RegisterDataManagerIfExists<EquipmentDataManager>(builder);
            RegisterDataManagerIfExists<ShopManager>(builder);
            RegisterDataManagerIfExists<StageManager>(builder);
            RegisterDataManagerIfExists<SkillDataManager>(builder);
            RegisterDataManagerIfExists<ProgressionManager>(builder);
        }
    }

    /// <summary>
    /// 데이터 매니저가 존재하면 현재 컨테이너에 등록
    /// </summary>
    private void RegisterDataManagerIfExists<T>(IContainerBuilder builder) where T : class
    {
        if (dataLifetimeScope.Container.TryResolve<T>(out var manager))
        {
            builder.RegisterInstance(manager).AsImplementedInterfaces().AsSelf();
        }
        else
        {
            Debug.LogWarning($"[GameSceneLifetimeScope] {typeof(T).Name}를 찾을 수 없습니다");
        }
    }


    private void RegisterUIReferences(IContainerBuilder builder)
    {
        builder.RegisterComponentInHierarchy<GameSceneUIRefs>()
               .AsSelf()
               .AsImplementedInterfaces();

        if (quickSlotManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(quickSlotManagerPrefab, Lifetime.Singleton)
                   .AsSelf()
                   .AsImplementedInterfaces()
                   .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] QuickSlotManager 프리팹이 할당되지 않았습니다!");
        }
    }

    private void InstallGameSceneManagers(IContainerBuilder builder)
    {
        builder.RegisterComponentInNewPrefab(gachaManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInNewPrefab(interactionManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        /*builder.RegisterComponentInNewPrefab(stageManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();*/

        builder.RegisterComponentInHierarchy<WorldmapController>();
        builder.RegisterComponentInHierarchy<MinimapTileGenerator>();
        builder.RegisterComponentInHierarchy<PortalTileController>();

        builder.RegisterComponentInNewPrefab(dungeonManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        var tilemaps = FindObjectsOfType<Tilemap>(includeInactive: true);
        builder.RegisterInstance<IReadOnlyList<Tilemap>>(tilemaps);

        builder.RegisterComponentInNewPrefab(objectPoolManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        // SaveLoadManager는 DataLifetimeScope에서 관리됨

        InstallUIControllers(builder);

        RegisterISaveableEntities(builder);
    }

    private void InstallGameSceneUI(IContainerBuilder builder)
    {
        builder.RegisterComponentInNewPrefab(gachaUIControllerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInNewPrefab(inventoryUIPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        if (inventoryPresenterPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(inventoryPresenterPrefab, Lifetime.Singleton);
        }

        if (shopUIPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(shopUIPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (relicUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(relicUIControllerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (gameSceneUIManager != null)
        {
            builder.RegisterComponent(gameSceneUIManager).AsImplementedInterfaces().AsSelf();
        }

        if (globalUIManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(globalUIManagerPrefab, Lifetime.Singleton).AsSelf();
        }

        builder.RegisterComponentInHierarchy<CharacterStatsUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        if(skillUIControllerPrefab != null)
            builder.RegisterComponentInNewPrefab(skillUIControllerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<RelicGridUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<RelicTooltip>()
                .AsImplementedInterfaces()
                .AsSelf();

        RegisterRelicSystemDebugger(builder);

        RegisterUIComponents(builder);

        RegisterNPCComponents(builder);

        if (debugPanelUI != null)
        {
            // 하이라키에 이미 배치된 인스턴스를 등록하고 주입함
            builder.RegisterComponent(debugPanelUI)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        else
        {
            // 씬에서 자동으로 찾아서 등록
            builder.RegisterComponentInHierarchy<Nytherion.UI.Test.DebugPanelUI>()
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
    }

    private void RegisterNPCComponents(IContainerBuilder builder)
    {
        // ShopDealer들을 씬에서 찾아서 등록
       /* builder.RegisterComponentInHierarchy<ShopDealer>()
                .AsImplementedInterfaces()
                .AsSelf();

        // GachaNPC들을 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<GachaNPC>()
                .AsImplementedInterfaces()
                .AsSelf();

        // GameManager를 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<GameManager>()
                .AsImplementedInterfaces()
                .AsSelf();

        // RelicAltar를 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<RelicAltar>()
                .AsImplementedInterfaces()
                .AsSelf();*/

        // GameManager를 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<GameManager>()
                .AsImplementedInterfaces()
                .AsSelf();

    }


    private void RegisterUIComponents(IContainerBuilder builder)
    {
        // 인벤토리 UI 슬롯 컴포넌트들을 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<InventorySlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<EquipmentSlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<QuickSlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();
    }

    private void InstallGameSceneSystems(IContainerBuilder builder)
    {
        var playerManagerInScene = FindObjectOfType<PlayerManager>();

        if (playerManagerInScene != null)
        {
            builder.RegisterComponent(playerManagerInScene)
                    .AsImplementedInterfaces()
                    .AsSelf();

            RegisterPlayerSubSystems(builder);
        }
        else if (playerManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(playerManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();

            RegisterPlayerSubSystems(builder);
            Debug.Log("[GameSceneLifetimeScope] 씬에 Player가 없어 프리팹을 생성합니다.");
        }

        if (enemySpawnerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(enemySpawnerPrefab, Lifetime.Singleton);
        }

        var existingFollowCamera = FindObjectOfType<FollowCamera>();
        if (existingFollowCamera != null)
        {
            builder.RegisterComponent(existingFollowCamera);
        }
        else if (followCameraPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(followCameraPrefab, Lifetime.Singleton);
        }
        else
        {
            Debug.LogWarning("[GameSceneLifetimeScope] FollowCamera를 찾을 수 없습니다!");
        }

        if (settingsManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(settingsManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (menuManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(menuManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (itemUsageManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(itemUsageManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (inventoryManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(inventoryManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (currencyManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(currencyManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

    }


    private void InstallUIControllers(IContainerBuilder builder)
    {
        // InventoryUIController 등록
        if (inventoryUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(inventoryUIControllerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        // CurrencyUIController 등록
        if (currencyUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(currencyUIControllerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (milestoneUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(milestoneUIControllerPrefab, Lifetime.Singleton)
            .AsImplementedInterfaces()
            .AsSelf();
        }

    }

    private void RegisterISaveableEntities(IContainerBuilder builder)
    {
        // 모든 ISaveable 엔티티들을 개별적으로 등록하여 SaveLoadManager가 찾을 수 있도록 함
        // 데이터 매니저들은 이미 .As<ISaveable>()로 등록되어 있음

        // QuickSlotManager도 ISaveable로 추가 등록
        builder.Register<ISaveable>(resolver => resolver.Resolve<QuickSlotManager>(), Lifetime.Singleton);
    }

    private void RegisterRelicSystemDebugger(IContainerBuilder builder)
    {
        // 씬에서 먼저 찾아보기
        var existingDebugger = FindObjectOfType<RelicSystemDebugger>();
        if (existingDebugger != null)
        {
            builder.RegisterComponent(existingDebugger)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        // 프리팹이 할당되어 있다면 생성
        else if (relicSystemDebuggerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(relicSystemDebuggerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        else
        {
            Debug.Log("[GameSceneLifetimeScope] RelicSystemDebugger를 건너뜁니다 (씬에 없고 프리팹도 할당되지 않음).");
        }
    }

    private void RegisterPlayerSubSystems(IContainerBuilder builder)
    {
        builder.Register<PlayerHealth>(resolver => resolver.Resolve<PlayerManager>().playerHealth, Lifetime.Singleton);
        builder.Register<PlayerCombat>(resolver => resolver.Resolve<PlayerManager>().PlayerCombat, Lifetime.Singleton);
        builder.Register<PlayerSkillManager>(resolver => resolver.Resolve<PlayerManager>().GetComponent<PlayerSkillManager>(), Lifetime.Singleton);
        builder.Register<PlayerController>(resolver => resolver.Resolve<PlayerManager>().GetComponent<PlayerController>(), Lifetime.Singleton);
    }
    private void InitializeGameSceneUI()
    {
        // 아키텍처 검증 헬퍼 등록 (디버그 빌드에서만)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RegisterArchitectureValidationHelper();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void RegisterArchitectureValidationHelper()
    {
        // 씬에서 ArchitectureValidationHelper 찾기
        var validationHelper = FindObjectOfType<ArchitectureValidationHelper>();
        if (validationHelper != null)
        {
            // VContainer에 등록하여 의존성 주입이 가능하도록 함
            Container.Inject(validationHelper);
        }
    }
#endif

}
