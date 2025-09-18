using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Characters.NPC;
using Nytherion.UI.EngravingBoard;
using Nytherion.UI.Presenters;
using Nytherion.GamePlay.Systems;
using Nytherion.GamePlay;
using Nytherion.UI.Controllers;
using Nytherion.UI.Shop;
using Nytherion.UI.Inventory;
using Nytherion.Core.Interfaces;
using Nytherion.GamePlay.Dungeon;
using Nytherion.GamePlay.Engravings;

public class GameSceneLifetimeScope : LifetimeScope
{
    [Header("UI References From Scene")]
    [SerializeField] private GameSceneUIRefs gameSceneUIRefs;
    
    [Header("GameScene Only Managers")]
    [SerializeField] private GachaManager gachaManagerPrefab;
    [SerializeField] private GachaUIController gachaUIControllerPrefab;
    [SerializeField] private InteractionManager interactionManagerPrefab;
    [SerializeField] private StageManager stageManagerPrefab;
    [SerializeField] private DungeonManager dungeonManagerPrefab;
    [SerializeField] private QuickSlotManager quickSlotManagerPrefab;
    [SerializeField] private ObjectPoolManager objectPoolManagerPrefab;

    [Header("Data Managers - SaveLoadManager와 함께 관리")]
    [SerializeField] private SaveLoadManager saveLoadManagerPrefab;
    [SerializeField] private CurrencyManager currencyManagerPrefab;
    [SerializeField] private InventoryManager inventoryManagerPrefab;
    [SerializeField] private EngravingManager engravingManagerPrefab;
    [SerializeField] private EquipmentDataManager equipmentDataManagerPrefab;
    [SerializeField] private ShopManager shopManagerPrefab;
    [SerializeField] private PuzzleManager puzzleManagerPrefab;

    [Header("GameScene Only UI")]
    [SerializeField] private InventoryUI inventoryUIPrefab;
    [SerializeField] private ShopUI shopUIPrefab;
    [SerializeField] private EngravingUIController engravingUIControllerPrefab;
    [SerializeField] private PuzzleUIController puzzleUIControllerPrefab;

    [Header("GameScene Only Systems")]
    [SerializeField] private PlayerManager playerManagerPrefab;
    [SerializeField] private MenuManager menuManagerPrefab;
    [SerializeField] private ItemUsageManager itemUsageManagerPrefab;

    [Header("Debug Systems")]
    [SerializeField] private EngravingSystemDebugger engravingSystemDebuggerPrefab;

    [Header("GameScene Only Gameplay")]
    [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private FollowCamera followCameraPrefab;
    [SerializeField] private SettingsManager settingsManagerPrefab;
    [SerializeField] private InventoryPresenter inventoryPresenterPrefab;
    [SerializeField] private GameSceneUIManager gameSceneUIManager;

    
    protected override void Configure(IContainerBuilder builder)
    {
        // 부모 scope의 의존성들을 현재 scope에서 사용할 수 있도록 등록
        RegisterParentScopeDependencies(builder);

        // GameSceneUIRefs를 하이라키에서 자동으로 찾아서 등록 (UI 컨트롤러들이 필요로 함)
        RegisterUIReferences(builder);

        // UI 컨트롤러들을 먼저 설치 (NPC들이 의존성을 주입받기 전에)
        InstallGameSceneOnlyUI(builder);

        InstallGameSceneOnlyManagers(builder);

        InstallGameSceneOnlySystems(builder);


        //InstallUIFromHierarchy(builder);
     }

    private void RegisterParentScopeDependencies(IContainerBuilder builder)
    {
        // 부모 scope에서 InputManager 가져와서 현재 scope에 등록
        if (Parent != null && Parent.Container.TryResolve<InputManager>(out var inputManager))
        {
            builder.RegisterInstance(inputManager);
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] 부모 scope에서 InputManager를 찾을 수 없습니다!");
        }
        
        // EventManager도 부모에서 가져오기
        if (Parent != null && Parent.Container.TryResolve<EventManager>(out var eventManager))
        {
            builder.RegisterInstance(eventManager);
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] 부모 scope에서 EventManager를 찾을 수 없습니다!");
        }
        
        // SaveLoadManager는 이제 현재 scope에서 직접 관리됨
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

