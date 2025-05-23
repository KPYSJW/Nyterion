using UnityEngine;
using UnityEngine.UI;
using Nytherion.Data.ScriptableObjects.Items;
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
            // 버튼 클릭 이벤트 연결
            if (addItem1Button != null) addItem1Button.onClick.AddListener(() => AddTestItem(testItem1));
            if (addItem2Button != null) addItem2Button.onClick.AddListener(() => AddTestItem(testItem2));
            if (addItem3Button != null) addItem3Button.onClick.AddListener(() => AddTestItem(testItem3));
            
            if (removeItem1Button != null) removeItem1Button.onClick.AddListener(RemoveTestItem1);
            if (clearInventoryButton != null) clearInventoryButton.onClick.AddListener(ClearInventory);
            if (saveButton != null) saveButton.onClick.AddListener(SaveInventory);
            if (loadButton != null) loadButton.onClick.AddListener(LoadInventory);
            if (clearSaveButton != null) clearSaveButton.onClick.AddListener(ClearSaveData);
            
            // 디버그 버튼 이벤트 연결
            if (debugSaveDataButton != null) debugSaveDataButton.onClick.AddListener(DebugSaveData);
            if (debugItemTableButton != null) debugItemTableButton.onClick.AddListener(DebugItemTable);
            if (debugCurrentInventoryButton != null) debugCurrentInventoryButton.onClick.AddListener(DebugCurrentInventory);

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
            // 인벤토리에서 아이템 목록 가져오기
            var items = InventoryManager.Instance.GetAllItems();
            
            if (items.Count == 0)
            {
                ShowStatusMessage("Inventory is empty");
                return;
            }
            
            // 첫 번째 아이템 가져오기
            var firstItem = items.Keys.First();
            int count = items[firstItem];
            
            // 아이템 삭제 (최대 1개씩)
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
            InventoryManager.Instance.SaveInventory();
            ShowStatusMessage("Inventory saved");
        }

        private void LoadInventory()
        {
            try
            {
                // 인벤토리 로드
                InventoryManager.Instance.LoadInventory();
                ShowStatusMessage("Inventory loaded");
                
                // InventoryManager에 UI 갱신을 강제하도록 요청
                Debug.Log("[TestUI] Requesting UI refresh");
                
                // InventoryManager에 UI 갱신을 위한 public 메서드가 있다면 호출
                var forceUpdateMethod = typeof(InventoryManager).GetMethod("ForceUpdateUI", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    
                if (forceUpdateMethod != null)
                {
                    forceUpdateMethod.Invoke(InventoryManager.Instance, null);
                    Debug.Log("[TestUI] UI refresh requested");
                }
                else
                {
                    // ForceUpdateUI 메서드가 없으면 직접 RefreshUI 호출 시도
                    var uiField = typeof(InventoryManager).GetField("OnInventoryUpdated", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.GetField);
                        
                    if (uiField != null)
                    {
                        var eventInstance = uiField.GetValue(InventoryManager.Instance) as System.Action;
                        if (eventInstance != null)
                        {
                            Debug.Log("[TestUI] Forcing UI refresh after load");
                            eventInstance.Invoke();
                        }
                    }
                    else
                    {
                        Debug.LogWarning("[TestUI] Could not find a way to force UI refresh");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TestUI] Error during load: {e.Message}");
                ShowStatusMessage($"Load failed: {e.Message}");
            }
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
                statusText.text = message;
                currentMessage = message;
                messageTimer = messageDuration;
                
                // 콘솔에도 로그 출력 (에디터에서 확인용)
                Debug.Log($"[TestUI] {message}");
            }
        }

        // ===== 디버그 메서드들 =====

        /// <summary>
        /// 저장된 데이터를 디버그 콘솔에 출력합니다.
        /// </summary>
        public void DebugSaveData()
        {
            string saveKey = "Inventory_DefaultSlot";
            if (PlayerPrefs.HasKey(saveKey))
            {
                string saveJson = PlayerPrefs.GetString(saveKey);
                Debug.Log($"=== 저장된 데이터 ===\n{saveJson}");
                
                try
                {
                    // JSON 파싱 시도
                    var saveData = JsonUtility.FromJson<SaveDataWrapper>(saveJson);
                    if (saveData != null && !string.IsNullOrEmpty(saveData.data))
                    {
                        string decrypted = new PlayerPrefsInventorySaveService().Decrypt(saveData.data);
                        Debug.Log($"=== 복호화된 데이터 ===\n{decrypted}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"저장된 데이터 파싱 오류: {e.Message}");
                }
            }
            else
            {
                Debug.Log("저장된 데이터가 없습니다.");
            }
        }
        
        /// <summary>
        /// 아이템 테이블의 내용을 디버그 콘솔에 출력합니다.
        /// </summary>
        public void DebugItemTable()
        {
            if (InventoryManager.Instance == null)
            {
                Debug.LogError("InventoryManager 인스턴스를 찾을 수 없습니다.");
                return;
            }
            
            // 리플렉션을 사용하여 itemTable에 접근
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
        
        /// <summary>
        /// 현재 인벤토리의 내용을 디버그 콘솔에 출력합니다.
        /// </summary>
        public void DebugCurrentInventory()
        {
            if (InventoryManager.Instance == null)
            {
                Debug.LogError("InventoryManager 인스턴스를 찾을 수 없습니다.");
                return;
            }
            
            // 리플렉션을 사용하여 items에 접근
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
        
        // SaveData 래퍼 클래스 (PlayerPrefsInventorySaveService의 내부 클래스와 동일해야 함)
        [System.Serializable]
        private class SaveDataWrapper
        {
            public string data;
            public int version;
        }
    }
}
