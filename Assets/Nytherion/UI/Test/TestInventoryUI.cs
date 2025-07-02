using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.Core;
using Nytherion.Services;
using TMPro;
using System.Linq;

namespace Nytherion.UI.Test
{
    public class TestInventoryUI : MonoBehaviour
    {
        [Header("아이템 참조")]
        [SerializeField] private ItemData testItem1;
        [SerializeField] private ItemData testItem2;
        [SerializeField] private ItemData testWeapon;

        [Header("UI 버튼")]
        [SerializeField] private Button addItem1Button;
        [SerializeField] private Button addItem2Button;
        [SerializeField] private Button addItem3Button;
        [SerializeField] private Button removeItem1Button;
        [SerializeField] private Button clearInventoryButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button clearSaveButton;
        [Space(10)]
        [Header("디버그 버튼")]
        [SerializeField] private Button debugSaveDataButton;
        [SerializeField] private Button debugItemTableButton;
        [SerializeField] private Button debugCurrentInventoryButton;

        [Header("상태 표시")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private float messageDuration = 2f;

        private float messageTimer;
        private string currentMessage;

        private void Start()
        {
            if (addItem1Button != null) addItem1Button.onClick.AddListener(() => AddTestItem(testItem1));
            if (addItem2Button != null) addItem2Button.onClick.AddListener(() => AddTestItem(testItem2));
            if (addItem3Button != null) addItem3Button.onClick.AddListener(() => AddTestItem(testWeapon));

            if (removeItem1Button != null) removeItem1Button.onClick.AddListener(RemoveTestItem1);
            if (clearInventoryButton != null) clearInventoryButton.onClick.AddListener(ClearInventory);

            if (saveButton != null) saveButton.onClick.AddListener(SaveInventory);
            if (loadButton != null) loadButton.onClick.AddListener(LoadInventory);

            if (debugItemTableButton != null) debugItemTableButton.onClick.AddListener(DebugItemTable);
            if (debugCurrentInventoryButton != null) debugCurrentInventoryButton.onClick.AddListener(DebugCurrentInventory);

            ShowStatusMessage("Test UI is ready. Test the inventory!");
        }

        private void Update()
        {
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
            var items = InventoryManager.Instance.GetAllItems();

            if (items.Count == 0)
            {
                ShowStatusMessage("Inventory is empty");
                return;
            }

            var firstItem = items.Keys.First();
            int count = items[firstItem];

            int removeCount = Mathf.Min(1, count);
            bool success = InventoryManager.Instance.RemoveItem(firstItem, removeCount);

            if (success)
            {
                ShowStatusMessage($"Removed {removeCount}x {firstItem.name}");
            }
            else
            {
                ShowStatusMessage("Failed to remove item");
            }
        }

        private void ClearInventory()
        {
            InventoryManager.Instance.ClearInventory();
            ShowStatusMessage("Inventory cleared");
        }

        private void SaveInventory()
        {
            SaveLoadManager.Instance.SaveGame();
            ShowStatusMessage("Game Saved via SaveLoadManager");
        }

        private void LoadInventory()
        {
            try
            {
                SaveLoadManager.Instance.LoadGame();
                ShowStatusMessage("Game Loaded via SaveLoadManager");

            }
            catch (System.Exception e)
            {
                ShowStatusMessage($"Load failed: {e.Message}");
            }
        }

        private void ShowStatusMessage(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                currentMessage = message;
                messageTimer = messageDuration;
            }
        }

        public void DebugItemTable()
        {
            if (InventoryManager.Instance == null)
            {
                Debug.LogError("InventoryManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            var field = typeof(InventoryManager).GetField("itemTable",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                var itemTable = field.GetValue(InventoryManager.Instance) as System.Collections.Generic.Dictionary<string, ItemData>;
                if (itemTable != null && itemTable.Count > 0)
                {
                    Debug.Log($"=== 아이템 테이블 (총 {itemTable.Count}개) ===");
                    foreach (var pair in itemTable)
                    {
                        Debug.Log($"ID: {pair.Key}, 이름: {pair.Value.name}, 타입: {pair.Value.GetType().Name}");
                    }
                }
                else
                {
                    Debug.Log("아이템 테이블이 비어있거나 로드되지 않았습니다.");
                }
            }
            else
            {
                Debug.LogError("itemTable 필드를 찾을 수 없습니다.");
            }
        }

        public void DebugCurrentInventory()
        {
            if (InventoryManager.Instance == null)
            {
                Debug.LogError("InventoryManager 인스턴스를 찾을 수 없습니다.");
                return;
            }

            var field = typeof(InventoryManager).GetField("items",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                var items = field.GetValue(InventoryManager.Instance) as System.Collections.Generic.Dictionary<ItemData, int>;
                if (items != null && items.Count > 0)
                {
                    Debug.Log($"=== 현재 인벤토리 (총 {items.Count}종류) ===");
                    foreach (var pair in items)
                    {
                        Debug.Log($"아이템: {pair.Key.name} (ID: {pair.Key.ID}), 수량: {pair.Value}");
                    }
                }
                else
                {
                    Debug.Log("인벤토리가 비어있습니다.");
                }
            }
            else
            {
                Debug.LogError("items 필드를 찾을 수 없습니다.");
            }
        }

        [System.Serializable]
        private class SaveDataWrapper
        {
            public string data;
            public int version;
        }
    }
}
