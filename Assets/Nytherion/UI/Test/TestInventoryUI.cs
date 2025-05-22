using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;
using Nytherion.Services;
using TMPro;

namespace Nytherion.UI.Test
{
    public class TestInventoryUI : MonoBehaviour
    {
        [Header("아이템 참조")]
        [SerializeField] private ItemData testItem1;
        [SerializeField] private ItemData testItem2;
        [SerializeField] private ItemData testItem3;

        [Header("UI 버튼")]
        [SerializeField] private Button addItem1Button;
        [SerializeField] private Button addItem2Button;
        [SerializeField] private Button addItem3Button;
        [SerializeField] private Button removeItem1Button;
        [SerializeField] private Button clearInventoryButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button clearSaveButton;

        [Header("상태 표시")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private float messageDuration = 2f;

        private float messageTimer;
        private string currentMessage;

        private void Start()
        {
            // 버튼 클릭 이벤트 연결
            if (addItem1Button != null) addItem1Button.onClick.AddListener(() => AddTestItem(testItem1));
            if (addItem2Button != null) addItem2Button.onClick.AddListener(() => AddTestItem(testItem2));
            if (addItem3Button != null) addItem3Button.onClick.AddListener(() => AddTestItem(testItem3));
            
            if (removeItem1Button != null) removeItem1Button.onClick.AddListener(RemoveTestItem1);
            if (clearInventoryButton != null) clearInventoryButton.onClick.AddListener(ClearInventory);
            if (saveButton != null) saveButton.onClick.AddListener(SaveInventory);
            if (loadButton != null) loadButton.onClick.AddListener(LoadInventory);
            if (clearSaveButton != null) clearSaveButton.onClick.AddListener(ClearSaveData);

            // 초기 메시지 설정
            ShowStatusMessage("Test UI is ready. Test the inventory!");
        }

        private void Update()
        {
            // 메시지 타이머 업데이트
            if (messageTimer > 0)
            {
                messageTimer -= Time.deltaTime;
                if (messageTimer <= 0)
                {
                    statusText.text = "";
                }
            }
        }

        private void AddTestItem(ItemData itemData, int count = 1)
        {
            if (itemData == null)
            {
                ShowStatusMessage("Item data not found");
                return;
            }

            bool success = InventoryManager.Instance.AddItem(itemData, count);
            if (success)
            {
                ShowStatusMessage($"{itemData.name} added x{count}");
            }
            else
            {
                ShowStatusMessage($"Failed to add {itemData.name} (inventory full)");
            }
        }

        private void RemoveTestItem1()
        {
            if (testItem1 == null) return;
            
            bool success = InventoryManager.Instance.RemoveItem(testItem1, 1);
            if (success)
            {
                ShowStatusMessage($"{testItem1.name} removed x1");
            }
            else
            {
                ShowStatusMessage($"{testItem1.name} not found in inventory");
            }
        }

        private void ClearInventory()
        {
            InventoryManager.Instance.ClearInventory();
            ShowStatusMessage("Inventory cleared");
        }

        private void SaveInventory()
        {
            InventoryManager.Instance.SaveInventory();
            ShowStatusMessage("Inventory saved");
        }

        private void LoadInventory()
        {
            InventoryManager.Instance.LoadInventory();
            ShowStatusMessage("Inventory loaded");
        }

        private void ClearSaveData()
        {
            var saveService = new PlayerPrefsInventorySaveService();
            saveService.DeleteSaveData();
            ShowStatusMessage("Saved data cleared");
        }

        private void ShowStatusMessage(string message)
        {
            if (statusText != null)
            {
                currentMessage = message;
                statusText.text = currentMessage;
                messageTimer = messageDuration;
                
                Debug.Log($"[TestUI] {message}");
            }
        }
    }
}
