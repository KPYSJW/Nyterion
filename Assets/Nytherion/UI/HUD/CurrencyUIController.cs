using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Managers;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Enums;
using Nytherion.UI.Components;
using System;
using System.Collections.Generic;

namespace Nytherion.UI.Controllers
{
    /// <summary>
    /// 통화 UI만 담당하는 컨트롤러
    /// 데이터는 CurrencyDataManager에서 가져와서 UI에 표시
    /// </summary>
    public class CurrencyUIController : MonoBehaviour, IInitializable, IDisposable
    {
        [Header("UI References")]
        [SerializeField] private CurrencyDisplay[] currencyDisplays;
        [SerializeField] private bool autoFindCurrencyDisplays = true;

        private CurrencyDataManager currencyDataManager;
        private EventManager eventManager;
        private GameSceneUIRefs uiRefs;
        private bool isInitialized = false;

        // UI 컴포넌트 매핑
        private Dictionary<CurrencyType, CurrencyDisplay> currencyUIMap;

        [Inject]
        public void Construct(CurrencyDataManager currencyDataManager, EventManager eventManager, GameSceneUIRefs uiRefs)
        {
            this.currencyDataManager = currencyDataManager;
            this.eventManager = eventManager;
            this.uiRefs = uiRefs;
        }

        public void Initialize()
        {
            if (isInitialized) return;

            if (uiRefs != null && uiRefs.CurrencyDisplays != null && uiRefs.CurrencyDisplays.Length > 0)
            {
                currencyDisplays = uiRefs.CurrencyDisplays;
            }
            else if (currencyDisplays != null && currencyDisplays.Length > 0)
            {
                Debug.Log("[CurrencyUIController] 수동 할당된 CurrencyDisplay 사용");
            }
            else if (autoFindCurrencyDisplays)
            {
                currencyDisplays = FindObjectsOfType<CurrencyDisplay>();
            }

            if (currencyDataManager == null)
            {
                Debug.LogError("[CurrencyUIController] CurrencyDataManager가 주입되지 않았습니다!");
                return;
            }

            // UI 매핑 초기화
            InitializeCurrencyUIMapping();

            // 데이터 변경 이벤트 구독
            currencyDataManager.OnDataChanged += HandleCurrencyDataChanged;

            // UI 초기화
            RefreshAllCurrencyUI();

            isInitialized = true;
        }

        private void InitializeCurrencyUIMapping()
        {
            currencyUIMap = new Dictionary<CurrencyType, CurrencyDisplay>();

            if (currencyDisplays == null) return;

            foreach (var display in currencyDisplays)
            {
                if (display != null)
                {
                    currencyUIMap[display.CurrencyType] = display;
                }
            }
        }

        private void HandleCurrencyDataChanged(CurrencyChangeData changeData)
        {
            if (!isInitialized) return;

            // 해당 통화 UI 업데이트
            UpdateCurrencyUI(changeData.currencyType, changeData.newAmount);

            // UI 업데이트 애니메이션 (변경량 표시)
            if (changeData.changeAmount != 0)
            {
                ShowCurrencyChangeEffect(changeData);
            }

            // 통화 UI 업데이트 이벤트 발송
            TriggerCurrencyUIUpdate(changeData);
        }

        private void UpdateCurrencyUI(CurrencyType currencyType, int newAmount)
        {
            if (!currencyUIMap.ContainsKey(currencyType)) return;

            var display = currencyUIMap[currencyType];
            if (display == null) return;

            try
            {
                display.UpdateAmount(newAmount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CurrencyUIController] {currencyType} UI 업데이트 실패: {ex.Message}");
            }
        }

        private void ShowCurrencyChangeEffect(CurrencyChangeData changeData)
        {
            if (!currencyUIMap.ContainsKey(changeData.currencyType)) return;

            var display = currencyUIMap[changeData.currencyType];
            if (display == null) return;

            try
            {
                // 변경량 표시 효과 (CurrencyDisplay에 해당 기능이 있다면)
                if (changeData.changeAmount > 0)
                {
                    display.ShowGainEffect(changeData.changeAmount);
                }
                else
                {
                    display.ShowLossEffect(-changeData.changeAmount);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CurrencyUIController] 통화 변경 효과 표시 실패: {ex.Message}");
            }
        }

