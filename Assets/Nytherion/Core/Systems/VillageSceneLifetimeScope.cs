using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Core.Test;
using Nytherion.GamePlay;
using Nytherion.GamePlay.Characters.NPC;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Dungeon;
using Nytherion.GamePlay.Engravings;
using Nytherion.GamePlay.Systems;
using Nytherion.UI.Controllers;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Inventory;
using Nytherion.UI.Map;
using Nytherion.UI.Presenters;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer;
using VContainer.Unity;

public class VillageSceneLifetimeScope : LifetimeScope
{
    [Header("UI References From Scene")]
    [SerializeField] private GameSceneUIRefs gameSceneUIRefs;

    [Header("GameScene Only Managers")]
    [SerializeField] private GachaManager gachaManagerPrefab;
    [SerializeField] private GachaUIController gachaUIControllerPrefab;
    [SerializeField] private InteractionManager interactionManagerPrefab;
    //[SerializeField] private DungeonManager dungeonManagerPrefab;
    [SerializeField] private QuickSlotManager quickSlotManagerPrefab;
   // [SerializeField] private ObjectPoolManager objectPoolManagerPrefab;

    [Header("GameScene Only UI")]
    [SerializeField] private InventoryUI inventoryUIPrefab;
    [SerializeField] private ShopUI shopUIPrefab;
    [SerializeField] private EngravingUIController engravingUIControllerPrefab;

    [Header("UI Controllers - 데이터와 UI 분리")]
    [SerializeField] private InventoryUIController inventoryUIControllerPrefab;
    [SerializeField] private CurrencyUIController currencyUIControllerPrefab;

    [Header("GameScene Only Systems")]
    [SerializeField] private PlayerManager playerManagerPrefab;
    [SerializeField] private MenuManager menuManagerPrefab;
    [SerializeField] private ItemUsageManager itemUsageManagerPrefab;
    [SerializeField] private InventoryManager inventoryManagerPrefab;
    [SerializeField] private CurrencyDataManager currencyManagerPrefab;

    [Header("Debug Systems")]
    [SerializeField] private EngravingSystemDebugger engravingSystemDebuggerPrefab;

    [Header("GameScene Only Gameplay")]
   // [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private FollowCamera followCameraPrefab;
    [SerializeField] private SettingsManager settingsManagerPrefab;
    [SerializeField] private InventoryPresenter inventoryPresenterPrefab;
    [SerializeField] private GameSceneUIManager gameSceneUIManager;

    // DataLifetimeScope 대기를 위한 상태 변수
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

        builder.RegisterComponentInHierarchy<VillagePortal>()
               .AsImplementedInterfaces()
               .AsSelf();

        // GameSceneUIRefs를 하이라키에서 자동으로 찾아서 등록 (UI 컨트롤러들이 필요로 함)
        RegisterUIReferences(builder);

        // UI 컨트롤러들을 먼저 설치 (NPC들이 의존성을 주입받기 전에)
        InstallGameSceneOnlyUI(builder);

        InstallGameSceneOnlyManagers(builder);

        InstallGameSceneOnlySystems(builder);

       
        // builder.RegisterEntryPoint<GameSceneInitializer>();
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
            RegisterDataManagerIfExists<EngravingManager>(builder);
            RegisterDataManagerIfExists<EquipmentDataManager>(builder);
            RegisterDataManagerIfExists<ShopManager>(builder);
            RegisterDataManagerIfExists<StageManager>(builder);

