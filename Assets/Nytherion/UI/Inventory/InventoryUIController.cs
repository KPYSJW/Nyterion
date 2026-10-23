using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Nytherion.UI.Controllers
{
    /// <summary> 외부의 인벤토리 표시·새로고침 요청을 패널에 전달한다. 슬롯 갱신은 Presenter가 담당한다. </summary>
    public class InventoryUIController : MonoBehaviour, IInitializable, IDisposable
    {
        [Header("UI References")]
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private bool autoFindInventoryUI = true;

        private bool isInitialized;

        [Inject]
        public void Construct(InventoryUI inventoryUI)
        {
            this.inventoryUI = inventoryUI;
        }

        public void Initialize()
        {
            if (isInitialized) return;
            if (inventoryUI == null && autoFindInventoryUI)
                inventoryUI = FindObjectOfType<InventoryUI>();
            if (inventoryUI == null)
            {
                Debug.LogWarning("[InventoryUIController] InventoryUI를 찾을 수 없습니다.");
                return;
            }

            // 패널 초기화는 VContainer가 InventoryUI의 IInitializable을 통해 수행한다.
            isInitialized = true;
        }

        public void RequestUIUpdate()
        {
            if (isInitialized) inventoryUI?.RefreshUI();
        }

        public void SetInventoryUIVisible(bool visible)
        {
            if (inventoryUI != null) inventoryUI.gameObject.SetActive(visible);
        }

        public bool IsInventoryUIVisible()
        {
            return inventoryUI != null && inventoryUI.gameObject.activeInHierarchy;
        }

        public bool IsUIReady()
        {
            return isInitialized && inventoryUI != null && inventoryUI.IsInitialized;
        }

        public void Dispose()
        {
            isInitialized = false;
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