        private void RefreshAllCurrencyUI()
        {
            if (currencyDataManager == null) return;

            var allCurrencies = currencyDataManager.GetAllCurrencies();

            foreach (var kvp in allCurrencies)
            {
                UpdateCurrencyUI(kvp.Key, kvp.Value);
            }
        }

        private void TriggerCurrencyUIUpdate(CurrencyChangeData changeData)
        {
            // 다른 UI 시스템들에게 통화 UI가 업데이트되었음을 알림
            eventManager?.TriggerEvent("CurrencyUIUpdated", new Dictionary<string, object>
            {
                ["currencyType"] = changeData.currencyType,
                ["newAmount"] = changeData.newAmount,
                ["changeAmount"] = changeData.changeAmount
            });
        }

        /// <summary>
        /// 외부에서 UI 업데이트를 요청할 때 사용
        /// </summary>
        public void RequestUIUpdate()
        {
            if (isInitialized)
            {
                RefreshAllCurrencyUI();
            }
        }

        /// <summary>
        /// 특정 통화 UI만 업데이트
        /// </summary>
        public void RequestCurrencyUIUpdate(CurrencyType currencyType)
        {
            if (isInitialized && currencyDataManager != null)
            {
                int amount = currencyDataManager.GetCurrency(currencyType);
                UpdateCurrencyUI(currencyType, amount);
            }
        }

        /// <summary>
        /// 통화 UI 표시/숨김
        /// </summary>
        public void SetCurrencyUIVisible(CurrencyType currencyType, bool visible)
        {
            if (!currencyUIMap.ContainsKey(currencyType)) return;

            var display = currencyUIMap[currencyType];
            if (display == null) return;

            try
            {
                display.gameObject.SetActive(visible);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CurrencyUIController] {currencyType} UI 표시 상태 변경 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 모든 통화 UI 표시/숨김
        /// </summary>
        public void SetAllCurrencyUIVisible(bool visible)
        {
            foreach (var kvp in currencyUIMap)
            {
                SetCurrencyUIVisible(kvp.Key, visible);
            }
        }

        /// <summary>
        /// 현재 통화 UI 상태 조회
        /// </summary>
        public bool IsCurrencyUIVisible(CurrencyType currencyType)
        {
            if (!currencyUIMap.ContainsKey(currencyType)) return false;

            var display = currencyUIMap[currencyType];
            return display != null && display.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// 통화 UI가 준비되었는지 확인
        /// </summary>
        public bool IsUIReady()
        {
            return isInitialized && currencyDataManager != null && currencyUIMap.Count > 0;
        }

        /// <summary>
        /// 특정 통화의 현재 표시 금액 조회
        /// </summary>
        public int GetDisplayedAmount(CurrencyType currencyType)
        {
            if (!currencyUIMap.ContainsKey(currencyType)) return 0;

            var display = currencyUIMap[currencyType];
            return display?.GetDisplayedAmount() ?? 0;
        }

        public void Dispose()
        {
            if (currencyDataManager != null)
            {
                currencyDataManager.OnDataChanged -= HandleCurrencyDataChanged;
            }

            isInitialized = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        // 디버그 메서드들
        [ContextMenu("통화 UI 새로고침")]
        public void DebugRefreshUI()
        {
            RequestUIUpdate();
        }

        [ContextMenu("골드 UI 토글")]
        public void DebugToggleGoldUI()
        {
            if (IsUIReady())
            {
                bool isVisible = IsCurrencyUIVisible(CurrencyType.Gold);
                SetCurrencyUIVisible(CurrencyType.Gold, !isVisible);
            }
        }

        [ContextMenu("모든 통화 UI 토글")]
        public void DebugToggleAllUI()
        {
            if (IsUIReady())
            {
                bool isGoldVisible = IsCurrencyUIVisible(CurrencyType.Gold);
                SetAllCurrencyUIVisible(!isGoldVisible);
            }
        }
    }
}