using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core.Interfaces;    
using VContainer;
using VContainer.Unity;

public class RootLifetimeScope : LifetimeScope
{
    public static RootLifetimeScope Instance { get; private set; }
    
    protected override void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        base.Awake();
        
    }

    [Header("Core Infrastructure")]
    [SerializeField] private EventManager eventManagerPrefab;
    [SerializeField] private ItemDatabaseSO itemDatabase;
    // SaveLoadManager는 GameSceneLifetimeScope로 이동
    // [SerializeField] private SaveLoadManager saveLoadManagerPrefab;

    [Header("Data Managers - 모두 GameSceneLifetimeScope로 이동")]
    // [SerializeField] private CurrencyManager currencyManagerPrefab;
    // [SerializeField] private InventoryManager inventoryManagerPrefab;
    // [SerializeField] private EngravingManager engravingManagerPrefab;
    // [SerializeField] private EquipmentDataManager equipmentDataManagerPrefab;
    // [SerializeField] private ShopManager shopManagerPrefab;

    [Header("System Managers")]
    [SerializeField] private AudioManager audioManagerPrefab;
    [SerializeField] private SceneTransitionManager sceneTransitionManagerPrefab;
    [SerializeField] private InputManager inputManagerPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        InstallCoreInfrastructure(builder);

        // 데이터 매니저들은 GameSceneLifetimeScope에서 관리
        // InstallDataManagers(builder);

        InstallSystemManagers(builder);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Boot")
        {
            var sceneTransitionManager = Container.Resolve<SceneTransitionManager>();
            sceneTransitionManager.LoadTargetScene("Title");
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;

    }

    private void InstallCoreInfrastructure(IContainerBuilder builder)
    {
        
        // EventManager 프리팹 등록
        if (eventManagerPrefab == null)
        {
            Debug.LogError("[RootLifetimeScope] eventManagerPrefab이 할당되지 않았습니다!");
        }
        else
        {
            builder.RegisterComponentInNewPrefab(eventManagerPrefab, Lifetime.Singleton)
                .UnderTransform(this.transform)
                .AsImplementedInterfaces()
                .AsSelf();
        }

        // ItemDatabase 등록
        if (itemDatabase == null)
        {
            Debug.LogError("[RootLifetimeScope] itemDatabase가 할당되지 않았습니다!");
        }
        else
        {
            builder.RegisterInstance(itemDatabase);
            ItemDatabase.Initialize(itemDatabase);
        }

        // SaveLoadManager는 GameSceneLifetimeScope로 이동됨
        
    }


    private void InstallSystemManagers(IContainerBuilder builder)
    {
        builder.RegisterComponentInNewPrefab(audioManagerPrefab, Lifetime.Singleton)
            .UnderTransform(this.transform)
            .AsImplementedInterfaces()
            .AsSelf();

        builder.RegisterComponentInNewPrefab(sceneTransitionManagerPrefab, Lifetime.Singleton)
            .UnderTransform(this.transform)
            .AsImplementedInterfaces()
            .AsSelf();

        builder.RegisterComponentInNewPrefab(inputManagerPrefab, Lifetime.Singleton)
            .UnderTransform(this.transform)
            .AsImplementedInterfaces()
            .AsSelf();
    }

}