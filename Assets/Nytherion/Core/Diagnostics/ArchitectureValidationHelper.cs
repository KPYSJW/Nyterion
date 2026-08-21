using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.Core.Systems;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Enums;
using Nytherion.UI.Controllers;
using Nytherion.Data.ScriptableObjects.Items;
using VContainer;

namespace Nytherion.Core.Test
{
    /// <summary>
    /// 새로운 아키텍처가 올바르게 작동하는지 검증하는 헬퍼 클래스
    /// 데이터 매니저와 UI 컨트롤러 간의 이벤트 기반 통신을 테스트
    /// </summary>
    public class ArchitectureValidationHelper : MonoBehaviour
    {
        [Header("테스트 컨트롤")]
        [SerializeField] private bool runValidationOnStart = false;
        [SerializeField] private bool enableDebugLogs = true;

        private InventoryDataManager _inventoryDataManager;
        private CurrencyDataManager _currencyDataManager;
        private InventoryUIController _inventoryUIController;
        private CurrencyUIController _currencyUIController;
        private DataLifetimeScope _dataScope;

        [Inject]
        public void Construct(
            InventoryDataManager inventoryDataManager,
            CurrencyDataManager currencyDataManager,
            InventoryUIController inventoryUIController = null,
            CurrencyUIController currencyUIController = null)
        {
            _inventoryDataManager = inventoryDataManager;
            _currencyDataManager = currencyDataManager;
            _inventoryUIController = inventoryUIController;
            _currencyUIController = currencyUIController;

            LogDebug("[ArchitectureValidationHelper] 의존성 주입 완료");
        }

        private void Start()
        {
            if (runValidationOnStart)
            {
                Invoke(nameof(RunValidation), 1f);
            }
        }

        [ContextMenu("아키텍처 검증 실행")]
        public void RunValidation()
        {
            LogDebug("=== 아키텍처 검증 시작 ===");

            // 1. DataLifetimeScope 검증
            ValidateDataLifetimeScope();
            ValidateDataManagers();
            ValidateUIControllers();
            ValidateEventCommunication();

            LogDebug("=== 아키텍처 검증 완료 ===");
        }

        private void ValidateDataLifetimeScope()
        {
            LogDebug("--- DataLifetimeScope 검증 ---");

            _dataScope = DataLifetimeScope.Instance;
            if (_dataScope == null)
            {
                LogError("DataLifetimeScope.Instance가 null입니다!");
                return;
            }

            LogDebug($"✅ DataLifetimeScope 인스턴스 존재: {_dataScope.name}");

            if (_dataScope.gameObject.scene.name == "DontDestroyOnLoad")
            {
                LogDebug("✅ DataLifetimeScope가 DontDestroyOnLoad에 있습니다");
            }
            else
            {
                LogWarning($"⚠️ DataLifetimeScope가 DontDestroyOnLoad에 없습니다. 현재 씬: {_dataScope.gameObject.scene.name}");
            }

            if (_dataScope.IsDataManagersReady())
            {
                LogDebug("✅ 데이터 매니저들이 준비되었습니다");
            }
            else
            {
                LogWarning("⚠️ 데이터 매니저들이 아직 준비되지 않았습니다");
            }
        }

        private void ValidateDataManagers()
        {
            LogDebug("--- 데이터 매니저 검증 ---");

            if (_inventoryDataManager != null)
            {
                LogDebug($"✅ InventoryDataManager 주입됨: {_inventoryDataManager.GetType().Name}");

                if (_inventoryDataManager is IDataManager dataManager)
                {
                    LogDebug($"✅ IDataManager 인터페이스 구현, 초기화 상태: {dataManager.IsInitialized}");
                }

                if (_inventoryDataManager is IInventoryDataNotifier notifier)
                {
                    LogDebug("✅ IInventoryDataNotifier 인터페이스 구현");
                }
            }
            else
            {
                LogError("❌ InventoryDataManager가 주입되지 않았습니다!");
            }

            if (_currencyDataManager != null)
            {
                LogDebug($"✅ CurrencyDataManager 주입됨: {_currencyDataManager.GetType().Name}");

                if (_currencyDataManager is IDataManager dataManager)
                {
                    LogDebug($"✅ IDataManager 인터페이스 구현, 초기화 상태: {dataManager.IsInitialized}");
                }

                if (_currencyDataManager is ICurrencyDataNotifier notifier)
                {
                    LogDebug("✅ ICurrencyDataNotifier 인터페이스 구현");
                }
            }
            else
            {
                LogError("❌ CurrencyDataManager가 주입되지 않았습니다!");
            }
        }

