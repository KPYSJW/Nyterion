using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class QuickSlotManager : MonoBehaviour
{
    public static QuickSlotManager Instance;
    public QuickSlotUI[] slots; // 4개 슬롯 연결

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    private void Start()
    {
        string[] keys = { "1", "2", "3", "4" };
        for (int i = 0; i < slots.Length && i < keys.Length; i++)
        {
            slots[i].SetKeyLabel(keys[i]);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) UseSlot(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) UseSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) UseSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) UseSlot(3);
    }

    public void UseSlot(int index)
    {
        if (index < 0 || index >= slots.Length) return;
        slots[index].Use();
    }

    public bool RegisterItemToSlot(ItemData item, int count, int index)
    {
        if (index < 0 || index >= slots.Length) return false;
        slots[index].SetItem(item, count);
        return true;
    }
}
