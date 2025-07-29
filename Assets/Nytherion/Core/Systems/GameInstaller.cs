using UnityEngine;
using Zenject;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.UI.Presenters;
using Nytherion.GamePlay.Systems;
using Nytherion.GamePlay;
using Nytherion.UI.Controllers;
using Nytherion.UI.Shop;
using Nytherion.UI.Inventory;
using Nytherion.Core.Systems;
using Nytherion.Core.Interfaces;

namespace Nytherion.Core.Systems
{
    public class GameInstaller : MonoInstaller
    {
        [SerializeField] private SellSlotUI sellSlotUIPrefab;
        [SerializeField] private InventoryUI inventoryUIPrefab;
        [Header("Manager Prefabs")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private PlayerManager playerManagerPrefab;
        [SerializeField] private InputManager inputManagerPrefab;
        [SerializeField] private AudioManager audioManagerPrefab;
        [SerializeField] private CurrencyManager currencyManagerPrefab;
        [SerializeField] private InventoryManager inventoryManagerPrefab;
        [SerializeField] private EngravingManager engravingManagerPrefab;
        [SerializeField] private EquipmentDataManager equipmentDataManagerPrefab;
        [SerializeField] private GachaManager gachaManagerPrefab;
        [SerializeField] private ShopManager shopManagerPrefab;
        [SerializeField] private SaveLoadManager saveLoadManagerPrefab;
        [SerializeField] private ObjectPoolManager objectPoolManagerPrefab;
        [SerializeField] private InteractionManager interactionManagerPrefab;
        [SerializeField] private StageManager stageManagerPrefab;
        [SerializeField] private EventManager eventManagerPrefab;
        [SerializeField] private MenuManager menuManagerPrefab;
        [SerializeField] private ItemUsageManager itemUsageManagerPrefab;
        [SerializeField] private QuickSlotManager quickSlotManagerPrefab;

        [Header("UI System")]
        [SerializeField] private GameUIInstaller gameUIInstaller;
        [SerializeField] private GameSceneUIManager gameSceneUIManager;

        [Header("Gameplay Systems")]
        [SerializeField] private EnemySpawner enemySpawnerPrefab;
        [SerializeField] private FollowCamera followCameraPrefab;
        [SerializeField] private SettingsManager settingsManagerPrefab;

        [Header("Databases")]
        [SerializeField] private ItemDatabaseSO itemDatabase;
        [Header("UI Prefabs")]
        [SerializeField] private InventoryPresenter inventoryPresenterPrefab;

        [Header("UI References")]
        [SerializeField] private ShopUI shopUIPrefab;

        public override void InstallBindings()
        {
            Container.Bind<InputManager>().FromComponentInNewPrefab(inputManagerPrefab).AsSingle().NonLazy();
            Container.Bind<AudioManager>().FromComponentInNewPrefab(audioManagerPrefab).AsSingle().NonLazy();
            Container.Bind<CurrencyManager>().FromComponentInNewPrefab(currencyManagerPrefab).AsSingle().NonLazy();
            Container.Bind<InventoryManager>()
                .FromComponentInNewPrefab(inventoryManagerPrefab)
                .AsSingle()
                .NonLazy();

            Container.Bind<InventoryManager>()
                .WithId("InventoryManager")
                .FromResolveGetter<DiContainer>(container => container.Resolve<InventoryManager>())
                .AsCached();
            Container.Bind<EngravingManager>().FromComponentInNewPrefab(engravingManagerPrefab).AsSingle().NonLazy();
            Container.Bind<EquipmentDataManager>().FromComponentInNewPrefab(equipmentDataManagerPrefab).AsSingle().NonLazy();
            Container.Bind<GachaManager>().FromComponentInNewPrefab(gachaManagerPrefab).AsSingle().NonLazy();
            Container.Bind<ShopManager>().FromComponentInNewPrefab(shopManagerPrefab).AsSingle().NonLazy();
            Container.Bind<SaveLoadManager>().FromComponentInNewPrefab(saveLoadManagerPrefab).AsSingle().NonLazy();
            Container.Bind<ObjectPoolManager>().FromComponentInNewPrefab(objectPoolManagerPrefab).AsSingle().NonLazy();
            Container.Bind<InteractionManager>().FromComponentInNewPrefab(interactionManagerPrefab).AsSingle().NonLazy();
            Container.Bind<StageManager>().FromComponentInNewPrefab(stageManagerPrefab).AsSingle().NonLazy();
            Container.Bind<EventManager>().FromComponentInNewPrefab(eventManagerPrefab).AsSingle().NonLazy();

            // Bind all ISaveable implementations
            Container.Bind<ISaveable>().To<CurrencyManager>().FromResolve();
            Container.Bind<ISaveable>().To<InventoryManager>().FromResolve();
            Container.Bind<ISaveable>().To<EngravingManager>().FromResolve();
            Container.Bind<ISaveable>().To<ShopManager>().FromResolve();
            Container.Bind<ISaveable>().To<QuickSlotManager>().FromResolve();
            Container.Bind<ISaveable>().To<EquipmentDataManager>().FromResolve();
            Container.Bind<SellSlotUI>()
                 .WithId("SellSlotUI")
                 .FromComponentInNewPrefab(sellSlotUIPrefab)
                 .AsSingle();
            Container.Bind<InventoryUI>()
                 .WithId("InventoryUI")
                 .FromComponentInNewPrefab(inventoryUIPrefab)
                 .AsSingle();
            Container.Bind<ItemDatabaseSO>().FromInstance(itemDatabase).AsSingle();
            ItemDatabase.Initialize(itemDatabase);

            if (playerManagerPrefab != null)
            {
                Container.Bind<PlayerManager>()
                    .FromComponentInNewPrefab(playerManagerPrefab)
                    .AsSingle()
                    .NonLazy();

                Container.Bind<PlayerHealth>()
                    .FromResolveGetter<PlayerManager>(x => x.playerHealth)
                    .AsSingle();
                Container.Bind<PlayerCombat>()
                    .FromResolveGetter<PlayerManager>(x => x.PlayerCombat)
                    .AsSingle();
                Container.Bind<PlayerSkillManager>()
                    .FromResolveGetter<PlayerManager>(x => x.GetComponent<PlayerSkillManager>())
                    .AsSingle();
                Container.Bind<PlayerController>()
                    .FromResolveGetter<PlayerManager>(x => x.GetComponent<PlayerController>())
                    .AsSingle();
            }
            if (inventoryPresenterPrefab != null)
            {
                // 1. ID 없이 바인딩 (GameSceneUIManager용)
                Container.Bind<InventoryPresenter>()
                    .FromComponentInNewPrefab(inventoryPresenterPrefab)
                    .AsSingle()
                    .NonLazy();

                // 2. ID로도 바인딩 (다른 컴포넌트용)
                Container.Bind<InventoryPresenter>()
                    .WithId("InventoryPresenter")
                    .FromResolveGetter<DiContainer>(container => container.Resolve<InventoryPresenter>())
                    .AsCached();
            }
            if (enemySpawnerPrefab != null)
            {
                Container.Bind<EnemySpawner>().FromComponentInNewPrefab(enemySpawnerPrefab).AsSingle().NonLazy();
            }

            if (followCameraPrefab != null)
            {
                Container.Bind<FollowCamera>().FromComponentInNewPrefab(followCameraPrefab).AsSingle().NonLazy();
            }

            if (settingsManagerPrefab != null)
            {
                Container.Bind<SettingsManager>().FromComponentInNewPrefab(settingsManagerPrefab).AsSingle().NonLazy();
            }

            if (menuManagerPrefab != null)
            {
                Container.Bind<MenuManager>().FromComponentInNewPrefab(menuManagerPrefab).AsSingle().NonLazy();
            }

            if (gameUIInstaller != null)
            {
                gameUIInstaller.SetContainer(Container);
                gameUIInstaller.InstallBindings();
            }

            if (gameSceneUIManager != null)
            {
                Container.Bind<GameSceneUIManager>().FromInstance(gameSceneUIManager).AsSingle();
            }
            if (shopUIPrefab != null)
            {
                // 1. ID 없이 바인딩 (InteractionManager용)
                Container.Bind<ShopUI>()
                    .FromComponentInNewPrefab(shopUIPrefab)
                    .AsSingle()
                    .NonLazy();

                // 2. ID로도 바인딩 (InventoryUI용)
                Container.Bind<ShopUI>()
                    .WithId("ShopUI")
                    .FromResolveGetter<DiContainer>(container => container.Resolve<ShopUI>())
                    .AsCached();
            }
            if (itemUsageManagerPrefab != null)
            {
                Container.Bind<ItemUsageManager>()
                    .FromComponentInNewPrefab(itemUsageManagerPrefab)
                    .AsSingle()
                    .NonLazy();
            }
        }

        public override void Start()
        {
            base.Start();
            if (Container.HasBinding<PlayerManager>())
            {
                var playerManager = Container.Resolve<PlayerManager>();
                Debug.Log("PlayerManager resolved successfully");
            }
            else
            {
                Debug.LogError("PlayerManager binding not found");
            }
            Container.Resolve<InputManager>().Initialize();
            Container.Resolve<InventoryManager>().Initialize();
            Container.Resolve<EngravingManager>().Initialize();
            Container.Resolve<CurrencyManager>().Initialize();
            Container.Resolve<EquipmentDataManager>().Initialize();
            Container.Resolve<ShopManager>().Initialize();
            Container.Resolve<SaveLoadManager>().Initialize();
        }
    }
}