        private void ValidateUIControllers()
        {
            LogDebug("--- UI 컨트롤러 검증 ---");

            if (_inventoryUIController != null)
            {
                LogDebug($"✅ InventoryUIController 주입됨: {_inventoryUIController.GetType().Name}");

                if (_inventoryUIController.IsUIReady())
                {
                    LogDebug("✅ InventoryUIController UI 준비 완료");
                }
                else
                {
                    LogWarning("⚠️ InventoryUIController UI가 아직 준비되지 않았습니다");
                }
            }
            else
            {
                LogDebug("ℹ️ InventoryUIController가 주입되지 않음 (GameScene이 아닐 수 있음)");
            }

            if (_currencyUIController != null)
            {
                LogDebug($"✅ CurrencyUIController 주입됨: {_currencyUIController.GetType().Name}");

                if (_currencyUIController.IsUIReady())
                {
                    LogDebug("✅ CurrencyUIController UI 준비 완료");
                }
                else
                {
                    LogWarning("⚠️ CurrencyUIController UI가 아직 준비되지 않았습니다");
                }
            }
            else
            {
                LogDebug("ℹ️ CurrencyUIController가 주입되지 않음 (GameScene이 아닐 수 있음)");
            }
        }

        private void ValidateEventCommunication()
        {
            LogDebug("--- 이벤트 통신 검증 ---");

            if (_inventoryDataManager == null || _currencyDataManager == null)
            {
                LogError("데이터 매니저가 없어서 이벤트 통신을 테스트할 수 없습니다");
                return;
            }

            bool inventoryEventReceived = false;
            bool currencyEventReceived = false;

            void OnInventoryChanged(InventoryChangeData data)
            {
                inventoryEventReceived = true;
                LogDebug($"✅ 인벤토리 이벤트 수신: {data.changeType}, 슬롯 {data.slotIndex}");
            }

            void OnCurrencyChanged(CurrencyChangeData data)
            {
                currencyEventReceived = true;
                LogDebug($"✅ 통화 이벤트 수신: {data.currencyType}, {data.oldAmount} → {data.newAmount}");
            }

            _inventoryDataManager.OnDataChanged += OnInventoryChanged;
            _currencyDataManager.OnDataChanged += OnCurrencyChanged;
            try
            {
                LogDebug("테스트: 통화 추가 시뮬레이션...");
                _currencyDataManager.AddCurrency(CurrencyType.Gold, 100);

                LogDebug("테스트: 인벤토리 변경 시뮬레이션...");
                var testChangeData = new InventoryChangeData
                {
                    slotIndex = 0,
                    itemId = "test_item",
                    newCount = 1,
                    changeType = InventoryChangeType.ItemAdded
                };

                var testItem = ScriptableObject.CreateInstance<ItemData>();
                testItem.name = "TestItem";
                _inventoryDataManager.AddItem(testItem, 1);

                if (currencyEventReceived && inventoryEventReceived)
                {
                    LogDebug("✅ 이벤트 기반 통신이 정상적으로 작동합니다!");
                }
                else
                {
                    LogWarning($"⚠️ 일부 이벤트가 수신되지 않음. 인벤토리: {inventoryEventReceived}, 통화: {currencyEventReceived}");
                }
            }
            finally
            {
                _inventoryDataManager.OnDataChanged -= OnInventoryChanged;
                _currencyDataManager.OnDataChanged -= OnCurrencyChanged;
            }
        }

        [ContextMenu("씬 전환 테스트")]
        public void TestSceneTransition()
        {
            LogDebug("=== 씬 전환 데이터 지속성 테스트 ===");

            if (_dataScope == null)
            {
                LogError("DataLifetimeScope가 없습니다!");
                return;
            }

            int currentGold = _currencyDataManager?.GetCurrency(CurrencyType.Gold) ?? 0;
            LogDebug($"현재 골드: {currentGold}");

            _currencyDataManager?.AddCurrency(CurrencyType.Gold, 999);
            int newGold = _currencyDataManager?.GetCurrency(CurrencyType.Gold) ?? 0;
            LogDebug($"변경 후 골드: {newGold}");

            LogDebug("씬 전환 후에도 이 데이터가 유지되는지 확인하세요!");
            LogDebug("다른 씬으로 이동한 후 이 테스트를 다시 실행해보세요.");
        }

        private void LogDebug(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ArchitectureValidation] {message}");
            }
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[ArchitectureValidation] {message}");
            }
        }

        private void LogError(string message)
        {
            Debug.LogError($"[ArchitectureValidation] {message}");
        }
    }
}