using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;

public class ItemOnCursor : MonoBehaviour
{
    public static ItemData Item { get; private set; }
    public static int Count { get; private set; }
    public static bool IsHoldingItem => Item != null;

    public static void Set(ItemData item, int count)
    {
        Item = item;
        Count = count;
        if (item != null)
        {
            DragItemIcon.Instance.SetIcon(item.icon);
            DragItemIcon.Instance.Show();
        }
        else
        {
            DragItemIcon.Instance.Hide();
        }
    }
    public static (ItemData item, int count) GetAndClear()
    {
        var held = (Item, Count);
        Set(null, 0);
        return held;
    }
}
