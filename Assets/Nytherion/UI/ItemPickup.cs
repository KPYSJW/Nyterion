using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Core;

/// <summary>
/// 필드에 드롭된 아이템을 획득하는 기능을 담당하는 클래스입니다.
/// 플레이어가 아이템에 닿으면 인벤토리에 추가됩니다.
/// </summary>

public class ItemPickup : MonoBehaviour
{
    /// <summary>
    /// 획득할 아이템의 데이터입니다.
    /// </summary>
    [Tooltip("획득할 아이템 데이터를 할당하세요.")]
    public ItemData itemData;

    /// <summary>
    /// 트리거에 다른 콜라이더가 진입할 때 호출됩니다.
    /// 플레이어가 아이템을 획득하면 인벤토리에 추가하고 아이템을 제거합니다.
    /// </summary>
    /// <param name="other">진입한 콜라이더</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어가 아이템에 닿은 경우
        if (other.CompareTag("Player"))
        {
            // 인벤토리에 아이템 추가 시도
            if (InventoryManager.Instance.AddItem(itemData))
            {
                // 성공적으로 추가되면 아이템 오브젝트 제거
                Destroy(gameObject);
            }
        }
    }
}
