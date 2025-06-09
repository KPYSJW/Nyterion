using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.UI.Shop;


namespace Nytherion.GamePlay.Characters.NPC
{
    public class ShopDealer : MonoBehaviour
    {
        public float interactionRange = 2f;
        private Transform player;
        private bool isPlayerInRange;

        private void Start()
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }

        private void Update()
        {
            if (player == null) return;

            float distance = Vector2.Distance(transform.position, player.position);
            isPlayerInRange = distance <= interactionRange;

            if (isPlayerInRange && Input.GetKeyDown(KeyCode.E))
            {
                ShopUI.Instance.OpenShop();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}
