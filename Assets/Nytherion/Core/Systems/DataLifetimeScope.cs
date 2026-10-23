using Nytherion.Core.Managers;
using UnityEngine;
using VContainer;
using VContainer.Unity;

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
        [SerializeField] private RelicManager relicManagerPrefab;
        [SerializeField] private EquipmentDataManager equipmentDataManagerPrefab;
        [SerializeField] private ShopManager shopManagerPrefab;
        [SerializeField] private StageManager stageManagerPrefab;
        [SerializeField] private SkillDataManager skillDataManagerPrefab;
        [SerializeField] private ProgressionManager progressionManagerPrefab;

        protected override void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // BootSystem 내의 Awake 순서가 달라도 VContainer가 Root 빌드 완료 후 재시도한다.
            parentReference = ParentReference.Create<RootLifetimeScope>();
            base.Awake();
        }

        protected override void Configure(IContainerBuilder builder)
        {
            InstallDataManagers(builder);
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
                    .AsSelf();
            }

            if (inventoryDataManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(inventoryDataManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }

            if (relicManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(relicManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }

            if (equipmentDataManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(equipmentDataManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }

            if (shopManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(shopManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }

            if (stageManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(stageManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }

            if (skillDataManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(skillDataManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }

            if (progressionManagerPrefab != null)
            {
                builder.RegisterComponentInNewPrefab(progressionManagerPrefab, Lifetime.Singleton)
                    .UnderTransform(this.transform)
                    .AsImplementedInterfaces()
                    .AsSelf();
            }
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
                return container.TryResolve<SaveLoadManager>(out var saveManager) && saveManager.IsInitialized &&
                       IsManagerInitialized<CurrencyDataManager>() &&
                       IsManagerInitialized<InventoryDataManager>() &&
                       IsManagerInitialized<RelicManager>() &&
                       IsManagerInitialized<EquipmentDataManager>() &&
                       IsManagerInitialized<ShopManager>() &&
                       IsManagerInitialized<StageManager>() &&
                       IsManagerInitialized<SkillDataManager>() &&
                       IsManagerInitialized<ProgressionManager>() &&
                       saveManager.HasLoadedData && !saveManager.IsSaveBlocked;
            }
            catch
            {
                return false;
            }
        }

        private bool IsManagerInitialized<T>() where T : BaseManager
        {
            return Container.TryResolve<T>(out var manager) && manager.IsInitialized;
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
