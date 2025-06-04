using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;

namespace Nytherion.UI.Inventory
{
    public class QuickSlotManager : MonoBehaviour
    {
        public static QuickSlotManager Instance { get; private set; }

        [SerializeField] private QuickSlotUI[] slots; // 4개 슬롯 연결
        [SerializeField] private KeyCode[] keys = 
        { 
            KeyCode.Alpha1, 
            KeyCode.Alpha2, 
            KeyCode.Alpha3, 
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8,
            KeyCode.Alpha9,
            KeyCode.Alpha0
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            for (int i = 0; i < slots.Length && i < keys.Length; i++)
            {
                slots[i].SetKeyLabel(keys[i].ToString().Replace("Alpha", ""));
            }
        }

        private void Update()
        {
            for (int i = 0; i < keys.Length && i < slots.Length; i++)
            {
                if (Input.GetKeyDown(keys[i]))
                {
                    UseSlot(i);
                }
            }
        }

        public void UseSlot(int index)
        {
            if (index < 0 || index >= slots.Length) return;
            slots[index].UseItem();
        }

        public bool RegisterItemToSlot(ItemData item, int count, int index)
        {
            if (index < 0 || index >= slots.Length) return false;
            slots[index].SetItem(item, count);
            return true;
        }
    }
}