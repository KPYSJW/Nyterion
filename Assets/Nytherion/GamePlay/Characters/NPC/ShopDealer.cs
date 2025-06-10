using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.UI.Shop;
using Nytherion.Data.Shop;


namespace Nytherion.GamePlay.Characters.NPC
{
    public class ShopDealer : MonoBehaviour
    {
        [Header("Shop Settings")]
        [Tooltip("Range at which the player can interact with the shop")]
        public float interactionRange = 2f;

        [Header("Shop Data")]
        [Tooltip("Shop data containing items for sale")]
        [SerializeField] private ShopData shopData;

        private Transform player;
        private bool isPlayerInRange;

        private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
            if (shopData == null)
            {
                Debug.LogError("ShopData가 할당되지 않았습니다", this);
            }
        }

        private void Update()
        {
            if (player == null) return;

            float distance = Vector2.Distance(transform.position, player.position);
            isPlayerInRange = distance <= interactionRange;

            if (isPlayerInRange && Input.GetKeyDown(KeyCode.E) && ShopUI.Instance == null)
            {
                ShopUI.Instance.OpenShop(shopData);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
