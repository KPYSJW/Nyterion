using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;
    public List<InventorySlot> slots;

    public GameObject slotPrefab;
    public Transform slotParent;
    public int slotCount = 24;

    private void Start()
    {
        slots = new List<InventorySlot>();
        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            InventorySlot slot = slotObj.GetComponent<InventorySlot>();
            slots.Add(slot);
        }
    }
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool AddItem(ItemData newItem)
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot.item == newItem && newItem.isStackable && slot.stackCount < newItem.maxStack)
            {
                slot.stackCount++;
                slot.countText.text = slot.stackCount.ToString();
                return true;
            }
        }

        foreach (InventorySlot slot in slots)
        {
            if (slot.item == null)
            {
                slot.SetItem(newItem);
                return true;
            }
        }
        return false; // 인벤토리 가득 참
    }

    public void SwapItems(InventorySlot from, InventorySlot to)
    {
        ItemData tempItem = to.item;
        int tempCount = to.stackCount;

        to.SetItem(from.item, from.stackCount);
        if (tempItem != null)
            from.SetItem(tempItem, tempCount);
        else
            from.ClearSlot();
    }

    public void RemoveItem(ItemData itemToRemove, int count = 1)
    {
        foreach (InventorySlot slot in slots)
        {
            if (slot.item == itemToRemove)
            {
                slot.stackCount -= count;
                if (slot.stackCount <= 0)
                {
                    slot.ClearSlot();
                }
                else
                {
                    slot.countText.text = slot.stackCount.ToString();
                }
                break;
            }
        }
    }
}
