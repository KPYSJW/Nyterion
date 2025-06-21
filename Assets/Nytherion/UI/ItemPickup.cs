using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;

public class ItemPickup : MonoBehaviour
{
    public ItemData itemData;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            if (InventoryManager.Instance.AddItem(itemData))
            {
                Destroy(gameObject);
            }
        }
    }
}
