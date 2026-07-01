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
    [SerializeField] private RelicUIController relicUIControllerPrefab;

    [Header("UI Controllers - �����Ϳ� UI �и�")]
    [SerializeField] private InventoryUIController inventoryUIControllerPrefab;
    [SerializeField] private CurrencyUIController currencyUIControllerPrefab;

    [Header("GameScene Only Systems")]
    [SerializeField] private PlayerManager playerManagerPrefab;
    [SerializeField] private MenuManager menuManagerPrefab;
    [SerializeField] private ItemUsageManager itemUsageManagerPrefab;
    [SerializeField] private InventoryManager inventoryManagerPrefab;
    [SerializeField] private CurrencyDataManager currencyManagerPrefab;

    [Header("Debug Systems")]
    [SerializeField] private RelicSystemDebugger relicSystemDebuggerPrefab;

    [Header("GameScene Only Gameplay")]
   // [SerializeField] private EnemySpawner enemySpawnerPrefab;
    [SerializeField] private FollowCamera followCameraPrefab;
    [SerializeField] private SettingsManager settingsManagerPrefab;
    [SerializeField] private InventoryPresenter inventoryPresenterPrefab;
    [SerializeField] private GameSceneUIManager gameSceneUIManager;

    // DataLifetimeScope ��⸦ ���� ���� ����
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
        // �θ� scope�� ���������� ���� scope���� ����� �� �ֵ��� ���
        RegisterParentScopeDependencies(builder);

        builder.RegisterComponentInHierarchy<VillagePortal>()
               .AsImplementedInterfaces()
               .AsSelf();

        // GameSceneUIRefs�� ���̶�Ű���� �ڵ����� ã�Ƽ� ��� (UI ��Ʈ�ѷ����� �ʿ�� ��)
        RegisterUIReferences(builder);

        // UI ��Ʈ�ѷ����� ���� ��ġ (NPC���� �������� ���Թޱ� ����)
        InstallGameSceneOnlyUI(builder);

        InstallGameSceneOnlyManagers(builder);

        InstallGameSceneOnlySystems(builder);

       
        // builder.RegisterEntryPoint<GameSceneInitializer>();
    }

    /// <summary>
    /// ������ �Ŵ������� �غ�� ������ ���
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
    /// ������ �Ŵ������� �غ�Ǿ��� �� ȣ��Ǵ� �ݹ�
    /// </summary>
    protected virtual void OnDataManagersReady()
    {
        // GameScene UI �ʱ�ȭ
        InitializeGameSceneUI();
    }

    private void RegisterParentScopeDependencies(IContainerBuilder builder)
    {
        // RootLifetimeScope���� �⺻ �Ŵ����� ��������
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

        // DataLifetimeScope���� ������ �Ŵ����� ��������
        dataLifetimeScope = DataLifetimeScope.Instance;
        if (dataLifetimeScope != null && dataLifetimeScope.Container != null && dataLifetimeScope.IsDataManagersReady())
        {
            // SaveLoadManager�� GameSceneLifetimeScope������ ���� �����ϵ��� ���
            RegisterDataManagerIfExists<SaveLoadManager>(builder);
            RegisterDataManagerIfExists<CurrencyDataManager>(builder);
            RegisterDataManagerIfExists<InventoryDataManager>(builder);
            RegisterDataManagerIfExists<RelicManager>(builder);
            RegisterDataManagerIfExists<EquipmentDataManager>(builder);
            RegisterDataManagerIfExists<ShopManager>(builder);
            RegisterDataManagerIfExists<StageManager>(builder);

            // RegisterDataManagerIfExists<PuzzleManager>(builder); // ���߿� ��� ����
            // PlayerManager�� GameScene������ �ʿ��ϹǷ� ���⼭ ���� ����
        }
    }

    /// <summary>
    /// ������ �Ŵ����� �����ϸ� ���� �����̳ʿ� ���
    /// </summary>
    private void RegisterDataManagerIfExists<T>(IContainerBuilder builder) where T : class
    {
        if (dataLifetimeScope.Container.TryResolve<T>(out var manager))
        {
            builder.RegisterInstance(manager);
        }
        else
        {
            Debug.LogWarning($"[GameSceneLifetimeScope] {typeof(T).Name}�� ã�� �� �����ϴ�");
        }
    }


    private void RegisterUIReferences(IContainerBuilder builder)
    {
        // GameSceneUIRefs ���
        builder.RegisterComponentInHierarchy<GameSceneUIRefs>()
               .AsSelf()
               .AsImplementedInterfaces();

        // QuickSlotManager ��� (GameSceneUIRefs ���Ŀ� ��ϵǾ�� ��)
        if (quickSlotManagerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(quickSlotManagerPrefab, Lifetime.Singleton)
                   .AsSelf()
                   .AsImplementedInterfaces()
                   .As<ISaveable>();
        }
        else
        {
            Debug.LogError("[GameSceneLifetimeScope] QuickSlotManager �������� �Ҵ���� �ʾҽ��ϴ�!");
        }
    }

    private void InstallGameSceneOnlyManagers(IContainerBuilder builder)
    {
        builder.RegisterComponentInNewPrefab(gachaManagerPrefab, Lifetime.Singleton)
                .AsImplementedInterfaces() // �������̽��� ���� ���� ����
                .AsSelf(); // �ڱ� �ڽ��� ���� ���� ����

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

    

        // SaveLoadManager�� DataLifetimeScope���� ������

        // UI ��Ʈ�ѷ��� ��� (�����Ϳ� UI �и�)
        InstallUIControllers(builder);

        // ISaveable �������̽����� �߰� ����Ͽ� SaveLoadManager�� ã�� �� �ֵ��� ��
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

        builder.RegisterComponentInHierarchy<CharacterStatsUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        // RelicGridUI�� IInitializable�� ����Ͽ� �ڵ� �ʱ�ȭ
        builder.RegisterComponentInHierarchy<RelicGridUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        // RelicTooltip�� �̱������� ���
        builder.RegisterComponentInHierarchy<Nytherion.UI.RelicBoard.RelicTooltip>()
                .AsImplementedInterfaces()
                .AsSelf();

        // ���� �ý��� ����� ���
        RegisterRelicSystemDebugger(builder);

        // �κ��丮 UI ���Ե��� ��� (������ ������ ����)
        RegisterInventoryUIComponents(builder);

        // UI ��Ʈ�ѷ����� ��ϵ� �Ŀ� NPC���� ��� (������ ���� ���� ����)
        RegisterNPCComponents(builder);

    }

    private void RegisterNPCComponents(IContainerBuilder builder)
    {
        // ShopDealer���� ������ ã�Ƽ� ���
        builder.RegisterComponentInHierarchy<ShopDealer>()
                .AsImplementedInterfaces()
                .AsSelf();

        // GachaNPC���� ������ ã�Ƽ� ���
        builder.RegisterComponentInHierarchy<GachaNPC>()
                .AsImplementedInterfaces()
                .AsSelf();

        // GameManager�� ������ ã�Ƽ� ���
        builder.RegisterComponentInHierarchy<GameManager>()
                .AsImplementedInterfaces()
                .AsSelf();

        // RelicAltar�� ������ ã�Ƽ� ���
        builder.RegisterComponentInHierarchy<Nytherion.GamePlay.Characters.NPC.RelicAltar>()
                .AsImplementedInterfaces()
                .AsSelf();

    }


    private void RegisterInventoryUIComponents(IContainerBuilder builder)
    {
        // �κ��丮 UI ���� ������Ʈ���� ������ ã�Ƽ� ���
        builder.RegisterComponentInHierarchy<Nytherion.UI.Inventory.InventorySlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<Nytherion.UI.Inventory.EquipmentSlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        builder.RegisterComponentInHierarchy<Nytherion.UI.Inventory.QuickSlotUI>()
                .AsImplementedInterfaces()
                .AsSelf();

        // QuickSlotManager�� �̹� RegisterUIReferences���� ��ϵ�
    }

    private void InstallGameSceneOnlySystems(IContainerBuilder builder)
    {
        // Player �ý���
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

  

        // ���� �ִ� FollowCamera ��� (Main Camera�� �پ��ִ� ���)
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
            Debug.LogWarning("[GameSceneLifetimeScope] FollowCamera�� ã�� �� �����ϴ�!");
        }

        SettingsManager settingsManagerInScene = FindObjectOfType<SettingsManager>(true);
        if (settingsManagerInScene != null)
        {
            builder.RegisterComponent(settingsManagerInScene)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        else if (settingsManagerPrefab != null)
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
        // InventoryUIController ���
        if (inventoryUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(inventoryUIControllerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }

        // CurrencyUIController ���
        if (currencyUIControllerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(currencyUIControllerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
    }

    private void RegisterISaveableEntities(IContainerBuilder builder)
    {
        // ��� ISaveable ��ƼƼ���� ���������� ����Ͽ� SaveLoadManager�� ã�� �� �ֵ��� ��
        // ������ �Ŵ������� �̹� .As<ISaveable>()�� ��ϵǾ� ����

        // QuickSlotManager�� ISaveable�� �߰� ���
        builder.Register<ISaveable>(resolver => resolver.Resolve<QuickSlotManager>(), Lifetime.Singleton);
    }

    private void RegisterRelicSystemDebugger(IContainerBuilder builder)
    {
        // ������ ���� ã�ƺ���
        var existingDebugger = FindObjectOfType<RelicSystemDebugger>();
        if (existingDebugger != null)
        {
            builder.RegisterComponent(existingDebugger)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        // �������� �Ҵ�Ǿ� �ִٸ� ����
        else if (relicSystemDebuggerPrefab != null)
        {
            builder.RegisterComponentInNewPrefab(relicSystemDebuggerPrefab, Lifetime.Singleton)
                    .AsImplementedInterfaces()
                    .AsSelf();
        }
        else
        {
            Debug.Log("[GameSceneLifetimeScope] RelicSystemDebugger�� �ǳʶݴϴ� (���� ���� �����յ� �Ҵ���� ����).");
        }
    }

    private void InitializeGameSceneUI()
    {
        // ��Ű��ó ���� ���� ��� (����� ��忡����)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        RegisterArchitectureValidationHelper();
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void RegisterArchitectureValidationHelper()
    {
        // ������ ArchitectureValidationHelper ã��
        var validationHelper = FindObjectOfType<ArchitectureValidationHelper>();
        if (validationHelper != null)
        {
            // VContainer�� ����Ͽ� ������ ������ �����ϵ��� ��
            Container.Inject(validationHelper);
        }
    }
#endif

}


