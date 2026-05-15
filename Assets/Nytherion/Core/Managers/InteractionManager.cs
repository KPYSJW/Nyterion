using UnityEngine;
using Nytherion.UI.Controllers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Core.Data;
using VContainer;
using VContainer.Unity;
using System;

namespace Nytherion.Core.Managers
{
    public class InteractionManager : BaseManager
    {

        [Header("Interaction Settings")]
        [SerializeField] private float interactionDistance = 1.5f;
        [SerializeField] private LayerMask interactableLayer;

        private Transform playerTransform;
        private InputManager inputManager;
        private EventManager eventManager;
        private ShopUI shopUI;
        private GachaUIController gachaUIController;
        private RelicUIController relicUIController;

        private static readonly Collider2D[] interactionBuffer = new Collider2D[10];

        [Inject]
        public void Construct(
            InputManager inputManager,
            EventManager eventManager,
            ShopUI shopUI,
          GachaUIController gachaUIController,
            RelicUIController relicUIController,
            PlayerController playerController)
        {
            this.inputManager = inputManager;
            this.eventManager = eventManager;
            this.shopUI = shopUI;
            this.gachaUIController = gachaUIController;
            this.relicUIController = relicUIController;
            playerTransform = playerController.transform;

        }

        protected override void OnInitializeInternal()
        {
            if (inputManager != null)
            {
                inputManager.onInteract += HandleInteraction;
            }
            base.OnInitializeInternal();
        }

        protected override void OnDestroy()
        {
            if (inputManager != null) inputManager.onInteract -= HandleInteraction;
            base.OnDestroy();
        }

        private void HandleInteraction()
        {
            if (shopUI != null && shopUI.IsOpen)
            {
                shopUI.Close();
                return;
            }
            if (gachaUIController != null && gachaUIController.IsOpen)
            {
                gachaUIController.Close();
                return;
            }
            if (relicUIController != null && relicUIController.IsOpen)
            {
                relicUIController.Close();
                return;
            }
            if (playerTransform == null) return;

            int hitCount = Physics2D.OverlapCircleNonAlloc(playerTransform.position, interactionDistance, interactionBuffer, interactableLayer);
            if (hitCount == 0) return;

            IInteractable closestInteractable = null;
            float closestDistanceSqr = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = interactionBuffer[i];
                float distanceSqr = (col.transform.position - playerTransform.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    if (col.TryGetComponent(out IInteractable interactableObject))
                    {
                        closestDistanceSqr = distanceSqr;
                        closestInteractable = interactableObject;
                    }
                }
            }

            if (closestInteractable != null)
            {
                closestInteractable.Interact();
            }
            else
            {
                Debug.Log("[InteractionManager] 상호작용 가능한 객체를 찾지 못했습니다");
            }
        }

        public override void PopulateSaveData(SaveData saveData)
        {

        }
        
        public override void LoadFromSaveData(SaveData saveData)
        {

        }
    }
}