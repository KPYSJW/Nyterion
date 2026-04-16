using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Managers;
using Nytherion.Core.Interfaces;

namespace Nytherion.Core.Systems
{
    /// <summary>
    /// 게임 데이터 매니저들을 관리하는 LifetimeScope
    /// DontDestroyOnLoad로 씬 간 데이터 지속성 보장
    /// </summary>
    public class DataLifetimeScope : LifetimeScope
    {
        public static DataLifetimeScope Instance { get; private set; }

        [SerializeField] private SaveLoadManager saveLoadManagerPrefab;
        [SerializeField] private CurrencyDataManager currencyDataManagerPrefab;
        [SerializeField] private InventoryDataManager inventoryDataManagerPrefab;
        [SerializeField] private EngravingManager engravingManagerPrefab;
        [SerializeField] private EquipmentDataManager equipmentDataManagerPrefab;
        [SerializeField] private ShopManager shopManagerPrefab;
        [SerializeField] private StageManager stageManagerPrefab;
        [SerializeField] private SkillDataManager skillDataManagerPrefab;
        [SerializeField] private ProgressionManager progressionManagerPrefab;

        protected override void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            //DontDestroyOnLoad(gameObject);

            base.Awake();
        }

        private bool hasConfigured = false;

        protected override void Configure(IContainerBuilder builder)
        {
            if (hasConfigured)
            {
                Debug.LogWarning($"[DataLifetimeScope] Configure가 이미 호출되었습니다. 중복 호출을 건너뜁니다.");
                return;
            }
           
            InstallDataManagers(builder);
            RegisterISaveableEntities(builder);
            hasConfigured = true;
        }

        private void InstallDataManagers(IContainerBuilder builder)
        {
            if (saveLoadManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(saveLoadManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }

            if (currencyDataManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(currencyDataManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>()
                    .As<IDataManager>()
                    .As<ICurrencyDataNotifier>();
            }

            if (inventoryDataManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(inventoryDataManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>()
                    .As<IDataManager>()
                    .As<IInventoryDataNotifier>();
            }

            if (engravingManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(engravingManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>();
            }

            if (equipmentDataManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(equipmentDataManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>();
            }

            if (shopManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(shopManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>();
            }

            if (stageManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(stageManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>();
            }

            if(skillDataManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(skillDataManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>();
            }

            if (progressionManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(progressionManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf()
                    .As<ISaveable>();
            }
        }

        private bool hasRegisteredISaveableCollection = false;

        private void RegisterISaveableEntities(IContainerBuilder builder)
        {
            // 인스턴스별 중복 등록 방지
            if (hasRegisteredISaveableCollection)
            {
                return;
            }

            hasRegisteredISaveableCollection = true;
        }

        /// <summary>
        /// 데이터 매니저의 준비 상태를 확인
        /// </summary>
        public bool IsDataManagersReady()
        {
            var container = Container;
            if (container == null) return false;

            try
            {
                return container.TryResolve<SaveLoadManager>(out _) &&
                       container.TryResolve<CurrencyDataManager>(out _) &&
                       container.TryResolve<InventoryDataManager>(out _);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 특정 데이터 매니저 조회
        /// </summary>
        public T GetDataManager<T>() where T : class
        {
            var container = Container;
            if (container == null) return null;

            return container.TryResolve<T>(out var manager) ? manager : null;
        }

        protected override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            base.OnDestroy();
        }
    }
}
