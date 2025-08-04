using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.GamePlay;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Dungeon;
using Nytherion.GamePlay.Systems;
using Nytherion.UI.Controllers;
using Nytherion.UI.Presenters;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Nytherion.Core.Systems
{
    public class GameInstaller : MonoInstaller
    {
        [Header("Manager Prefabs")]
        [SerializeField] private GameObject playerPrefab;
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
        [SerializeField] private DungeonManager dungeonManagerPrefab;
        [Header("UI System")]
        [SerializeField] private GameUIInstaller gameUIInstaller;
        [SerializeField] private GameSceneUIManager gameSceneUIManager;

        [Header("Gameplay Systems")]
        [SerializeField] private EnemySpawner enemySpawnerPrefab;
        [SerializeField] private FollowCamera followCameraPrefab;
        [SerializeField] private SettingsManager settingsManagerPrefab;

        [Header("Databases")]
        [SerializeField] private ItemDatabaseSO itemDatabase;

        [Header("Scene Tilemaps")]
        [SerializeField] private Tilemap floorTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private Tilemap portalTilemap;

        public override void InstallBindings()
        {
            Container.Bind<InputManager>().FromComponentInNewPrefab(inputManagerPrefab).AsSingle().NonLazy();
            Container.Bind<AudioManager>().FromComponentInNewPrefab(audioManagerPrefab).AsSingle().NonLazy();
            Container.Bind<CurrencyManager>().FromComponentInNewPrefab(currencyManagerPrefab).AsSingle().NonLazy();
            Container.Bind<InventoryManager>().FromComponentInNewPrefab(inventoryManagerPrefab).AsSingle().NonLazy();
            Container.Bind<EngravingManager>().FromComponentInNewPrefab(engravingManagerPrefab).AsSingle().NonLazy();
            Container.Bind<EquipmentDataManager>().FromComponentInNewPrefab(equipmentDataManagerPrefab).AsSingle().NonLazy();
            Container.Bind<GachaManager>().FromComponentInNewPrefab(gachaManagerPrefab).AsSingle().NonLazy();
            Container.Bind<ShopManager>().FromComponentInNewPrefab(shopManagerPrefab).AsSingle().NonLazy();
            Container.Bind<SaveLoadManager>().FromComponentInNewPrefab(saveLoadManagerPrefab).AsSingle().NonLazy();
            Container.Bind<ObjectPoolManager>().FromComponentInNewPrefab(objectPoolManagerPrefab).AsSingle().NonLazy();
            Container.Bind<InteractionManager>().FromComponentInNewPrefab(interactionManagerPrefab).AsSingle().NonLazy();
            Container.Bind<StageManager>().FromComponentInNewPrefab(stageManagerPrefab).AsSingle().NonLazy();
            Container.Bind<EventManager>().FromComponentInNewPrefab(eventManagerPrefab).AsSingle().NonLazy();
            Container.Bind<DungeonManager>().FromComponentInNewPrefab(dungeonManagerPrefab).AsSingle().NonLazy();

            Container.Bind<ItemDatabaseSO>().FromInstance(itemDatabase).AsSingle();
            ItemDatabase.Initialize(itemDatabase);
            
            Container.Bind<PlayerManager>().FromComponentInNewPrefab(playerPrefab).AsSingle().NonLazy();
            Container.Bind<PlayerHealth>().FromResolveGetter<PlayerManager>(x => x.playerHealth).AsSingle();
            Container.Bind<PlayerCombat>().FromResolveGetter<PlayerManager>(x => x.PlayerCombat).AsSingle();
            Container.Bind<PlayerSkillManager>().FromResolveGetter<PlayerManager>(x => x.GetComponent<PlayerSkillManager>()).AsSingle();
            Container.Bind<PlayerController>().FromResolveGetter<PlayerManager>(x => x.GetComponent<PlayerController>()).AsSingle();

            Container.Bind<InventoryPresenter>().FromComponentInNewPrefab(inventoryManagerPrefab).AsSingle().NonLazy();

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

            Container.Bind<Tilemap>().WithId("FloorTilemap").FromInstance(floorTilemap).AsCached();
            Container.Bind<Tilemap>().WithId("WallTilemap").FromInstance(wallTilemap).AsCached();
            Container.Bind<Tilemap>().WithId("PortalTilemap").FromInstance(portalTilemap).AsCached();
        }

        public override void Start()
        {
            base.Start();

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