        builder.RegisterComponentInNewPrefab(stageManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInNewPrefab(dungeonManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();


        builder.RegisterComponentInNewPrefab(objectPoolManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces()
                .AsSelf();

        // SaveLoadManager 등록 (데이터 매니저들과 같은 scope에서 관리)
        if (saveLoadManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(saveLoadManagerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] SaveLoadManager 프리팹이 할당되지 않았습니다!");
        }

        // 데이터 매니저들 등록
        InstallDataManagers(builder);

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

        if (puzzleUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(puzzleUIControllerPrefab, Lifetime.Singleton)
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
        builder.RegisterComponentInHierarchy<EngravingTooltip>()
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
        builder.RegisterComponentInHierarchy<EngravingAltar>()
                .AsImplementedInterfaces()
                .AsSelf();

        // PuzzleNPC를 씬에서 찾아서 등록
        builder.RegisterComponentInHierarchy<PuzzleNPC>()
                .AsImplementedInterfaces()
                .AsSelf();

    }


    private void RegisterInventoryUIComponents(IContainerBuilder builder)
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

        // Gameplay 시스템들
        if (enemySpawnerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(enemySpawnerPrefab, Lifetime.Singleton);
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
    }

    // private void InstallUIFromHierarchy(IContainerBuilder builder)
    // { 
    //      if (inventoryCanvasGroup != null)
    //         {
    //             builder.RegisterInstance(inventoryCanvasGroup);
    //         }
            
    //         if (equipmentPanel != null)
    //             builder.RegisterInstance(equipmentPanel);

    //         if (statsPanel != null)
    //             builder.RegisterInstance(statsPanel);

    //         if (inventorySlotParent != null)
    //         {
    //             if (shopPlayerInventoryParent == null)
    //             {
    //                 shopPlayerInventoryParent = inventorySlotParent;
    //             }
    //             builder.RegisterInstance(inventorySlotParent);
    //         }

    //         if (inventoryCloseButton != null)
    //             builder.RegisterInstance(inventoryCloseButton);

    //         if (engravingGridUI != null)
    //             builder.RegisterInstance(engravingGridUI);

    //         if (engravingTooltip != null)
    //             builder.RegisterInstance(engravingTooltip);


    //         if (gachaCanvasGroup != null)
    //             builder.RegisterInstance(gachaCanvasGroup);

    //         if (gachaMainPanel != null)
    //             builder.RegisterInstance(gachaMainPanel);

    //         if (gachaResultPanel != null)
    //             builder.RegisterInstance(gachaResultPanel);

    //         if (tokenCountText != null)
    //             builder.RegisterInstance(tokenCountText);

    //         if (drawWeaponOnceButton != null)
    //             builder.RegisterInstance(drawWeaponOnceButton);

    //         if (drawWeaponTenTimesButton != null)
    //             builder.RegisterInstance(drawWeaponTenTimesButton);

    //         if (drawEngravingOnceButton != null)
    //             builder.RegisterInstance(drawEngravingOnceButton);

    //         if (drawEngravingTenTimesButton != null)
    //             builder.RegisterInstance(drawEngravingTenTimesButton);

    //         if (gachaCloseButton != null)
    //             builder.RegisterInstance(gachaCloseButton);

    //         if (resultCloseButton != null)
    //             builder.RegisterInstance(resultCloseButton);

    //         if (resultSlotParent != null)
    //             builder.RegisterInstance(resultSlotParent);

    //         if (resultSlotPrefab != null)
    //             builder.RegisterInstance(resultSlotPrefab);
                

    //         if (masterSlider != null)
    //             builder.RegisterInstance(masterSlider).WithId("MasterSlider");

    //         if (bgmSlider != null)
    //             builder.RegisterInstance(bgmSlider).WithId("BgmSlider");

    //         if (sfxSlider != null)
    //             builder.RegisterInstance(sfxSlider).WithId("SfxSlider");

    //         if (fullscreenToggle != null)
    //             builder.RegisterInstance(fullscreenToggle).WithId("FullscreenToggle");

    //         if (resolutionDropdown != null)
    //             builder.RegisterInstance(resolutionDropdown).WithId("ResolutionDropdown");
                


    //         if (shopCanvasGroup != null)
    //         builder.RegisterInstance(shopCanvasGroup);

    //         if (shopSlotParent != null)
    //             builder.RegisterInstance(shopSlotParent);

    //         if (shopSlotPrefab != null)
    //             builder.RegisterInstance(shopSlotPrefab);

    //         if (shopCloseButton != null)
    //             builder.RegisterInstance(shopCloseButton);

    //         if (shopPlayerGoldText != null)
    //             builder.RegisterInstance(shopPlayerGoldText);

    //         if (shopPlayerInventoryParent != null)
    //             builder.RegisterInstance(shopPlayerInventoryParent);


    //         if (menuCanvasGroup != null)
    //             builder.RegisterInstance(menuCanvasGroup);

    //         if (menuMainPanel != null)
    //             builder.RegisterInstance(menuMainPanel);

    //         if (menuResumeButton != null)
    //             builder.RegisterInstance(menuResumeButton);

    //         if (menuSettingsButton != null)
    //             builder.RegisterInstance(menuSettingsButton);

    //         if (menuSettingsPanel != null)
    //             builder.RegisterInstance(menuSettingsPanel);

    //         if (menuControlButton != null)
    //             builder.RegisterInstance(menuControlButton);

    //         if (menuControlsPanel != null)
    //             builder.RegisterInstance(menuControlsPanel);

    //         if (menuMainMenuButton != null)
    //             builder.RegisterInstance(menuMainMenuButton);

    //         if (engravingCanvasGroup != null)
    //             builder.RegisterInstance(engravingCanvasGroup);

    //         if (engravingUIControllerPrefab != null)
    //         {
    //             builder.RegisterComponentInNewPrefab(engravingUIControllerPrefab, Lifetime.Singleton);
    //         }
    // }

    private void InstallDataManagers(IContainerBuilder builder)
    {
        if (currencyManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(currencyManagerPrefab, Lifetime.Singleton)
                .UnderTransform(this.transform)
                .AsImplementedInterfaces()
                .AsSelf()
                .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] CurrencyManager 프리팹이 할당되지 않았습니다!");
        }

        if (inventoryManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(inventoryManagerPrefab, Lifetime.Singleton)
                .UnderTransform(this.transform)
                .AsImplementedInterfaces()
                .AsSelf()
                .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] InventoryManager 프리팹이 할당되지 않았습니다!");
        }

        if (engravingManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(engravingManagerPrefab, Lifetime.Singleton)
                .UnderTransform(this.transform)
                .AsImplementedInterfaces()
                .AsSelf()
                .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] EngravingManager 프리팹이 할당되지 않았습니다!");
        }

        if (equipmentDataManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(equipmentDataManagerPrefab, Lifetime.Singleton)
                .UnderTransform(this.transform)
                .AsImplementedInterfaces()
                .AsSelf()
                .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] EquipmentDataManager 프리팹이 할당되지 않았습니다!");
        }

