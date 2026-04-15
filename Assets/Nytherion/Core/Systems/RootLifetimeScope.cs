using UnityEngine.SceneManagement;
using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Data.ScriptableObjects.Items;
using VContainer;
using VContainer.Unity;
using Nytherion.Scenes;


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
    [SerializeField] private DataLifetimeScope dataLifetimeScopePrefab;

    [Header("System Managers")]
    [SerializeField] private AudioManager audioManagerPrefab;
    [SerializeField] private SceneTransitionManager sceneTransitionManagerPrefab;
    [SerializeField] private InputManager inputManagerPrefab;

    [SerializeField] private ObjectPoolManager objectPoolManagerPrefab;
    protected override void Configure(IContainerBuilder builder)
    {
        InstallCoreInfrastructure(builder);
        InstallSystemManagers(builder);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Boot")
        {
            var sceneTransitionManager = Container.Resolve<SceneTransitionManager>();
            // DataLifetimeScope 생성
            CreateDataLifetimeScope();

            
            sceneTransitionManager.LoadTargetScene("Title");
        }
        else if(scene.name == "BootTest")
        {
            CreateDataLifetimeScope();

            var sceneTransitionManager = Container.Resolve<SceneTransitionManager>();
            sceneTransitionManager.LoadTargetScene("TitleTest");
        }
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void CreateDataLifetimeScope()
    {
        // 이미 씬에 DataLifetimeScope가 존재하는지 확인
        var existingDataScope = FindObjectOfType<DataLifetimeScope>();
        if (existingDataScope != null)
        {
            return;
        }

        if (dataLifetimeScopePrefab != null && DataLifetimeScope.Instance == null)
        {
            //var dataScope = Instantiate(dataLifetimeScopePrefab);
            var dataScope = Instantiate(dataLifetimeScopePrefab, this.transform);
        }
        else if (dataLifetimeScopePrefab == null)
        {
            Debug.LogError("[RootLifetimeScope] DataLifetimeScope 프리팹이 할당되지 않았습니다!");
        }
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

        // SaveLoadManager는 DataLifetimeScope로 이동됨
        
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

        builder.RegisterComponentInNewPrefab(objectPoolManagerPrefab, Lifetime.Singleton)
            .UnderTransform(this.transform)
            .AsImplementedInterfaces()
            .AsSelf();
    }

}