            // RegisterDataManagerIfExists<PuzzleManager>(builder); // 나중에 사용 예정
            // PlayerManager는 GameScene에서만 필요하므로 여기서 직접 관리
        }
    }

    /// <summary>
    /// 데이터 매니저가 존재하면 현재 컨테이너에 등록
    /// </summary>
    private void RegisterDataManagerIfExists<T>(IContainerBuilder builder) where T : class
    {
        if (dataLifetimeScope.Container.TryResolve<T>(out var manager))
        {
            builder.RegisterInstance(manager);
        }
        else
        {
            Debug.LogWarning($"[GameSceneLifetimeScope] {typeof(T).Name}를 찾을 수 없습니다");
        }
    }


    private void RegisterUIReferences(IContainerBuilder builder)
    {
        // GameSceneUIRefs 등록
        builder.RegisterComponentInHierarchy<GameSceneUIRefs>()
               .AsSelf()
               .AsImplementedInterfaces();

        // QuickSlotManager 등록 (GameSceneUIRefs 이후에 등록되어야 함)
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

    private void InstallGameSceneOnlyManagers(IContainerBuilder builder)
    {
        builder.RegisterComponentInNewPrefab(gachaManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces() // 인터페이스를 통해 접근 가능
                .AsSelf(); // 자기 자신을 통해 접근 가능

        builder.RegisterComponentInNewPrefab(interactionManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        /*builder.RegisterComponentInNewPrefab(stageManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();*/

        builder.RegisterComponentInHierarchy<WorldmapController>();
        builder.RegisterComponentInHierarchy<MinimapTileGenerator>();
        builder.RegisterComponentInHierarchy<PortalTileController>();

     

        var tilemaps = FindObjectsOfType<Tilemap>(includeInactive: true);
        builder.RegisterInstance<IReadOnlyList<Tilemap>>(tilemaps);

    

        // SaveLoadManager는 DataLifetimeScope에서 관리됨

        // UI 컨트롤러들 등록 (데이터와 UI 분리)
        InstallUIControllers(builder);

        // ISaveable 인터페이스들을 추가 등록하여 SaveLoadManager가 찾을 수 있도록 함
        RegisterISaveableEntities(builder);
    }

    private void InstallGameSceneOnlyUI(IContainerBuilder builder)
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

        if (engravingUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(engravingUIControllerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        if (gameSceneUIManager != null)
        {
            builder.RegisterComponent(gameSceneUIManager).AsImplementedInterfaces().AsSelf();
        }

        builder.RegisterComponentInHierarchy<CharacterStatsUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        // EngravingGridUI를 IInitializable로 등록하여 자동 초기화
        builder.RegisterComponentInHierarchy<EngravingGridUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        // EngravingTooltip을 싱글톤으로 등록
        builder.RegisterComponentInHierarchy<Nytherion.UI.EngravingBoard.EngravingTooltip>()
                .AsImplementedInterfaces()
                .AsSelf();

        // 각인 시스템 디버거 등록
        RegisterEngravingSystemDebugger(builder);

        // 인벤토리 UI 슬롯들을 등록 (의존성 주입을 위해)
        RegisterInventoryUIComponents(builder);

        // UI 컨트롤러들이 등록된 후에 NPC들을 등록 (의존성 주입 순서 보장)
        RegisterNPCComponents(builder);

    }

    private void RegisterNPCComponents(IContainerBuilder builder)
    {
        // ShopDealer들을 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<ShopDealer>()
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

        // EngravingAltar를 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<Nytherion.GamePlay.Characters.NPC.EngravingAltar>()
                .AsImplementedInterfaces()
                .AsSelf();

    }


    private void RegisterInventoryUIComponents(IContainerBuilder builder)
    {
        // 인벤토리 UI 슬롯 컴포넌트들을 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<Nytherion.UI.Inventory.InventorySlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<Nytherion.UI.Inventory.EquipmentSlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<Nytherion.UI.Inventory.QuickSlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        // QuickSlotManager는 이미 RegisterUIReferences에서 등록됨
    }

    private void InstallGameSceneOnlySystems(IContainerBuilder builder)
    {
        // Player 시스템
        if (playerManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(playerManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();

            builder.Register<PlayerHealth>(resolver => resolver.Resolve<PlayerManager>().playerHealth, Lifetime.Singleton);
            builder.Register<PlayerCombat>(resolver => resolver.Resolve<PlayerManager>().PlayerCombat, Lifetime.Singleton);
            builder.Register<PlayerSkillManager>(resolver => resolver.Resolve<PlayerManager>().GetComponent<PlayerSkillManager>(), Lifetime.Singleton);
            builder.Register<PlayerController>(resolver => resolver.Resolve<PlayerManager>().GetComponent<PlayerController>(), Lifetime.Singleton);
        }

  

        // 씬에 있는 FollowCamera 등록 (Main Camera에 붙어있는 경우)
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
    }

    private void RegisterISaveableEntities(IContainerBuilder builder)
    {
        // 모든 ISaveable 엔티티들을 개별적으로 등록하여 SaveLoadManager가 찾을 수 있도록 함
        // 데이터 매니저들은 이미 .As<ISaveable>()로 등록되어 있음

        // QuickSlotManager도 ISaveable로 추가 등록
        builder.Register<ISaveable>(resolver => resolver.Resolve<QuickSlotManager>(), Lifetime.Singleton);
    }

    private void RegisterEngravingSystemDebugger(IContainerBuilder builder)
    {
        // 씬에서 먼저 찾아보기
        var existingDebugger = FindObjectOfType<EngravingSystemDebugger>();
        if (existingDebugger != null)
        {
            builder.RegisterComponent(existingDebugger)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        // 프리팹이 할당되어 있다면 생성
        else if (engravingSystemDebuggerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(engravingSystemDebuggerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        else
        {
            Debug.Log("[GameSceneLifetimeScope] EngravingSystemDebugger를 건너뜁니다 (씬에 없고 프리팹도 할당되지 않음).");
        }
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