        if (shopManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(shopManagerPrefab, Lifetime.Singleton)
                .UnderTransform(this.transform)
                .AsImplementedInterfaces()
                .AsSelf()
                .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] ShopManager 프리팹이 할당되지 않았습니다!");
        }

        if (puzzleManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(puzzleManagerPrefab, Lifetime.Singleton)
                .UnderTransform(this.transform)
                .AsImplementedInterfaces()
                .AsSelf()
                .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] PuzzleManager 프리팹이 할당되지 않았습니다!");
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

    private void Start()
    {
        // 이 로직은 GameScene에서만 실행되어야 합니다.
        // Title 씬 등 다른 씬에서 NullReferenceException을 유발하는 것을 방지합니다.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene")
        {
            return;
        }

        StartCoroutine(LinkManagersAfterBuild());
    }

        private System.Collections.IEnumerator LinkManagersAfterBuild()
        {
            yield return new UnityEngine.WaitForEndOfFrame();
            
            try
            {
                var container = Container;
                if (container != null && 
                    container.TryResolve<StageManager>(out var stageManager) && 
                    container.TryResolve<DungeonManager>(out var dungeonManager))
                {
                    stageManager.SetDungeonManager(dungeonManager);
                    dungeonManager.SetStageManager(stageManager);
                }
                else
                {
                    Debug.LogWarning("[GameSceneLifetimeScope] Could not resolve StageManager or DungeonManager");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameInstaller] Failed to link StageManager and DungeonManager: {e.Message}");
            }
        }
}
